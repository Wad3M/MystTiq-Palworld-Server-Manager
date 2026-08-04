using System;

namespace PalworldManager.Models;

public sealed class WorldToolsWorldRow
{
    public string WorldId { get; init; } = "";
    public string WorldPath { get; init; } = "";
    public bool IsActive { get; init; }
    public long SizeBytes { get; init; }
    public int FileCount { get; init; }
    public int PlayerCount { get; init; }
    public DateTime LastWriteTimeUtc { get; init; }
    public string Status { get; init; } = "Unknown";
    public string SizeDisplay => WorldToolsFormatting.FormatBytes(SizeBytes);
    public string LastWriteDisplay => LastWriteTimeUtc == DateTime.MinValue ? "—" : LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

public sealed class WorldToolsVerificationResult
{
    public bool IsValid { get; init; }
    public string WorldId { get; init; } = "";
    public string WorldPath { get; init; } = "";
    public int FileCount { get; init; }
    public int PlayerCount { get; init; }
    public long SizeBytes { get; init; }
    public string LevelSha256 { get; init; } = "";
    public string Summary { get; init; } = "";
}

public sealed class WorldToolsCleanupPreview
{
    public string WorldPath { get; init; } = "";
    public int FolderCount { get; init; }
    public int FileCount { get; init; }
    public long SizeBytes { get; init; }
    public string SizeDisplay => WorldToolsFormatting.FormatBytes(SizeBytes);
}

public static class WorldToolsFormatting
{
    public static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }
}
