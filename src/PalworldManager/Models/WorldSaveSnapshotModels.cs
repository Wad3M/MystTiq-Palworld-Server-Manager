namespace PalworldManager.Models;

public sealed record WorldSaveSnapshot(
    string SourcePath,
    string SnapshotPath,
    DateTime SourceWriteUtc,
    long SourceLength,
    int AttemptCount,
    bool RequiredRetry);
