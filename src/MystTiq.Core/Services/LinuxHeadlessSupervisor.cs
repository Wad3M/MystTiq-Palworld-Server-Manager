using System.Runtime.Versioning;
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

[SupportedOSPlatform("linux")]
public sealed class LinuxHeadlessSupervisor
{
    private readonly IServerLifecycleService lifecycle;
    private readonly LinuxServiceSupervisorOptions options;
    private readonly IReadOnlyList<string> serverArguments;
    private readonly Queue<DateTimeOffset> restartHistory = new();

    public LinuxHeadlessSupervisor(
        IServerLifecycleService lifecycle,
        LinuxServiceSupervisorOptions options,
        IReadOnlyList<string> serverArguments)
    {
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.serverArguments = serverArguments ?? throw new ArgumentNullException(nameof(serverArguments));
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("MystTiq Linux service supervisor starting.");

        var initial = await lifecycle.GetStatusAsync(cancellationToken);
        if (initial.Processes.Count == 0)
        {
            var start = await lifecycle.StartAsync(serverArguments, options.StartupTimeout, cancellationToken);
            if (!start.Success && start.ExitCode != HeadlessExitCode.AlreadyRunning)
            {
                Console.Error.WriteLine($"Initial PalServer start failed: {start.ExitCode} — {start.Message}");
                return (int)start.ExitCode;
            }
        }
        else
        {
            Console.WriteLine($"Adopted existing PalServer PID {initial.NativeProcessId?.ToString() ?? "unknown"}.");
        }

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
                return (int)HeadlessExitCode.CrashDetected;
            }

            restartHistory.Enqueue(DateTimeOffset.UtcNow);
            Console.Error.WriteLine(
                $"PalServer crash/disappearance detected. Recovery attempt {restartHistory.Count}/{options.MaximumRestartAttempts} after {options.RestartBackoff}.");

            await Task.Delay(options.RestartBackoff, cancellationToken);
            var restart = await lifecycle.StartAsync(serverArguments, options.StartupTimeout, cancellationToken);

            if (!restart.Success && restart.ExitCode != HeadlessExitCode.AlreadyRunning)
                Console.Error.WriteLine($"Automatic recovery failed: {restart.ExitCode} — {restart.Message}");
            else
                Console.WriteLine($"PalServer recovery succeeded; PID {restart.Snapshot.NativeProcessId?.ToString() ?? "unknown"}.");
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

    private bool CanRestart(DateTimeOffset now)
    {
        while (restartHistory.Count > 0 && now - restartHistory.Peek() > options.RestartWindow)
            restartHistory.Dequeue();

        return restartHistory.Count < options.MaximumRestartAttempts;
    }
}
