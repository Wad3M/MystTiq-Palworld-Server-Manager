namespace MystTiq.Core.Operations;

// Returned by IOperationCoordinator.BeginAsync. Disposing releases the
// resource-key locks the operation held -- callers should dispose in a
// finally block so failed/interrupted operations always release locks,
// mirroring the try/finally cleanup pattern already used by every existing
// Apply flow (HeadlessWorldTransactionService, HeadlessGuildOwnershipService,
// HeadlessBaseOwnershipService).
public sealed class OperationHandle : IDisposable
{
    private readonly OperationCoordinator coordinator;
    private readonly IReadOnlyList<string> resourceKeys;
    private bool disposed;

    internal OperationHandle(OperationCoordinator coordinator, OperationId id, IReadOnlyList<string> resourceKeys)
    {
        this.coordinator = coordinator;
        Id = id;
        this.resourceKeys = resourceKeys;
    }

    public OperationId Id { get; }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        coordinator.ReleaseLocks(Id, resourceKeys);
    }
}
