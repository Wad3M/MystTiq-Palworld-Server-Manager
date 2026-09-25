using System.Diagnostics;
using System.Text.Json;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.8.17.0: process priority and eco mode for one server (see ServerResourcePolicy). The policy is kept in the
// server's own runtime folder and applied on the automation tick (every 15 s) to every managed process -- the game
// process itself is created after launch and on every restart -- and at once when it is saved. A process is only
// touched when it differs from what the policy wants, so each change is logged once, not every tick.
// v0.8.24.0: also the processor cores (affinity), and applied as soon as a start through MystTiq succeeds
// (PolicyApplyingLifecycle), not only on the next tick.
public sealed class HeadlessResourcePolicyService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly IServerPathProfile paths;
    private readonly IServerLifecycleService lifecycle;
    private readonly Func<CancellationToken, Task<int?>> playersOnline;
    private readonly HeadlessActivityLogService activity;
    private readonly IProcessResourceControl control;
    private readonly Func<DateTimeOffset> clock;
    private readonly string policyPath;
    private readonly SemaphoreSlim applyGate = new(1, 1);
    private readonly HashSet<int> efficiencySetByMystTiq = [];
    private readonly HashSet<int> affinityPinnedByMystTiq = [];
    private readonly int processorCount;
    private ServerResourcePolicy policy;
    private DateTimeOffset? emptySinceUtc;
    private bool? lastEcoActive;
    private string lastError = string.Empty;
    private DateTimeOffset? lastAppliedUtc;

    public HeadlessResourcePolicyService(
        IServerPathProfile paths,
        IServerLifecycleService lifecycle,
        Func<CancellationToken, Task<int?>> playersOnline,
        HeadlessActivityLogService activity,
        IProcessResourceControl? control = null,
        Func<DateTimeOffset>? clock = null,
        int? processorCount = null)
    {
        this.paths = paths;
        this.lifecycle = lifecycle;
        this.playersOnline = playersOnline;
        this.activity = activity;
        this.control = control ?? ProcessResourceControl.ForCurrentPlatform();
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.processorCount = processorCount ?? Environment.ProcessorCount;
        policyPath = Path.Combine(paths.ManagerRuntimeRoot, "resource-policy.json");
        policy = Load();
    }

    public ServerResourcePolicy GetPolicy() => policy;

    public async Task<HeadlessResourcePolicySaveResult> SaveAsync(ServerResourcePolicy requested, string? actor, CancellationToken token)
    {
        var errors = requested.Validate(processorCount);
        if (errors.Count > 0) return new HeadlessResourcePolicySaveResult(false, string.Join(" ", errors), policy, null);
        Directory.CreateDirectory(paths.ManagerRuntimeRoot);
        var temp = policyPath + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(requested, JsonOptions), token);
        File.Move(temp, policyPath, overwrite: true);
        policy = requested;
        activity.Record("Information", "Performance", "Process priority and eco mode saved",
            $"Priority {requested.Priority}, eco mode {requested.EcoMode}" + (requested.EcoMode == ServerEcoMode.WhenEmpty ? $" after {requested.EcoAfterEmptyMinutes} min empty" : "") + (string.IsNullOrWhiteSpace(requested.Cores) ? ", every core." : $", cores {requested.Cores}."), actor);
        var snapshot = await ApplyAsync(token);
        return new HeadlessResourcePolicySaveResult(true, "Saved and applied.", requested, snapshot);
    }

    // The automation tick and a save both land here.
    public async Task<HeadlessResourcePolicySnapshot> ApplyAsync(CancellationToken token) => await RunAsync(apply: true, token);

    // The HOST tab's read: what the processes are now, without changing anything.
    public async Task<HeadlessResourcePolicySnapshot> GetSnapshotAsync(CancellationToken token) => await RunAsync(apply: false, token);

    private async Task<HeadlessResourcePolicySnapshot> RunAsync(bool apply, CancellationToken token)
    {
        await applyGate.WaitAsync(token);
        try
        {
            var now = clock();
            var status = await lifecycle.GetStatusAsync(token);
            var running = status.Processes.Count > 0;
            int? online = null;
            if (policy.EcoMode == ServerEcoMode.WhenEmpty && running)
            {
                try { online = await playersOnline(token); }
                catch (Exception ex) when (ex is not OperationCanceledException) { online = null; }
            }
            if (apply) emptySinceUtc = running ? ResourcePolicyDecision.NextEmptySince(emptySinceUtc, online, now) : null;
            var eco = running
                ? ResourcePolicyDecision.Eco(policy, online, emptySinceUtc, now)
                : new EcoDecision(false, "The server is not running.");

            var errors = new List<string>();
            if (apply && running)
            {
                var targetPriority = ResourcePolicyDecision.TargetPriority(policy, eco.Active);
                foreach (var process in status.Processes)
                {
                    if (targetPriority is { } wanted && control.GetPriority(process.ProcessId) is { } current && current != wanted)
                    {
                        try
                        {
                            control.SetPriority(process.ProcessId, wanted);
                            activity.Record("Information", "Performance", $"{process.ProcessName} priority set to {wanted}",
                                $"PID {process.ProcessId}, was {current}. {eco.Reason}");
                        }
                        catch (Exception ex) { errors.Add($"{process.ProcessName} (PID {process.ProcessId}) priority: {ex.Message}"); }
                    }
                    if (control.SupportsEfficiencyMode)
                    {
                        var wantEfficiency = ResourcePolicyDecision.TargetEfficiency(eco.Active, efficiencySetByMystTiq.Contains(process.ProcessId));
                        var state = control.GetEfficiency(process.ProcessId);
                        var needsChange = wantEfficiency switch
                        {
                            true => state != EfficiencyState.On,
                            false => state == EfficiencyState.On,
                            _ => false,
                        };
                        if (needsChange)
                        {
                            try
                            {
                                control.SetEfficiency(process.ProcessId, wantEfficiency == true);
                                if (wantEfficiency == true) efficiencySetByMystTiq.Add(process.ProcessId);
                                else efficiencySetByMystTiq.Remove(process.ProcessId);
                            }
                            catch (Exception ex) { errors.Add($"{process.ProcessName} (PID {process.ProcessId}) efficiency mode: {ex.Message}"); }
                        }
                        else if (wantEfficiency == false) efficiencySetByMystTiq.Remove(process.ProcessId);
                    }
                    // v0.8.24.0: processor cores.
                    var pinning = !string.IsNullOrWhiteSpace(policy.Cores);
                    if (ResourcePolicyDecision.TargetAffinity(policy, processorCount, affinityPinnedByMystTiq.Contains(process.ProcessId)) is { } wantedCores)
                    {
                        var currentCores = control.GetAffinity(process.ProcessId);
                        if (currentCores is { } coresNow && coresNow != wantedCores)
                        {
                            try
                            {
                                control.SetAffinity(process.ProcessId, wantedCores);
                                activity.Record("Information", "Performance", $"{process.ProcessName} cores set to {CoreSelection.Describe(wantedCores, processorCount)}",
                                    $"PID {process.ProcessId}, was {CoreSelection.Describe(coresNow, processorCount)}.");
                                if (pinning) affinityPinnedByMystTiq.Add(process.ProcessId); else affinityPinnedByMystTiq.Remove(process.ProcessId);
                            }
                            catch (Exception ex) { errors.Add($"{process.ProcessName} (PID {process.ProcessId}) cores: {ex.Message}"); }
                        }
                        else if (currentCores is not null)
                        {
                            if (pinning) affinityPinnedByMystTiq.Add(process.ProcessId); else affinityPinnedByMystTiq.Remove(process.ProcessId);
                        }
                    }
                }
                // Processes that ended are forgotten.
                efficiencySetByMystTiq.IntersectWith(status.Processes.Select(p => p.ProcessId));
                affinityPinnedByMystTiq.IntersectWith(status.Processes.Select(p => p.ProcessId));
                if (lastEcoActive is { } before && before != eco.Active)
                    activity.Record("Information", "Performance", eco.Active ? "Eco mode on" : "Eco mode off", eco.Reason);
                lastEcoActive = eco.Active;
                lastError = string.Join(" ", errors);
                lastAppliedUtc = now;
            }

            var processes = status.Processes.Select(p => Describe(p.ProcessId, p.ProcessName)).ToArray();
            return new HeadlessResourcePolicySnapshot(policy, running, eco.Active, eco.Reason, online, emptySinceUtc,
                control.SupportsEfficiencyMode, processes, lastError, lastAppliedUtc, now, CoreSelection.UsableCores(processorCount));
        }
        finally { applyGate.Release(); }
    }

    private HeadlessProcessResourceState Describe(int processId, string name)
    {
        long workingSet = 0;
        try { using var process = Process.GetProcessById(processId); workingSet = process.WorkingSet64; } catch { }
        return new HeadlessProcessResourceState(processId, name, control.GetPriority(processId)?.ToString() ?? "Unknown",
            control.GetEfficiency(processId).ToString(), workingSet,
            control.GetAffinity(processId) is { } mask ? CoreSelection.Describe(mask, processorCount) : "Unknown");
    }

    private ServerResourcePolicy Load()
    {
        try
        {
            if (!File.Exists(policyPath)) return new ServerResourcePolicy();
            var loaded = JsonSerializer.Deserialize<ServerResourcePolicy>(File.ReadAllText(policyPath), JsonOptions);
            return loaded is not null && loaded.Validate(processorCount).Count == 0 ? loaded : new ServerResourcePolicy();
        }
        catch
        {
            // A damaged file must not stop the server's management; nothing is applied until it is saved again.
            return new ServerResourcePolicy();
        }
    }
}

public sealed record HeadlessProcessResourceState(int ProcessId, string ProcessName, string Priority, string Efficiency, long WorkingSetBytes, string Cores = "Unknown");

public sealed record HeadlessResourcePolicySnapshot(
    ServerResourcePolicy Policy,
    bool Running,
    bool EcoActive,
    string EcoReason,
    int? PlayersOnline,
    DateTimeOffset? EmptySinceUtc,
    bool EfficiencyModeSupported,
    IReadOnlyList<HeadlessProcessResourceState> Processes,
    string LastError,
    DateTimeOffset? LastAppliedUtc,
    DateTimeOffset ObservedAt,
    int ProcessorCount = 0);

public sealed record HeadlessResourcePolicySaveResult(bool Success, string Message, ServerResourcePolicy Policy, HeadlessResourcePolicySnapshot? Snapshot);
