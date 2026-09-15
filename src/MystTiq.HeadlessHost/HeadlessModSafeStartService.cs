using MystTiq.Core.Models;
using MystTiq.Core.Operations;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.7.59.0: Safe-Start MOD Diagnostic. Direct request, grounded in real community practice found
// via research: "remove mods one by one until the crash stops" is the standard, widely-documented
// manual troubleshooting technique for Palworld server crashes -- this automates exactly that,
// rather than inventing a new approach. Requested detection covers BOTH failure modes actually seen
// in this project's own live investigation this session: an outright crash (ServerLifecycleSnapshot
// .CrashDetected) and a hang that never reaches ready (HeadlessExitCode.StartupTimeout) -- a naive
// crash-exit-only check would have missed the exact freeze this session spent hours investigating.
//
// Algorithm, in order:
//   0. Baseline sanity check: disable every currently-enabled MOD, then attempt one start with
//      NONE of them. If even this fails, the problem isn't MOD-related at all -- abort immediately
//      and say so, rather than cycling through every MOD and reporting each one as "bad" when the
//      real cause is environmental (this exact scenario was independently confirmed this session:
//      the live freeze persisted with the entire UE4SS/PalDefender/d3d9 injection chain removed).
//   1. Incrementally re-enable one MOD at a time on top of the already-confirmed-good set (not
//      each MOD tested totally alone -- real MOD ecosystems have dependencies, e.g. shared/core
//      UE4SS libraries other mods rely on, so testing in isolation would produce false failures).
//   2. A MOD that fails (crash or hang) is disabled again and recorded; the run continues with the
//      remaining candidates on top of the still-confirmed-good set.
//   3. Final start with the surviving set, left running on success -- the whole point is automatic
//      recovery, not just a diagnostic report.
public sealed class HeadlessModSafeStartService
{
    private readonly IServerLifecycleService lifecycle;
    private readonly HeadlessModManagementService modManagement;
    private readonly HeadlessServerProfileConfiguration serverProfile;
    private readonly HeadlessActivityLogService activity;
    private readonly IOperationCoordinator operations;
    private readonly ServerProfileId profileId;
    private readonly TimeSpan startupTimeout;
    private readonly TimeSpan stopTimeout;
    private readonly object gate = new();
    private SafeStartJob? currentJob;

    public HeadlessModSafeStartService(
        IServerLifecycleService lifecycle, HeadlessModManagementService modManagement,
        HeadlessServerProfileConfiguration serverProfile, HeadlessActivityLogService activity,
        IOperationCoordinator operations, ServerProfileId profileId,
        TimeSpan startupTimeout, TimeSpan stopTimeout)
    {
        this.lifecycle = lifecycle;
        this.modManagement = modManagement;
        this.serverProfile = serverProfile;
        this.activity = activity;
        this.operations = operations;
        this.profileId = profileId;
        this.startupTimeout = startupTimeout;
        this.stopTimeout = stopTimeout;
    }

    public SafeStartStatus? GetStatus()
    {
        lock (gate) return currentJob?.ToStatus();
    }

    public async Task<HeadlessModMutationResult> BeginAsync(CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (currentJob is { IsRunning: true })
                return HeadlessModMutationResult.Failure("A safe-start diagnostic is already running.");
        }

        var status = await lifecycle.GetStatusAsync(cancellationToken);
        if (status.NativeProcessId.HasValue || status.Ready)
            return HeadlessModMutationResult.Failure("Stop the server before starting a safe-start diagnostic -- it needs to control start/stop cycles itself.");

        var inventory = await modManagement.GetInventoryAsync(cancellationToken);
        var candidates = inventory.Mods.Where(m => m.Enabled).ToList();
        if (candidates.Count == 0)
            return HeadlessModMutationResult.Failure("No enabled MODs to test -- nothing for a safe-start diagnostic to do.");

        // Held for the ENTIRE background job, not just this synchronous call -- a manual
        // Start/Stop/Restart hitting the same "lifecycle"/"world-mutation" resource keys mid-run
        // would corrupt the test (two things fighting over the same process). RunLifecycleActionAsync
        // elsewhere in this file only holds its handle for one synchronous call; this is a genuinely
        // different shape (multi-minute unattended background work), so the handle's Complete/Fail/
        // Dispose happen inside RunAsync's own finally block instead.
        OperationHandle handle;
        try
        {
            handle = await operations.BeginAsync(profileId, "mods-safe-start", "HeadlessModSafeStartService", ["lifecycle", "world-mutation"], cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return HeadlessModMutationResult.Failure($"Another lifecycle operation is already in progress: {ex.Message}");
        }

        var job = new SafeStartJob(candidates.Select(m => (m.Type, m.Package)).ToList());
        lock (gate) { currentJob = job; }

        activity.Record("Information", "Mods", "Safe-start diagnostic started", $"{candidates.Count} enabled MOD(s) queued for testing.");
        _ = Task.Run(() => RunAsync(job, candidates, handle, job.Cts.Token));
        return new HeadlessModMutationResult(true, string.Empty, string.Empty, false, 0,
            $"Safe-start diagnostic began: {candidates.Count} MOD(s) will be tested one at a time.");
    }

    public Task<HeadlessModMutationResult> CancelAsync()
    {
        SafeStartJob? job;
        lock (gate) job = currentJob;
        if (job is not { IsRunning: true })
            return Task.FromResult(HeadlessModMutationResult.Failure("No safe-start diagnostic is currently running."));
        job.Cts.Cancel();
        return Task.FromResult(new HeadlessModMutationResult(true, string.Empty, string.Empty, false, 0,
            "Cancelling -- every originally-enabled MOD will be restored once the current start/stop cycle finishes."));
    }

    private async Task RunAsync(SafeStartJob job, List<HeadlessModItem> candidates, OperationHandle handle, CancellationToken ct)
    {
        try
        {
            await RunCoreAsync(job, candidates, ct);
        }
        finally
        {
            // Every RunCoreAsync exit path (including its own catch) calls one of job.Finish*
            // before returning, so job.Success reliably reflects the real outcome here regardless
            // of which path was taken.
            if (job.Success) operations.Complete(handle.Id, job.ToStatus().FinalMessage);
            else operations.Fail(handle.Id, job.ToStatus().FinalMessage);
            handle.Dispose();
        }
    }

    private async Task RunCoreAsync(SafeStartJob job, List<HeadlessModItem> candidates, CancellationToken ct)
    {
        try
        {
            job.SetPhase("Establishing a known-clean baseline (disabling all candidates)…");
            foreach (var mod in candidates)
                await modManagement.SetEnabledAsync(mod.Type, mod.Package, false, CancellationToken.None);

            job.SetPhase("Testing baseline: starting with none of the candidate MODs enabled…");
            var baseline = await lifecycle.StartAsync(serverProfile.LaunchArguments, startupTimeout, ct);
            if (!IsHealthyStart(baseline))
            {
                await lifecycle.StopAsync(TimeSpan.Zero, CancellationToken.None);
                foreach (var mod in candidates)
                    await modManagement.SetEnabledAsync(mod.Type, mod.Package, true, CancellationToken.None);
                job.FinishNotModRelated(DescribeFailure(baseline));
                activity.Record("Warning", "Mods", "Safe-start diagnostic: not a MOD problem",
                    "The server failed to start even with every candidate MOD disabled -- restored original MOD state and stopped.");
                return;
            }
            await lifecycle.StopAsync(stopTimeout, CancellationToken.None);

            foreach (var mod in candidates)
            {
                if (ct.IsCancellationRequested) break;

                job.SetCurrent(mod.Package);
                await modManagement.SetEnabledAsync(mod.Type, mod.Package, true, CancellationToken.None);
                var result = await lifecycle.StartAsync(serverProfile.LaunchArguments, startupTimeout, ct);

                if (IsHealthyStart(result))
                {
                    await lifecycle.StopAsync(stopTimeout, CancellationToken.None);
                    job.RecordResult(mod.Package, ok: true, "Started and became ready.");
                }
                else
                {
                    await lifecycle.StopAsync(TimeSpan.Zero, CancellationToken.None);
                    await modManagement.SetEnabledAsync(mod.Type, mod.Package, false, CancellationToken.None);
                    job.RecordResult(mod.Package, ok: false, DescribeFailure(result));
                }
            }

            if (ct.IsCancellationRequested)
            {
                foreach (var mod in candidates)
                    await modManagement.SetEnabledAsync(mod.Type, mod.Package, true, CancellationToken.None);
                job.FinishCancelled();
                activity.Record("Information", "Mods", "Safe-start diagnostic cancelled", "Original MOD state restored.");
                return;
            }

            job.SetPhase("Final validation: starting with the surviving MOD set…");
            var final = await lifecycle.StartAsync(serverProfile.LaunchArguments, startupTimeout, CancellationToken.None);
            job.Finish(IsHealthyStart(final), DescribeFailure(final));
            activity.Record(job.Success ? "Information" : "Warning", "Mods", "Safe-start diagnostic complete",
                $"{job.BadMods.Count} MOD(s) disabled: {(job.BadMods.Count > 0 ? string.Join(", ", job.BadMods) : "none")}.");
        }
        catch (Exception ex)
        {
            try { await lifecycle.StopAsync(TimeSpan.Zero, CancellationToken.None); } catch { /* best effort */ }
            job.FinishError(ex.Message);
            activity.Record("Error", "Mods", "Safe-start diagnostic failed", ex.Message);
        }
    }

    private static bool IsHealthyStart(ServerLifecycleOperationResult result) =>
        result.Success && result.Snapshot.Ready && !result.Snapshot.CrashDetected;

    private static string DescribeFailure(ServerLifecycleOperationResult result) =>
        result.ExitCode == HeadlessExitCode.StartupTimeout
            ? "Timed out without becoming ready (hang, not a crash)."
            : result.Snapshot.CrashDetected
                ? "Process crashed before becoming ready."
                : result.Message;
}

public sealed class SafeStartJob
{
    private readonly List<(string Type, string Package)> candidates;
    private readonly List<SafeStartModResult> results = [];
    private readonly object gate = new();
    private string phase;
    private string? currentPackage;
    private bool completed;
    private bool cancelled;
    private bool notModRelated;
    private string finalMessage = string.Empty;

    public SafeStartJob(List<(string Type, string Package)> candidates)
    {
        this.candidates = candidates;
        phase = "Starting…";
        StartedAtUtc = DateTime.UtcNow;
    }

    public CancellationTokenSource Cts { get; } = new();
    public DateTime StartedAtUtc { get; }
    public bool IsRunning { get { lock (gate) return !completed; } }
    public bool Success { get; private set; }
    public IReadOnlyList<string> BadMods { get { lock (gate) return results.Where(r => !r.Ok).Select(r => r.Package).ToArray(); } }

    public void SetPhase(string text) { lock (gate) { phase = text; currentPackage = null; } }
    public void SetCurrent(string package) { lock (gate) { currentPackage = package; phase = $"Testing {package}…"; } }
    public void RecordResult(string package, bool ok, string detail) { lock (gate) results.Add(new SafeStartModResult(package, ok, detail)); }

    public void Finish(bool success, string detail)
    {
        lock (gate) { completed = true; Success = success; finalMessage = success ? "Completed successfully." : detail; }
    }
    public void FinishNotModRelated(string detail)
    {
        lock (gate) { completed = true; notModRelated = true; Success = false; finalMessage = $"Not a MOD problem -- {detail}"; }
    }
    public void FinishCancelled() { lock (gate) { completed = true; cancelled = true; Success = false; finalMessage = "Cancelled; original MOD state restored."; } }
    public void FinishError(string message) { lock (gate) { completed = true; Success = false; finalMessage = message; } }

    public SafeStartStatus ToStatus()
    {
        lock (gate)
            return new SafeStartStatus(
                !completed, completed, cancelled, notModRelated, Success, phase, currentPackage,
                candidates.Count, results.Count, results.Select(r => r).ToArray(), finalMessage, StartedAtUtc);
    }
}

public sealed record SafeStartModResult(string Package, bool Ok, string Detail);

public sealed record SafeStartStatus(
    bool IsRunning, bool Completed, bool Cancelled, bool NotModRelated, bool Success,
    string Phase, string? CurrentPackage, int TotalCandidates, int TestedCount,
    IReadOnlyList<SafeStartModResult> Results, string FinalMessage, DateTime StartedAtUtc);
