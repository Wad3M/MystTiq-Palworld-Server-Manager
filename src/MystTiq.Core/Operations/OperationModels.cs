using System.Text.Json.Serialization;

namespace MystTiq.Core.Operations;

// Kind-agnostic operation identity/record contracts. Promoted from the
// per-feature journal pattern each of HeadlessWorldTransactionService,
// HeadlessGuildOwnershipService and HeadlessBaseOwnershipService used to
// hand-roll independently (each writing its own HeadlessWorldTransactionJournal
// -shaped file). Same field shape, now shared through OperationCoordinator so
// every long-running/destructive action is visible and lockable in one place.

[JsonConverter(typeof(OperationIdJsonConverter))]
public readonly record struct OperationId(string Value)
{
    public static OperationId New() => new(Guid.NewGuid().ToString("N"));
    public override string ToString() => Value;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OperationPhase
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
    RolledBack = 5
}

// State is a free-form label (e.g. "PreviewAccepted", "SafetyBackupCreated", "Staged",
// "Verified", "Committed") rather than the coarser OperationPhase enum, preserving the
// granular per-milestone naming the three migrated services already used.
public sealed record OperationStage(string State, string Detail, DateTimeOffset TimestampUtc);

// Every operation that reaches this coordinator carries safety-backup + rollback bookkeeping
// (SafetyBackup, RolledBack on OperationRecord below) -- it IS a transaction, even though the
// three migrated services don't yet expose a dedicated commit/rollback API of their own. This
// derives a real, queryable transaction outcome from the same fields so future rollback-capable
// operations (and an eventual Operations Center UI) have one reusable contract instead of each
// re-deriving "did this actually commit" from Phase + RolledBack by hand.
public enum OperationTransactionState { Active, Committed, RolledBack, Failed }

public sealed class OperationRecord
{
    public required OperationId OperationId { get; init; }
    public required ServerProfileId ServerProfileId { get; init; }
    public required string Kind { get; init; }
    public required string Source { get; init; }
    public required IReadOnlyList<string> ResourceKeys { get; init; }
    public OperationPhase Phase { get; set; }
    public DateTimeOffset CreatedUtc { get; init; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public string? SafetyBackup { get; set; }
    public bool RolledBack { get; set; }
    public List<OperationStage> Stages { get; init; } = [];
    public string JournalPath { get; init; } = string.Empty;

    public OperationTransactionState TransactionState => Phase switch
    {
        OperationPhase.Completed => OperationTransactionState.Committed,
        OperationPhase.RolledBack => OperationTransactionState.RolledBack,
        OperationPhase.Failed or OperationPhase.Cancelled => RolledBack ? OperationTransactionState.RolledBack : OperationTransactionState.Failed,
        _ => OperationTransactionState.Active,
    };
}
