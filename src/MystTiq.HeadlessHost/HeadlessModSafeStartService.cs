// MystTiq v1.0.0.5: file reviewed for this release (2026-10-05).
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
//
// v1.0.0.1, the stuck-start protocol (requested 2026-09-30, after the main server spun for minutes without opening its
// port): it can begin while a start is stuck (the stuck process is stopped first), a "test load" mode only starts once
// with every MOD off and then restores them, every start's time to ready is recorded, and a MOD that cannot be switched
// ends the test instead of being tested in the wrong state.
public enum SafeStartMode { OneAtATime, TestLoad }

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

    public Task<HeadlessModMutationResult> BeginAsync(CancellationToken cancellationToken) => BeginAsync(SafeStartMode.OneAtATime, cancellationToken);

    public async Task<HeadlessModMutationResult> BeginAsync(SafeStartMode mode, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (currentJob is { IsRunning: true })
                return HeadlessModMutationResult.Failure("A safe-start diagnostic is already running.");
        }

        // v1.0.0.1: a start that is stuck (running, port never opened, for StartupWatch.StuckAfter) may be tested: the job
        // stops it first. A running, ready server, or one still within its normal start time, is left alone.
        var status = await lifecycle.GetStatusAsync(cancellationToken);
        var stopStuckFirst = status.Processes.Count > 0 && status.StartupStuck;
        if (!stopStuckFirst && (status.Processes.Count > 0 || status.Ready))
            return HeadlessModMutationResult.Failure("Stop the server before starting a safe-start diagnostic -- it needs to control start/stop cycles itself. (A start that is stuck can be tested without stopping it first.)");

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

        var job = new SafeStartJob(candidates.Select(m => (m.Type, m.Package)).ToList(), mode);
        lock (gate) { currentJob = job; }

        activity.Record("Information", "Mods", mode == SafeStartMode.TestLoad ? "Stuck-start test load started" : "Safe-start diagnostic started",
            $"{candidates.Count} enabled MOD(s){(stopStuckFirst ? "; the stuck start is stopped first" : string.Empty)}.");
        _ = Task.Run(() => RunAsync(job, candidates, stopStuckFirst, handle, job.Cts.Token));
        return new HeadlessModMutationResult(true, string.Empty, string.Empty, false, 0,
            mode == SafeStartMode.TestLoad
                ? $"Test load began: the server starts once with every MOD off ({candidates.Count}), then they are switched back on."
                : $"Safe-start diagnostic began: {candidates.Count} MOD(s) will be tested one at a time.");
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

    private async Task RunAsync(SafeStartJob job, List<HeadlessModItem> candidates, bool stopStuckFirst, OperationHandle handle, CancellationToken ct)
    {
        try
        {
            if (stopStuckFirst)
            {
                job.SetPhase("Stopping the stuck start…");
                await lifecycle.StopAsync(TimeSpan.Zero, CancellationToken.None);
            }
            if (job.Mode == SafeStartMode.TestLoad) await RunTestLoadAsync(job, candidates, ct);
            else await RunCoreAsync(job, candidates, ct);
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

    // v1.0.0.1: switches a MOD and says why when it could not. The test only means something when every MOD is in the
    // state it believes; v0.7.59.0 ignored a refused switch.
    private async Task SwitchAsync(HeadlessModItem mod, bool enabled)
    {
        var result = await modManagement.SetEnabledAsync(mod.Type, mod.Package, enabled, CancellationToken.None);
        if (!result.Success)
            throw new InvalidOperationException(enabled ? $"{mod.Package} could not be enabled: {result.Message}" : $"{mod.Package} could not be disabled: {result.Message}");
    }

    private async Task RestoreAsync(List<HeadlessModItem> candidates)
    {
        foreach (var mod in candidates)
            await modManagement.SetEnabledAsync(mod.Type, mod.Package, true, CancellationToken.None);
    }

    // v1.0.0.1: one start with every MOD off, timed; then the server is stopped and the MODs switched back on.
    private async Task RunTestLoadAsync(SafeStartJob job, List<HeadlessModItem> candidates, CancellationToken ct)
    {
        try
        {
            job.SetPhase("Switching every MOD off for the test load…");
            foreach (var mod in candidates) await SwitchAsync(mod, false);

            job.SetPhase("Test load: starting with every MOD off…");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var load = await lifecycle.StartAsync(serverProfile.LaunchArguments, startupTimeout, ct);
            var healthy = IsHealthyStart(load);
            job.RecordResult("(no MODs)", healthy, healthy ? $"Ready in {StartupWatch.Describe(clock.Elapsed)}." : DescribeFailure(load));
            await lifecycle.StopAsync(healthy ? stopTimeout : TimeSpan.Zero, CancellationToken.None);

            job.SetPhase("Switching the MODs back on…");
            await RestoreAsync(candidates);
            if (healthy)
                job.Finish(true, string.Empty, $"Without MODs the server was ready in {StartupWatch.Describe(clock.Elapsed)}, so a MOD is the likely cause. Run the one-at-a-time test to find it. Every MOD is switched back on and the server is stopped.");
            else
                job.FinishNotModRelated(DescribeFailure(load) + " Every MOD is switched back on and the server is stopped.");
            activity.Record(healthy ? "Information" : "Warning", "Mods", "Stuck-start test load complete",
                healthy ? $"Ready without MODs in {StartupWatch.Describe(clock.Elapsed)}." : "Not ready even without MODs: not a MOD problem.");
        }
        catch (Exception ex)
        {
            try { await lifecycle.StopAsync(TimeSpan.Zero, CancellationToken.None); } catch { /* best effort */ }
            await RestoreAsync(candidates);
            job.FinishError(ex is OperationCanceledException ? "Cancelled; every MOD is switched back on." : ex.Message);
            activity.Record("Error", "Mods", "Stuck-start test load failed", ex.Message);
        }
    }

    private async Task RunCoreAsync(SafeStartJob job, List<HeadlessModItem> candidates, CancellationToken ct)
    {
        try
        {
            job.SetPhase("Establishing a known-clean baseline (disabling all candidates)…");
            foreach (var mod in candidates)
                await SwitchAsync(mod, false);

            job.SetPhase("Testing baseline: starting with none of the candidate MODs enabled…");
            var baselineClock = System.Diagnostics.Stopwatch.StartNew();
            var baseline = await lifecycle.StartAsync(serverProfile.LaunchArguments, startupTimeout, ct);
            job.RecordResult("(no MODs)", IsHealthyStart(baseline), IsHealthyStart(baseline) ? $"Ready in {StartupWatch.Describe(baselineClock.Elapsed)}." : DescribeFailure(baseline));
            if (!IsHealthyStart(baseline))
            {
                await lifecycle.StopAsync(TimeSpan.Zero, CancellationToken.None);
                await RestoreAsync(candidates);
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
                await SwitchAsync(mod, true);
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var result = await lifecycle.StartAsync(serverProfile.LaunchArguments, startupTimeout, ct);

                if (IsHealthyStart(result))
                {
                    await lifecycle.StopAsync(stopTimeout, CancellationToken.None);
                    job.RecordResult(mod.Package, ok: true, $"Ready in {StartupWatch.Describe(clock.Elapsed)}.");
                }
                else
                {
                    await lifecycle.StopAsync(TimeSpan.Zero, CancellationToken.None);
                    await SwitchAsync(mod, false);
                    job.RecordResult(mod.Package, ok: false, DescribeFailure(result));
                }
            }

            if (ct.IsCancellationRequested)
            {
                await RestoreAsync(candidates);
                job.FinishCancelled();
                activity.Record("Information", "Mods", "Safe-start diagnostic cancelled", "Original MOD state restored.");
                return;
            }

            job.SetPhase("Final validation: starting with the surviving MOD set…");
            var final = await lifecycle.StartAsync(serverProfile.LaunchArguments, startupTimeout, CancellationToken.None);
            job.Finish(IsHealthyStart(final), DescribeFailure(final),
                job.BadMods.Count > 0 ? $"Left off: {string.Join(", ", job.BadMods)}. The server is running with the other MODs." : "Every MOD started; the server is running with all of them.");
            activity.Record(job.Success ? "Information" : "Warning", "Mods", "Safe-start diagnostic complete",
                $"{job.BadMods.Count} MOD(s) disabled: {(job.BadMods.Count > 0 ? string.Join(", ", job.BadMods) : "none")}.");
        }
        catch (Exception ex)
        {
            try { await lifecycle.StopAsync(TimeSpan.Zero, CancellationToken.None); } catch { /* best effort */ }
            // v1.0.0.1: a failure part-way (a MOD that could not be switched) puts every MOD back as it was.
            await RestoreAsync(candidates);
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

    public SafeStartJob(List<(string Type, string Package)> candidates, SafeStartMode mode = SafeStartMode.OneAtATime)
    {
        this.candidates = candidates;
        Mode = mode;
        phase = "Starting…";
        StartedAtUtc = DateTime.UtcNow;
    }

    public CancellationTokenSource Cts { get; } = new();
    public SafeStartMode Mode { get; }
    public DateTime StartedAtUtc { get; }
    public bool IsRunning { get { lock (gate) return !completed; } }
    public bool Success { get; private set; }
    public IReadOnlyList<string> BadMods { get { lock (gate) return results.Where(r => !r.Ok && r.Package != "(no MODs)").Select(r => r.Package).ToArray(); } }

    public void SetPhase(string text) { lock (gate) { phase = text; currentPackage = null; } }
    public void SetCurrent(string package) { lock (gate) { currentPackage = package; phase = $"Testing {package}…"; } }
    public void RecordResult(string package, bool ok, string detail) { lock (gate) results.Add(new SafeStartModResult(package, ok, detail)); }

    public void Finish(bool success, string detail, string? successMessage = null)
    {
        lock (gate) { completed = true; Success = success; finalMessage = success ? successMessage ?? "Completed successfully." : detail; }
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
                candidates.Count, results.Count(r => r.Package != "(no MODs)"), results.Select(r => r).ToArray(), finalMessage, StartedAtUtc)
            { Mode = Mode == SafeStartMode.TestLoad ? "TestLoad" : "OneAtATime" };
    }
}

public sealed record SafeStartModResult(string Package, bool Ok, string Detail);

public sealed record SafeStartStatus(
    bool IsRunning, bool Completed, bool Cancelled, bool NotModRelated, bool Success,
    string Phase, string? CurrentPackage, int TotalCandidates, int TestedCount,
    IReadOnlyList<SafeStartModResult> Results, string FinalMessage, DateTime StartedAtUtc)
{
    public string Mode { get; init; } = "OneAtATime";
}
