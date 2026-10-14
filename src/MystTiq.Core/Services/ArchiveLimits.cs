// MystTiq v1.1.0.0: file reviewed for this release (2026-10-12).
using System.IO.Compression;

namespace MystTiq.Core.Services;

/// <summary>
/// v1.0.7.4 (roadmap Q-15): world import already refused archives over 20,000 entries or 8 GiB expanded. MOD installs
/// (whose download was capped at 512 MB but not what it expands to), MOD snapshot restores, backup restores and the
/// self-update had no limit, and nothing checked the disk had room first, so a hostile or broken archive could fill the
/// drive. Every archive MystTiq opens now goes through the same check, before anything is written.
/// </summary>
public static class ArchiveLimits
{
    public const int MaximumEntries = 20_000;
    public const long MaximumExpandedBytes = 8L * 1024 * 1024 * 1024;
    /// <summary>Room left free on the target drive beyond what the archive expands to.</summary>
    public const long FreeSpaceMargin = 256L * 1024 * 1024;

    /// <summary>Why this archive must not be extracted into <paramref name="destination"/>, or null when it can be.</summary>
    public static string? Check(ZipArchive archive, string destination, int maximumEntries = MaximumEntries, long maximumExpandedBytes = MaximumExpandedBytes)
    {
        if (archive.Entries.Count > maximumEntries)
            return $"The archive has {archive.Entries.Count:N0} entries, more than the {maximumEntries:N0} MystTiq accepts.";
        long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            expanded += entry.Length;
            if (expanded > maximumExpandedBytes)
                return $"The archive expands to more than {maximumExpandedBytes / (1024 * 1024 * 1024)} GiB, more than MystTiq accepts.";
        }
        var free = FreeBytes(destination);
        if (free is { } bytes && bytes < expanded + FreeSpaceMargin)
            return $"The archive expands to {Megabytes(expanded)} MB, but the drive has only {Megabytes(bytes)} MB free (MystTiq keeps {Megabytes(FreeSpaceMargin)} MB spare).";
        return null;
    }

    /// <summary>The same check for an archive on disk.</summary>
    public static string? CheckFile(string archivePath, string destination, int maximumEntries = MaximumEntries, long maximumExpandedBytes = MaximumExpandedBytes)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            return Check(archive, destination, maximumEntries, maximumExpandedBytes);
        }
        catch (InvalidDataException ex) { return $"The archive could not be read: {ex.Message}"; }
    }

    private static long? FreeBytes(string destination)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(destination));
            return string.IsNullOrEmpty(root) ? null : new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return null; }
    }

    private static long Megabytes(long bytes) => bytes / (1024 * 1024);
}
