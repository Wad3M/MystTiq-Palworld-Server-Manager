using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class WorldToolsService
{
    private readonly AppSettings settings;
    public WorldToolsService(AppSettings settings) => this.settings = settings;

    public IReadOnlyList<WorldToolsWorldRow> DiscoverWorlds()
    {
        var root = Path.Combine(settings.SaveRoot, "0");
        if (!Directory.Exists(root)) return [];
        var active = FindConfiguredWorldId();
        return Directory.EnumerateDirectories(root)
            .Select(path => BuildRow(path, active))
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.LastWriteTimeUtc)
            .ToList();
    }

    public string? FindActiveWorldPath()
    {
        var root = Path.Combine(settings.SaveRoot, "0");
        if (!Directory.Exists(root)) return null;
        var configured = FindConfiguredWorldId();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var path = Path.Combine(root, configured);
            if (File.Exists(Path.Combine(path, "Level.sav"))) return path;
        }
        return Directory.EnumerateDirectories(root)
            .Where(p => File.Exists(Path.Combine(p, "Level.sav")))
            .OrderByDescending(p => File.GetLastWriteTimeUtc(Path.Combine(p, "Level.sav")))
            .FirstOrDefault();
    }

    public WorldToolsVerificationResult Verify(string worldPath)
    {
        EnsureWorld(worldPath);
        var level = Path.Combine(worldPath, "Level.sav");
        var meta = Path.Combine(worldPath, "LevelMeta.sav");
        var files = Directory.EnumerateFiles(worldPath, "*", SearchOption.AllDirectories).ToList();
        var playerDir = Path.Combine(worldPath, "Players");
        var playerCount = Directory.Exists(playerDir)
            ? Directory.EnumerateFiles(playerDir, "*.sav", SearchOption.TopDirectoryOnly).Count(p => !p.EndsWith("_dps.sav", StringComparison.OrdinalIgnoreCase))
            : 0;
        var missing = new List<string>();
        if (!File.Exists(level)) missing.Add("Level.sav");
        if (!File.Exists(meta)) missing.Add("LevelMeta.sav");
        if (new FileInfo(level).Length < 16) missing.Add("Level.sav is unexpectedly small");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(level)));
        var size = files.Sum(f => new FileInfo(f).Length);
        return new WorldToolsVerificationResult
        {
            IsValid = missing.Count == 0,
            WorldId = Path.GetFileName(worldPath.TrimEnd(Path.DirectorySeparatorChar)),
            WorldPath = worldPath,
            FileCount = files.Count,
            PlayerCount = playerCount,
            SizeBytes = size,
            LevelSha256 = hash,
            Summary = missing.Count == 0
                ? $"World structure passed validation. {files.Count} file(s), {playerCount} player save(s), {WorldToolsFormatting.FormatBytes(size)}."
                : "Validation failed: " + string.Join(", ", missing)
        };
    }

    public string ExportZip(string worldPath, string destinationZip)
    {
        EnsureWorld(worldPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationZip) ?? ".");
        if (File.Exists(destinationZip)) File.Delete(destinationZip);
        ZipFile.CreateFromDirectory(worldPath, destinationZip, CompressionLevel.Optimal, false);
        using var archive = ZipFile.OpenRead(destinationZip);
        if (!archive.Entries.Any(e => e.FullName.Equals("Level.sav", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Export verification failed: Level.sav is missing from the ZIP.");
        return destinationZip;
    }

    public string Duplicate(string worldPath, string newWorldId)
    {
        EnsureWorld(worldPath);
        ValidateWorldId(newWorldId);
        var destination = Path.Combine(Path.GetDirectoryName(worldPath)!, newWorldId);
        if (Directory.Exists(destination)) throw new IOException("A world with that ID already exists.");
        CopyDirectory(worldPath, destination);
        Verify(destination);
        return destination;
    }

    public string Rename(string worldPath, string newWorldId)
    {
        EnsureWorld(worldPath);
        ValidateWorldId(newWorldId);
        var oldId = Path.GetFileName(worldPath.TrimEnd(Path.DirectorySeparatorChar));
        var destination = Path.Combine(Path.GetDirectoryName(worldPath)!, newWorldId);
        if (Directory.Exists(destination)) throw new IOException("A world with that ID already exists.");
        Directory.Move(worldPath, destination);
        if (string.Equals(FindConfiguredWorldId(), oldId, StringComparison.OrdinalIgnoreCase))
            SetConfiguredWorldId(newWorldId);
        return destination;
    }

    public string Archive(string worldPath)
    {
        EnsureWorld(worldPath);
        var archiveRoot = Path.Combine(settings.BackupRoot, "WorldArchives");
        Directory.CreateDirectory(archiveRoot);
        var id = Path.GetFileName(worldPath.TrimEnd(Path.DirectorySeparatorChar));
        var zip = Path.Combine(archiveRoot, $"{id}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        return ExportZip(worldPath, zip);
    }

    public string CreateSafetyBackup(string worldPath, string reason)
    {
        EnsureWorld(worldPath);
        var root = Path.Combine(settings.BackupRoot, "WorldTools", DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Sanitize(reason));
        Directory.CreateDirectory(root);
        var destination = Path.Combine(root, Path.GetFileName(worldPath.TrimEnd(Path.DirectorySeparatorChar)));
        CopyDirectory(worldPath, destination);
        Verify(destination);
        return destination;
    }

    public WorldToolsCleanupPreview PreviewInternalBackupCleanup(string worldPath)
    {
        EnsureWorld(worldPath);
        var backup = Path.Combine(worldPath, "backup");
        if (!Directory.Exists(backup)) return new WorldToolsCleanupPreview { WorldPath = worldPath };
        var files = Directory.EnumerateFiles(backup, "*", SearchOption.AllDirectories).ToList();
        return new WorldToolsCleanupPreview
        {
            WorldPath = worldPath,
            FolderCount = Directory.EnumerateDirectories(backup, "*", SearchOption.AllDirectories).Count() + 1,
            FileCount = files.Count,
            SizeBytes = files.Sum(f => new FileInfo(f).Length)
        };
    }

    public void CleanInternalBackups(string worldPath)
    {
        EnsureWorld(worldPath);
        var backup = Path.Combine(worldPath, "backup");
        if (Directory.Exists(backup)) Directory.Delete(backup, true);
    }

    private WorldToolsWorldRow BuildRow(string path, string? active)
    {
        var id = Path.GetFileName(path);
        var files = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).ToList();
        var playerDir = Path.Combine(path, "Players");
        var players = Directory.Exists(playerDir) ? Directory.EnumerateFiles(playerDir, "*.sav").Count(p => !p.EndsWith("_dps.sav", StringComparison.OrdinalIgnoreCase)) : 0;
        var level = Path.Combine(path, "Level.sav");
        return new WorldToolsWorldRow
        {
            WorldId = id,
            WorldPath = path,
            IsActive = string.Equals(id, active, StringComparison.OrdinalIgnoreCase),
            FileCount = files.Count,
            PlayerCount = players,
            SizeBytes = files.Sum(f => new FileInfo(f).Length),
            LastWriteTimeUtc = File.Exists(level) ? File.GetLastWriteTimeUtc(level) : Directory.GetLastWriteTimeUtc(path),
            Status = File.Exists(level) && File.Exists(Path.Combine(path, "LevelMeta.sav")) ? "Ready" : "Incomplete"
        };
    }

    private string? FindConfiguredWorldId()
    {
        var file = Path.Combine(settings.ServerRoot, "Pal", "Saved", "Config", "WindowsServer", "GameUserSettings.ini");
        if (!File.Exists(file)) return null;
        var match = Regex.Match(File.ReadAllText(file), @"(?im)^\s*DedicatedServerName\s*=\s*(.+?)\s*$");
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private void SetConfiguredWorldId(string worldId)
    {
        var dir = Path.Combine(settings.ServerRoot, "Pal", "Saved", "Config", "WindowsServer");
        var file = Path.Combine(dir, "GameUserSettings.ini");
        Directory.CreateDirectory(dir);
        var text = File.Exists(file) ? File.ReadAllText(file) : "[/Script/Pal.PalGameLocalSettings]" + Environment.NewLine;
        if (File.Exists(file)) File.Copy(file, file + ".myst-worldtools-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bak", true);
        var pattern = @"(?im)^\s*DedicatedServerName\s*=.*$";
        text = Regex.IsMatch(text, pattern) ? Regex.Replace(text, pattern, "DedicatedServerName=" + worldId) : text.TrimEnd() + Environment.NewLine + "DedicatedServerName=" + worldId + Environment.NewLine;
        File.WriteAllText(file, text, new UTF8Encoding(false));
    }

    private static void EnsureWorld(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) throw new DirectoryNotFoundException("Select a valid world folder.");
        if (!File.Exists(Path.Combine(path, "Level.sav"))) throw new InvalidDataException("The selected folder does not contain Level.sav.");
    }

    private static void ValidateWorldId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Enter a world ID.");
        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.Contains(Path.DirectorySeparatorChar) || value.Contains(Path.AltDirectorySeparatorChar))
            throw new ArgumentException("The world ID contains invalid characters.");
    }

    private static string Sanitize(string value) => Regex.Replace(value, @"[^A-Za-z0-9_-]+", "_");

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }
}
