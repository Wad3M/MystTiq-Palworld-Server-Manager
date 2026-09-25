using MystTiq.Core.Models;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.6.13.0: closes the real gap the v0.6.12.0 gap audit flagged -- crash-detect-and-auto-restart
// (HeadlessSupervisor) was only ever active inside the installed OS service (service-run), and only
// for the default profile. api-run (Desktop's own sidecar, and every other profile in the fleet)
// had zero crash recovery. This is a thin per-profile wrapper around the exact same
// HeadlessSupervisor logic service-run already relies on -- reused via RunCrashRecoveryLoopAsync,
// which deliberately skips RunAsync's "ensure started on launch" preamble (right for service-run's
// unattended-boot semantics, wrong here: a profile nobody has started yet is not a crash). Started
// and stopped exactly where HeadlessAutomationService already is, per profile.
//
// v0.7.110.0: once RunCrashRecoveryLoopAsync gives up (too many restarts inside the window), it
// RETURNS -- by design, since service-run relies on that exit code for the OS's own recovery policy,
// and that contract is not touched here. But for api-run that return meant nothing ever watched the
// profile again: the pinned "server is DOWN" notice (CrashAlertObserver) had no way to know a person
// later started it manually, so it stayed pinned forever with no "back up" notice at all. This class
// (api-run-only, never used by service-run) now wraps the loop instead of running it once: after a
// give-up, it watches (same PollInterval) purely for the process reappearing, tells the observer
// (ManualRecovery), and resumes full crash-detect-and-restart monitoring on the SAME supervisor
// instance -- CanRestart's own window-aging already handles a stale restart history safely, so nothing
// needs to be reset by hand.
public sealed class HeadlessFleetCrashRecoveryService : IAsyncDisposable
{
    private readonly HeadlessSupervisor supervisor;
    private readonly IServerLifecycleService lifecycle;
    private readonly HeadlessSupervisorOptions options;
    private readonly ISupervisorObserver? observer;
    // v0.7.115.0: the restart window, the give-up and the pinned DOWN notice survive a MystTiq restart.
    private readonly SupervisorRecoveryStateStore? stateStore;

    private CancellationTokenSource? loopCts;
    private Task? loopTask;

    public HeadlessFleetCrashRecoveryService(
        IServerLifecycleService lifecycle,
        HeadlessSupervisorOptions options,
        IReadOnlyList<string> serverArguments,
        ISupervisorObserver? observer = null,
        SupervisorRecoveryStateStore? stateStore = null)
    {
        this.lifecycle = lifecycle;
        this.options = options;
        this.observer = observer;
        this.stateStore = stateStore;
        // v0.7.101.0: the optional observer is how a crash, a recovery and a give-up reach the
        // notification pipeline instead of only the console.
        supervisor = new HeadlessSupervisor(lifecycle, options, serverArguments, observer, stateStore);
    }

    public Task StartAsync(CancellationToken hostShutdownToken)
    {
        loopCts = CancellationTokenSource.CreateLinkedTokenSource(hostShutdownToken);
        loopTask = Task.Run(() => RunWithGiveUpWatchAsync(loopCts.Token));
        return Task.CompletedTask;
    }

    private async Task RunWithGiveUpWatchAsync(CancellationToken token)
    {
        // v0.7.115.0: if recovery had already given up before MystTiq last stopped, carry on where it left off
        // (watching for the server to come back) instead of treating the still-down server as a new crash.
        var resumeWatching = stateStore?.Read().GaveUpAtUtc is not null;
        while (!token.IsCancellationRequested)
        {
            if (!resumeWatching)
            {
                var exitCode = await supervisor.RunCrashRecoveryLoopAsync(token);
                if (token.IsCancellationRequested || exitCode != (int)HeadlessExitCode.CrashDetected)
                    return; // cancelled, or an unexpected exit -- nothing left to watch for.
                // (The supervisor itself records the give-up in the shared state store since v0.8.2.0.)
            }
            resumeWatching = false;

            if (!await WaitForManualRecoveryAsync(token)) return; // cancelled while watching.
            stateStore?.Update(s => s with { GaveUpAtUtc = null });
            await NotifyManualRecoveryAsync(token);
            // Loop back: the same supervisor resumes full crash-detect-and-restart monitoring.
        }
    }

    // Polls for the server to be back -- an admin pressing Start, or Automation's own Restart, are both just
    // "the server is running again" from here. v0.7.115.0 (deficiency report): "back" now means READY (the
    // process is there and its game port is up, the same test a normal start uses), not merely that a process
    // exists, so the "server is back up" notice can no longer arrive for a server still loading or stuck.
    private async Task<bool> WaitForManualRecoveryAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var status = await lifecycle.GetStatusAsync(token);
                if (status.Processes.Count > 0 && status.Ready) return true;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // An unreadable status must not stop watching; try again next tick.
            }
            try { await Task.Delay(options.PollInterval, token); }
            catch (OperationCanceledException) { return false; }
        }
        return false;
    }

    private async Task NotifyManualRecoveryAsync(CancellationToken token)
    {
        if (observer is null) return;
        try
        {
            await observer.OnEventAsync(new SupervisorEvent(SupervisorEventKind.ManualRecovery, 0, options.MaximumRestartAttempts,
                TimeSpan.Zero, options.RestartWindow, "PalServer is running again.", DateTimeOffset.UtcNow), token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Supervisor observer failed (ManualRecovery): {ex.Message}");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (loopCts is null) return;
        loopCts.Cancel();
        if (loopTask is not null)
        {
            try { await loopTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken); }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync(CancellationToken.None);
}
