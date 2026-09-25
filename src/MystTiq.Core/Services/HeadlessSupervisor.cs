using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

// v0.6.3.0: platform-neutral -- was LinuxHeadlessSupervisor, but the implementation only ever used
// the shared IServerLifecycleService interface and platform-neutral models, never anything
// Linux-specific. Both Windows's and Linux's `service-run` CLI branches construct this same class,
// which is what actually makes RecoveryBackoffSeconds/MaximumRecoveryAttempts/RecoveryWindowSeconds
// real on Windows for the first time -- Windows previously had no auto-restart-on-crash loop at all.
public sealed class HeadlessSupervisor
{
    private readonly IServerLifecycleService lifecycle;
    private readonly HeadlessSupervisorOptions options;
    private readonly IReadOnlyList<string> serverArguments;
    private readonly Queue<DateTimeOffset> restartHistory = new();
    private readonly ISupervisorObserver? observer;
    // v0.7.115.0: optional (api-run passes one; service-run does not), so the restart window survives a
    // MystTiq restart instead of resetting to zero attempts every time.
    private readonly SupervisorRecoveryStateStore? stateStore;

    public HeadlessSupervisor(
        IServerLifecycleService lifecycle,
        HeadlessSupervisorOptions options,
        IReadOnlyList<string> serverArguments,
        ISupervisorObserver? observer = null,
        SupervisorRecoveryStateStore? stateStore = null)
    {
        this.observer = observer;
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.serverArguments = serverArguments ?? throw new ArgumentNullException(nameof(serverArguments));
        this.stateStore = stateStore;
        if (stateStore is not null)
            foreach (var attempt in stateStore.Read().RestartHistory.OrderBy(t => t)) restartHistory.Enqueue(attempt);
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("MystTiq service supervisor starting.");

        var initial = await lifecycle.GetStatusAsync(cancellationToken);
        var ready = initial.Ready;
        if (initial.Processes.Count == 0)
        {
            var start = await lifecycle.StartAsync(serverArguments, options.StartupTimeout, cancellationToken);
            if (!start.Success && start.ExitCode != HeadlessExitCode.AlreadyRunning)
            {
                Console.Error.WriteLine($"Initial PalServer start failed: {start.ExitCode} — {start.Message}");
                return (int)start.ExitCode;
            }
            ready = start.Snapshot.Ready;
        }
        else
        {
            Console.WriteLine($"Adopted existing PalServer PID {initial.NativeProcessId?.ToString() ?? "unknown"}.");
        }

        // v0.8.2.0: service-run exits after giving up and the OS service manager starts it again, which starts the
        // server again above. If that recorded give-up is still standing and the server is now up and READY, the outage
        // it announced is over: say so (unpinning the DOWN notice through the observer) and clear it.
        if (stateStore?.Read().GaveUpAtUtc is not null && (ready || await WaitUntilReadyAsync(cancellationToken)))
        {
            stateStore.Update(s => s with { GaveUpAtUtc = null });
            await NotifyAsync(SupervisorEventKind.ManualRecovery, 0, "PalServer is running again after the MystTiq service restarted.", cancellationToken);
        }

        return await RunCrashRecoveryLoopAsync(cancellationToken);
    }

    // v0.6.13.0: extracted so api-run (fleet-wide, every profile including non-default) can reuse
    // the exact same crash-detect/backoff/window logic without RunAsync's "ensure started on
    // launch" preamble above -- appropriate for service-run's unattended-boot semantics, wrong for
    // api-run, where a profile nobody has started yet is not a crash. Behavior-identical to the
    // pre-v0.6.13.0 RunAsync loop for every existing service-run caller.
    public async Task<int> RunCrashRecoveryLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(options.PollInterval, cancellationToken);
            var status = await lifecycle.GetStatusAsync(cancellationToken);

            if (status.Processes.Count > 0)
                continue;

            if (!status.CrashDetected && status.Phase == ServerLifecyclePhase.Stopped)
            {
                Console.WriteLine("PalServer is stopped by recorded intent; supervisor will not auto-restart it.");
                continue;
            }

            if (!CanRestart(DateTimeOffset.UtcNow))
            {
                Console.Error.WriteLine(
                    $"Automatic recovery suppressed after {options.MaximumRestartAttempts} attempts inside {options.RestartWindow}.");
                // v0.8.2.0: recorded here, where the give-up happens, so both callers see it (api-run's wrapper resumes
                // watching from it; service-run's next start announces the recovery from it).
                stateStore?.Update(s => s with { GaveUpAtUtc = DateTimeOffset.UtcNow });
                await NotifyAsync(SupervisorEventKind.RecoverySuppressed, restartHistory.Count,
                    $"Automatic recovery gave up after {options.MaximumRestartAttempts} restart attempts inside {options.RestartWindow}.", cancellationToken);
                return (int)HeadlessExitCode.CrashDetected;
            }

            restartHistory.Enqueue(DateTimeOffset.UtcNow);
            SaveHistory();
            Console.Error.WriteLine(
                $"PalServer crash/disappearance detected. Recovery attempt {restartHistory.Count}/{options.MaximumRestartAttempts} after {options.RestartBackoff}.");
            await NotifyAsync(SupervisorEventKind.CrashDetected, restartHistory.Count, status.Detail ?? string.Empty, cancellationToken);

            await Task.Delay(options.RestartBackoff, cancellationToken);
            var restart = await lifecycle.StartAsync(serverArguments, options.StartupTimeout, cancellationToken);

            if (!restart.Success && restart.ExitCode != HeadlessExitCode.AlreadyRunning)
            {
                Console.Error.WriteLine($"Automatic recovery failed: {restart.ExitCode} — {restart.Message}");
                await NotifyAsync(SupervisorEventKind.RecoveryFailed, restartHistory.Count, $"{restart.ExitCode}: {restart.Message}", cancellationToken);
            }
            // v0.7.115.0 (deficiency report): "AlreadyRunning" (a process appeared while backing off) used to count
            // as recovered on sight. Recovered now means ready: the game port is up, the same test a normal start
            // uses, waited for up to the startup timeout.
            else if (!restart.Snapshot.Ready && !await WaitUntilReadyAsync(cancellationToken))
            {
                Console.Error.WriteLine("Automatic recovery failed: PalServer is running but never became ready.");
                await NotifyAsync(SupervisorEventKind.RecoveryFailed, restartHistory.Count,
                    $"NotReady: a PalServer process is running, but its game port did not come up within {options.StartupTimeout}.", cancellationToken);
            }
            else
            {
                Console.WriteLine($"PalServer recovery succeeded; PID {restart.Snapshot.NativeProcessId?.ToString() ?? "unknown"}.");
                await NotifyAsync(SupervisorEventKind.RecoverySucceeded, restartHistory.Count,
                    $"PalServer is running again (PID {restart.Snapshot.NativeProcessId?.ToString() ?? "unknown"}).", cancellationToken);
            }
        }

        return 0;
    }

    public async Task StopManagedServerAsync(CancellationToken cancellationToken)
    {
        var status = await lifecycle.GetStatusAsync(cancellationToken);
        if (status.Processes.Count == 0)
            return;

        Console.WriteLine("MystTiq service stopping; requesting graceful PalServer shutdown.");
        var result = await lifecycle.StopAsync(options.StopTimeout, cancellationToken);
        Console.WriteLine($"PalServer shutdown result: {result.ExitCode} — {result.Message}");
    }

    // An observer must never be able to stop crash recovery, so its failures are logged and dropped.
    private async Task NotifyAsync(SupervisorEventKind kind, int attempt, string detail, CancellationToken cancellationToken)
    {
        if (observer is null) return;
        try
        {
            await observer.OnEventAsync(new SupervisorEvent(kind, attempt, options.MaximumRestartAttempts,
                options.RestartBackoff, options.RestartWindow, detail, DateTimeOffset.UtcNow), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Supervisor observer failed ({kind}): {ex.Message}");
        }
    }

    private bool CanRestart(DateTimeOffset now)
    {
        var aged = false;
        while (restartHistory.Count > 0 && now - restartHistory.Peek() > options.RestartWindow)
        {
            restartHistory.Dequeue();
            aged = true;
        }
        if (aged) SaveHistory();

        return restartHistory.Count < options.MaximumRestartAttempts;
    }

    private void SaveHistory() => stateStore?.Update(s => s with { RestartHistory = restartHistory.ToArray() });

    // True once the lifecycle reports the server ready (game port up), false if the startup timeout passes first.
    public async Task<bool> WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + options.StartupTimeout;
        var step = options.PollInterval < TimeSpan.FromSeconds(1) ? options.PollInterval : TimeSpan.FromSeconds(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var status = await lifecycle.GetStatusAsync(cancellationToken);
            if (status.Processes.Count > 0 && status.Ready) return true;
            await Task.Delay(step, cancellationToken);
        }
        return false;
    }
}
