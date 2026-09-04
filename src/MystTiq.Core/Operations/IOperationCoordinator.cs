namespace MystTiq.Core.Operations;

// A resource key (e.g. "world-mutation") held by one in-flight operation blocks
// BeginAsync for any other operation requesting that same key -- reject-if-
// conflicting rather than queue-and-wait. Deliberately not a full priority
// queue/dependency graph; see the Operation Platform doc under
// docs/architecture/ for what's covered by this pass versus deferred to a
// later revision.
public interface IOperationCoordinator
{
    Task<OperationHandle> BeginAsync(ServerProfileId profile, string kind, string source, IReadOnlyList<string> resourceKeys, CancellationToken cancellationToken);
    void Advance(OperationId id, string state, string detail);
    void SetSafetyBackup(OperationId id, string fileName);
    void Complete(OperationId id, string detail);
    void Fail(OperationId id, string detail, bool rolledBack = false);
    IReadOnlyList<OperationRecord> ListRecent(int max = 50);
    IReadOnlyList<OperationRecord> ListRecent(ServerProfileId? profile, int max = 50);
    OperationRecord? Find(OperationId id);
}
