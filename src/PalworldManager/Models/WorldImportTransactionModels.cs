namespace PalworldManager.Models;

public enum WorldImportTransactionState
{
    Created, ArchiveAnalyzed, Extracted, SaveDecoded, AwaitingPlayerMapping, AwaitingGuildRepair, AwaitingBaseRepair, StructurallyValidated, ReadyToActivate, Activated, Failed, RolledBack
}

public sealed class WorldImportTransaction
{
    public Guid TransactionId { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string ArchivePath { get; set; } = "";
    public string StagingRoot { get; set; } = "";
    public string WorkingWorldPath { get; set; } = "";
    public string OutputWorldPath { get; set; } = "";
    public string DecodedRoot => Path.Combine(StagingRoot, "Decoded");
    public string ReportsRoot => Path.Combine(StagingRoot, "Reports");
    public WorldImportTransactionState State { get; set; }
    public List<string> Diagnostics { get; set; } = [];
}

public sealed class WorldArchiveCandidate
{
    public string LevelSavePath { get; set; } = "";
    public string RootPrefix { get; set; } = "";
    public int Depth { get; set; }
    public bool IsBackup { get; set; }
    public bool HasPlayersDirectory { get; set; }
    public bool HasLevelMeta { get; set; }
    public int Score { get; set; }
}
