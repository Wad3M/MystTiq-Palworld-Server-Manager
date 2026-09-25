using MystTiq.Core.Models;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.8.24.0: a server started through MystTiq gets its priority, eco mode and cores at once, instead of on the next
// automation tick (up to 15 s later). Every start and restart in one server's service graph goes through this wrapper:
// the API, automation, the Discord bot and mod safe start. The service supervisor's crash recovery (service-run) uses
// its own lifecycle and is picked up by the tick, as before. AfterStart is set once the policy service exists (it reads
// the lifecycle too); it never turns a successful start into a failure.
public sealed class PolicyApplyingLifecycle(IServerLifecycleService inner) : IServerLifecycleService
{
    public Func<CancellationToken, Task>? AfterStart { get; set; }

    public Task<ServerLifecycleSnapshot> GetStatusAsync(CancellationToken cancellationToken = default) => inner.GetStatusAsync(cancellationToken);

    public async Task<ServerLifecycleOperationResult> StartAsync(IReadOnlyList<string> serverArguments, TimeSpan startupTimeout, CancellationToken cancellationToken = default) =>
        await AfterAsync(await inner.StartAsync(serverArguments, startupTimeout, cancellationToken), cancellationToken);

    public Task<ServerLifecycleOperationResult> StopAsync(TimeSpan gracefulTimeout, CancellationToken cancellationToken = default) =>
        inner.StopAsync(gracefulTimeout, cancellationToken);

    public async Task<ServerLifecycleOperationResult> RestartAsync(IReadOnlyList<string> serverArguments, TimeSpan startupTimeout, TimeSpan gracefulTimeout, CancellationToken cancellationToken = default) =>
        await AfterAsync(await inner.RestartAsync(serverArguments, startupTimeout, gracefulTimeout, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<ServerInstanceInfo>> FindAllInstancesAsync(CancellationToken cancellationToken = default) => inner.FindAllInstancesAsync(cancellationToken);

    public Task<InstanceTerminationResult> TerminateUnmanagedInstanceAsync(int processId, CancellationToken cancellationToken = default) =>
        inner.TerminateUnmanagedInstanceAsync(processId, cancellationToken);

    private async Task<ServerLifecycleOperationResult> AfterAsync(ServerLifecycleOperationResult result, CancellationToken cancellationToken)
    {
        if (result.Success && AfterStart is { } hook)
        {
            try { await hook(cancellationToken); }
            catch (Exception ex) when (ex is not OperationCanceledException) { }
        }
        return result;
    }
}