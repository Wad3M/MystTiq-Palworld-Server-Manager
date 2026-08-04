namespace PalworldManager.Models;

public enum OwnershipOperationType
{
    TransferOwnership,
    DeleteBaseAndOwnedObjects
}

public sealed class OwnershipPreview
{
    public string WorldPath { get; set; } = "";
    public string LevelSavePath { get; set; } = "";
    public string SourceHash { get; set; } = "";
    public BaseManagerRow Base { get; set; } = new();
    public OwnershipOperationType Operation { get; set; }
    public string TargetGuildId { get; set; } = "";
    public bool CodecAvailable { get; set; }
    public bool ServerMustBeStopped { get; set; }
    public int MatchedScopeCount { get; set; }
    public int BaseReferenceCount { get; set; }
    public int PalboxReferenceCount { get; set; }
    public int GuildReferenceCount { get; set; }
    public Dictionary<string, int> Categories { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> SamplePaths { get; set; } = [];
    public List<string> Findings { get; set; } = [];
    public bool CanApply => CodecAvailable && !ServerMustBeStopped && File.Exists(LevelSavePath) && MatchedScopeCount > 0 &&
        (Operation != OwnershipOperationType.TransferOwnership || !string.IsNullOrWhiteSpace(TargetGuildId));
}

public sealed class OwnershipTransactionResult
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public string ReportPath { get; set; } = "";
    public int ScopesChanged { get; set; }
    public int ValuesChanged { get; set; }
    public bool VerificationPassed { get; set; }
    public List<string> Messages { get; set; } = [];
}
