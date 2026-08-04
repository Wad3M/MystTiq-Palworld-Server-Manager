namespace PalworldManager.Services.Infrastructure;

public enum OperationState
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

public sealed record OperationProgress(
    string OperationKey,
    string OperationName,
    string Step,
    int? Percent,
    OperationState State,
    DateTime StartedUtc,
    DateTime UpdatedUtc,
    TimeSpan Elapsed,
    bool CanCancel,
    string? Error = null);
