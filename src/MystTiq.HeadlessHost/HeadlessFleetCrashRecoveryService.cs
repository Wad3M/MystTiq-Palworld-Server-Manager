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
public sealed class HeadlessFleetCrashRecoveryService : IAsyncDisposable
{
    private readonly HeadlessSupervisor supervisor;

    private CancellationTokenSource? loopCts;
    private Task? loopTask;

    public HeadlessFleetCrashRecoveryService(
        IServerLifecycleService lifecycle,
        HeadlessSupervisorOptions options,
        IReadOnlyList<string> serverArguments)
    {
        supervisor = new HeadlessSupervisor(lifecycle, options, serverArguments);
    }

    public Task StartAsync(CancellationToken hostShutdownToken)
    {
        loopCts = CancellationTokenSource.CreateLinkedTokenSource(hostShutdownToken);
        loopTask = Task.Run(() => supervisor.RunCrashRecoveryLoopAsync(loopCts.Token));
        return Task.CompletedTask;
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
