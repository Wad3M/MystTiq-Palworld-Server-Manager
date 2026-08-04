namespace PalworldManager.Models;

public enum WorldArchiveLayout { Unknown, FlatWorld, WrappedWorld, BackupTree }
public enum WorldOptionImportMode { Quarantine, Preserve, Exclude }
public enum WorldImportMode { CreateNew, ReplaceActive }

public sealed class WorldImportEntry
{
    public string ArchivePath { get; set; } = "";
    public long Size { get; set; }
    public bool Allowed { get; set; }
    public string Status { get; set; } = "";
}

public sealed class WorldImportScanResult
{
    public string ArchivePath { get; set; } = "";
    public string ArchiveSha256 { get; set; } = "";
    public WorldArchiveLayout Layout { get; set; }
    public string RootPrefix { get; set; } = "";
    public bool HasLevelSave { get; set; }
    public bool HasLevelMeta { get; set; }
    public bool HasLocalData { get; set; }
    public bool HasWorldOption { get; set; }
    public int PlayerSaveCount { get; set; }
    public int DerivedPlayerSaveCount { get; set; }
    public int BackupEntryCount { get; set; }
    public int InstallableEntryCount { get; set; }
    public long TotalUncompressedBytes { get; set; }
    public List<WorldImportEntry> Entries { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public bool IsValid => HasLevelSave && Entries.All(x => x.Allowed);
    public string WorldProfile => PlayerSaveCount <= 1 && BackupEntryCount == 0
        ? "Fresh / Minimal"
        : PlayerSaveCount <= 1
            ? "Fresh world with embedded backups"
            : "Migrated / Multiplayer";
    public string Readiness => IsValid
        ? HasLevelMeta ? "Ready for staging" : "Ready with advisory"
        : "Blocked";
    public string Summary => $"{Layout} • Level.sav: {(HasLevelSave ? "Found" : "Missing")} • Players: {PlayerSaveCount} • {WorldProfile} • {FormatBytes(TotalUncompressedBytes)}";
    private static string FormatBytes(long value) => value >= 1024L*1024L ? $"{value/(1024d*1024d):0.00} MB" : $"{value/1024d:0.0} KB";
}

public sealed class WorldImportPlan
{
    public string ArchivePath { get; set; } = "";
    public string DestinationWorldId { get; set; } = "";
    public WorldImportMode ImportMode { get; set; } = WorldImportMode.CreateNew;
    public WorldOptionImportMode WorldOptionMode { get; set; } = WorldOptionImportMode.Quarantine;
    public bool CreateBackup { get; set; } = true;
    public bool ValidateAfterExtraction { get; set; } = true;
    public bool OpenGuildRecovery { get; set; } = true;
}

public sealed class WorldImportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public string DestinationWorldPath { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public string ManifestPath { get; set; } = "";
    public string QuarantinePath { get; set; } = "";
}

public sealed class WorldImportHistoryRow
{
    public DateTime ImportedUtc { get; set; }
    public string SourceArchive { get; set; } = "";
    public string WorldId { get; set; } = "";
    public string Status { get; set; } = "";
    public string BackupPath { get; set; } = "";
    public string ManifestPath { get; set; } = "";
}
