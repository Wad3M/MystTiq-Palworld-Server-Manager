namespace PalworldManager.Models;

public sealed class CharacterResetOptions
{
    public bool RemovePlayerRegistration { get; set; } = true;
    public bool ClearRespawnReferences { get; set; } = true;
    public bool RemoveGuildReferences { get; set; } = true;
    public bool RemovePlayerSave { get; set; } = true;
    public bool ForceCreateOnNextLogin { get; set; } = true;
}

public sealed class CharacterResetPreview
{
    public string PlayerName { get; set; } = "";
    public string PlayerGuid { get; set; } = "";
    public string WorldPath { get; set; } = "";
    public string LevelSavePath { get; set; } = "";
    public string PlayerSavePath { get; set; } = "";
    public string CompanionSavePath { get; set; } = "";
    public bool CodecAvailable { get; set; }
    public bool ServerMustBeStopped { get; set; }
    public int ExactReferenceCount { get; set; }
    public long PlayerSaveSizeBytes { get; set; }
    public List<string> Identifiers { get; set; } = [];
    public List<string> Findings { get; set; } = [];
    public CharacterResetOptions Options { get; set; } = new();

    public bool CanApply => CodecAvailable && File.Exists(LevelSavePath) && File.Exists(PlayerSavePath) && !ServerMustBeStopped;
}

public sealed class CharacterResetResult
{
    public bool Success { get; set; }
    public string TransactionId { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public string ReportPath { get; set; } = "";
    public int ReferencesRemoved { get; set; }
    public bool PlayerSaveRemoved { get; set; }
    public bool CompanionSaveRemoved { get; set; }
    public bool VerificationPassed { get; set; }
    public List<string> Messages { get; set; } = [];
}
