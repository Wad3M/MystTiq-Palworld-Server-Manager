namespace PalworldManager.Models;

public enum WorldTransactionState
{
    Prepared,
    BackedUp,
    Staged,
    Encoded,
    Verified,
    Committed,
    RolledBack,
    Failed
}

public sealed class WorldTransactionStage
{
    public WorldTransactionState State { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Message { get; set; } = "";
}

public sealed class WorldTransactionJournal
{
    public string TransactionId { get; set; } = "";
    public string Operation { get; set; } = "";
    public string WorldPath { get; set; } = "";
    public string TargetId { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public string ResultHash { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public string ReportPath { get; set; } = "";
    public WorldTransactionState State { get; set; } = WorldTransactionState.Prepared;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public List<WorldTransactionStage> Stages { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}
