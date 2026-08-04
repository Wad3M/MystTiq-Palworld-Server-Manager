namespace PalworldManager.Models;

public sealed class SaveInspectorSummary
{
    public string WorldPath { get; set; } = "";
    public string WorldId { get; set; } = "";
    public string LevelSavePath { get; set; } = "";
    public PalworldSaveHeader Header { get; set; } = new();
    public int PlayerSaveCount { get; set; }
    public int DerivedPlayerFileCount { get; set; }
    public int BackupFolderCount { get; set; }
    public bool HasLevelMeta { get; set; }
    public bool HasLocalData { get; set; }
    public bool HasWorldOption { get; set; }
    public bool CodecAvailable { get; set; }
    public string CodecStatus { get; set; } = "";
    public DateTime LastWriteUtc { get; set; }
    public bool IsBackup { get; set; }
    public bool IsDerived { get; set; }
    public bool IsRequired { get; set; }
    public bool IsOptional { get; set; }
    public long TotalWorldBytes { get; set; }
    public List<SaveInspectorFileRow> Files { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public int TotalFileCount => Files.Count;
    public int LiveFileCount => Files.Count(x => !x.IsBackup);
    public int BackupFileCount => Files.Count(x => x.IsBackup);
    public int RequiredFileCount => Files.Count(x => x.IsRequired);
    public int OptionalFileCount => Files.Count(x => x.IsOptional);
    public int UnknownFileCount => Files.Count(x => x.Category == "Other");
    public DateTime LatestFileWriteUtc => Files.Count == 0 ? default : Files.Max(x => x.LastWriteUtc);
    public DateTime OldestFileWriteUtc => Files.Count == 0 ? default : Files.Min(x => x.LastWriteUtc);
    public SaveInspectorFileRow? LargestFile => Files.OrderByDescending(x => x.SizeBytes).FirstOrDefault();
    public string LatestFileWriteDisplay => LatestFileWriteUtc == default ? "Unknown" : LatestFileWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string LargestFileDisplay => LargestFile is null ? "None" : $"{LargestFile.RelativePath} ({LargestFile.SizeDisplay})";

    public string ContainerDisplay => Header.Kind.ToString();
    public string SizeDisplay => FormatBytes(TotalWorldBytes);
    public string LevelSizeDisplay => FormatBytes(Header.Length);
    public string LastWriteDisplay => LastWriteUtc == default ? "Unknown" : LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string HealthDisplay => Warnings.Count == 0 ? "Ready to inspect" : $"{Warnings.Count} warning(s)";

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = Math.Max(0, bytes);
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.##} {units[unit]}";
    }
}

public sealed class SaveInspectorFileRow
{
    public string Name { get; set; } = "";
    public string RelativePath { get; set; } = "";
    public string Category { get; set; } = "";
    public string Status { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime LastWriteUtc { get; set; }
    public bool IsBackup { get; set; }
    public bool IsDerived { get; set; }
    public bool IsRequired { get; set; }
    public bool IsOptional { get; set; }
    public string SizeDisplay => FormatBytes(SizeBytes);
    public string LastWriteDisplay => LastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = Math.Max(0, bytes);
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1) { size /= 1024; unit++; }
        return $"{size:0.##} {units[unit]}";
    }
}

public sealed class SaveExplorerNode
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Detail { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public List<SaveExplorerNode> Children { get; set; } = [];
    public string Display => string.IsNullOrWhiteSpace(Detail) ? Name : $"{Name} — {Detail}";
}

public sealed class SaveHealthSummary
{
    public int Score { get; set; }
    public string Overall { get; set; } = "Unknown";
    public int HealthyCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Findings { get; set; } = [];
}

public sealed class SaveIntegrityRow
{
    public string Severity { get; set; } = "Info";
    public string Area { get; set; } = "";
    public string Finding { get; set; } = "";
    public string Recommendation { get; set; } = "";
}

public sealed class SaveRepairSuggestion
{
    public bool Selected { get; set; }
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Risk { get; set; } = "Low";
    public string State { get; set; } = "Preview only";
}
