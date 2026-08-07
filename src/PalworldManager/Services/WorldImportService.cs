using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class WorldImportService
{
    private readonly SafeFileSystemService fileSystem = new();
    private readonly PlayerSaveDiscoveryService playerSaveDiscovery = new();
    private static readonly HashSet<string> AllowedRootFiles = new(StringComparer.OrdinalIgnoreCase)
    { "Level.sav", "LevelMeta.sav", "LocalData.sav", "WorldOption.sav" };
    private static readonly string[] BlockedExtensions = [".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi", ".scr", ".com"];
    private const long MaxEntryBytes = 2L * 1024 * 1024 * 1024;
    private const long MaxArchiveBytes = 8L * 1024 * 1024 * 1024;
    private readonly AppSettings settings;
    public WorldImportService(AppSettings settings) => this.settings = settings;
    public string ImportsRoot => Path.Combine(settings.BackupRoot, "WorldImports");
    public string StagingRoot => Path.Combine(ImportsRoot, "Staging");
    public string HistoryRoot => Path.Combine(ImportsRoot, "History");

    public WorldImportScanResult Scan(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
            throw new FileNotFoundException("World archive was not found.", archivePath);

        var result = new WorldImportScanResult
        {
            ArchivePath = archivePath,
            ArchiveSha256 = HashFile(archivePath)
        };

        using var zip = ZipFile.OpenRead(archivePath);
        var fileEntries = zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToList();
        var normalized = fileEntries.Select(e => Normalize(e.FullName)).ToList();
        var rootDetection = DetectWorldRoot(normalized);
        result.RootPrefix = rootDetection.RootPrefix;
        result.Layout = rootDetection.Layout;

        if (!string.IsNullOrWhiteSpace(rootDetection.Warning))
            result.Warnings.Add(rootDetection.Warning);

        foreach (var entry in fileEntries)
        {
            var path = Normalize(entry.FullName);
            var safePath = IsSafePath(path);
            var oversized = entry.Length > MaxEntryBytes;
            var blockedExtension = BlockedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
            var underSelectedRoot = string.IsNullOrEmpty(result.RootPrefix)
                || path.StartsWith(result.RootPrefix, StringComparison.OrdinalIgnoreCase);
            var relative = underSelectedRoot ? RemovePrefix(path, result.RootPrefix) : path;
            var ignoredBackup = underSelectedRoot && IsBackupPath(relative);
            var installable = underSelectedRoot && !ignoredBackup && IsAllowed(relative);

            var blocked = !safePath || oversized || blockedExtension;
            var status = blocked
                ? (!safePath ? "Blocked: unsafe path" : oversized ? "Blocked: file too large" : "Blocked: executable/script")
                : installable
                    ? "Allowed"
                    : ignoredBackup
                        ? "Ignored: backup data"
                        : underSelectedRoot
                            ? "Ignored: unsupported file"
                            : "Ignored: outside selected world";

            result.Entries.Add(new WorldImportEntry
            {
                ArchivePath = path,
                Size = entry.Length,
                Allowed = !blocked,
                Status = status
            });

            result.TotalUncompressedBytes += entry.Length;
            if (ignoredBackup) result.BackupEntryCount++;
            if (installable) result.InstallableEntryCount++;
            if (!installable) continue;

            if (relative.Equals("Level.sav", StringComparison.OrdinalIgnoreCase)) result.HasLevelSave = true;
            else if (relative.Equals("LevelMeta.sav", StringComparison.OrdinalIgnoreCase)) result.HasLevelMeta = true;
            else if (relative.Equals("LocalData.sav", StringComparison.OrdinalIgnoreCase)) result.HasLocalData = true;
            else if (relative.Equals("WorldOption.sav", StringComparison.OrdinalIgnoreCase)) result.HasWorldOption = true;
            else if (relative.StartsWith("Players/", StringComparison.OrdinalIgnoreCase)
                     && relative.EndsWith(".sav", StringComparison.OrdinalIgnoreCase))
            {
                if (relative.EndsWith("_dps.sav", StringComparison.OrdinalIgnoreCase)) result.DerivedPlayerSaveCount++;
                else result.PlayerSaveCount++;
            }
        }

        if (result.TotalUncompressedBytes > MaxArchiveBytes)
            result.Warnings.Add("The archive exceeds the safe extraction limit.");
        if (!result.HasLevelSave)
            result.Warnings.Add("A primary Level.sav could not be identified.");
        if (result.Entries.Any(x => !x.Allowed))
            result.Warnings.Add("One or more unsafe entries were blocked by the safe-import policy.");
        if (result.Entries.Any(x => x.Status == "Ignored: backup data"))
            result.Warnings.Add("Backup copies were detected and will be ignored during installation.");
        if (result.HasWorldOption)
            result.Warnings.Add("WorldOption.sav can override PalWorldSettings.ini. Quarantine is recommended.");

        return result;
    }

    public string Stage(WorldImportScanResult scan)
    {
        if (!scan.IsValid || scan.TotalUncompressedBytes > MaxArchiveBytes) throw new InvalidOperationException("The archive failed safe-import validation.");
        Directory.CreateDirectory(StagingRoot);
        var stage = Path.Combine(StagingRoot, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(stage);
        using var zip = ZipFile.OpenRead(scan.ArchivePath);
        foreach (var entry in zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)))
        {
            var relative = RemovePrefix(Normalize(entry.FullName), scan.RootPrefix);
            if (!IsAllowed(relative)) continue;
            var target = Path.GetFullPath(Path.Combine(stage, relative.Replace('/', Path.DirectorySeparatorChar)));
            var stageFull = Path.GetFullPath(stage) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(stageFull, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive path escaped the staging folder.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, false);
        }
        ValidateStagedWorld(stage, scan);
        File.WriteAllText(Path.Combine(stage, "myst-stage.json"), JsonSerializer.Serialize(new { scan.ArchivePath, scan.ArchiveSha256, scan.Layout, scan.PlayerSaveCount, StagedUtc = DateTime.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
        return stage;
    }

    public WorldImportResult Install(WorldImportScanResult scan, WorldImportPlan plan, bool serverRunning)
    {
        if (serverRunning) throw new InvalidOperationException("Stop PalServer before importing a world.");
        var stage = Stage(scan);
        var saveZero = Path.Combine(settings.SaveRoot, "0");
        Directory.CreateDirectory(saveZero);
        var worldId = NormalizeWorldId(plan.DestinationWorldId);
        if (string.IsNullOrWhiteSpace(worldId)) worldId = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var destination = Path.Combine(saveZero, worldId);
        if (Directory.Exists(destination)) throw new IOException("The destination world folder already exists: " + destination);
        var backup = plan.CreateBackup ? BackupSaveRoot() : "";
        var quarantine = "";
        try
        {
            Directory.CreateDirectory(destination);
            CopyDirectory(stage, destination, f => !f.EndsWith("myst-stage.json", StringComparison.OrdinalIgnoreCase));
            var option = Path.Combine(destination, "WorldOption.sav");
            if (File.Exists(option) && plan.WorldOptionMode != WorldOptionImportMode.Preserve)
            {
                if (plan.WorldOptionMode == WorldOptionImportMode.Quarantine)
                {
                    quarantine = Path.Combine(destination, "MystImportData"); Directory.CreateDirectory(quarantine);
                    File.Move(option, Path.Combine(quarantine, "OriginalWorldOption.sav"));
                }
                else File.Delete(option);
            }
            ValidateStagedWorld(destination, scan);
            var manifest = WriteManifest(scan, plan, destination, backup, quarantine, "Installed");
            return new WorldImportResult { Success = true, Message = "World import installed and verified.", DestinationWorldPath = destination, BackupPath = backup, ManifestPath = manifest, QuarantinePath = quarantine };
        }
        catch
        {
            try { if (Directory.Exists(destination)) Directory.Delete(destination, true); } catch { }
            throw;
        }
        finally { try { if (Directory.Exists(stage)) Directory.Delete(stage, true); } catch { } }
    }


    public string ValidateInstalledWorld(string worldPath)
    {
        if (string.IsNullOrWhiteSpace(worldPath) || !Directory.Exists(worldPath))
            throw new DirectoryNotFoundException("The imported world folder was not found.");
        var level = Path.Combine(worldPath, "Level.sav");
        if (!File.Exists(level) || new FileInfo(level).Length == 0)
            throw new InvalidDataException("Level.sav is missing or empty.");
        var meta = Path.Combine(worldPath, "LevelMeta.sav");
        var players = Path.Combine(worldPath, "Players");
        var playerCount = playerSaveDiscovery.DiscoverFromPlayersDirectory(players).Accepted.Count;
        var fileCount = fileSystem.EnumerateFiles(worldPath, "*", SearchOption.AllDirectories).Count;
        var hash = HashFile(level);
        return $"Validated: Level.sav {new FileInfo(level).Length:N0} bytes • LevelMeta.sav {(File.Exists(meta) ? "present" : "not present (advisory)")} • Players {playerCount} • Files {fileCount} • SHA-256 {hash[..12]}…";
    }
    public string Activate(string worldPath, bool serverRunning)
    {
        if (serverRunning) throw new InvalidOperationException("Stop PalServer before activating an imported world.");
        if (!File.Exists(Path.Combine(worldPath, "Level.sav"))) throw new InvalidDataException("The selected folder is not a valid Palworld world.");
        var worldId = Path.GetFileName(worldPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var configDir = Path.Combine(settings.ServerRoot, "Pal", "Saved", "Config", "WindowsServer");
        var gameUserSettings = Path.Combine(configDir, "GameUserSettings.ini");
        Directory.CreateDirectory(configDir);
        var text = File.Exists(gameUserSettings) ? File.ReadAllText(gameUserSettings) : "[/Script/Pal.PalGameLocalSettings]" + Environment.NewLine;
        var backup = gameUserSettings + ".myst-before-world-import-" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".bak";
        if (File.Exists(gameUserSettings)) File.Copy(gameUserSettings, backup, true);
        var pattern = @"(?im)^\s*DedicatedServerName\s*=.*$";
        text = Regex.IsMatch(text, pattern) ? Regex.Replace(text, pattern, "DedicatedServerName=" + worldId) : text.TrimEnd() + Environment.NewLine + "DedicatedServerName=" + worldId + Environment.NewLine;
        var temp = gameUserSettings + ".myst.tmp"; File.WriteAllText(temp, text, new UTF8Encoding(false)); File.Move(temp, gameUserSettings, true);
        var marker = Path.Combine(ImportsRoot, "active-world.json"); Directory.CreateDirectory(ImportsRoot);
        File.WriteAllText(marker, JsonSerializer.Serialize(new { WorldPath = worldPath, WorldId = worldId, GameUserSettings = gameUserSettings, PreviousSettingsBackup = File.Exists(backup) ? backup : "", ActivatedUtc = DateTime.UtcNow }, new JsonSerializerOptions { WriteIndented = true }));
        return marker;
    }

    public List<WorldImportHistoryRow> LoadHistory()
    {
        if (!Directory.Exists(HistoryRoot)) return [];
        var rows = new List<WorldImportHistoryRow>();
        foreach (var file in Directory.EnumerateFiles(HistoryRoot, "*.json").OrderByDescending(File.GetLastWriteTimeUtc))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(file)); var r = doc.RootElement;
                rows.Add(new WorldImportHistoryRow { ImportedUtc = r.GetProperty("importedUtc").GetDateTime(), SourceArchive = r.GetProperty("sourceArchive").GetString() ?? "", WorldId = r.GetProperty("destinationWorldId").GetString() ?? "", Status = r.GetProperty("status").GetString() ?? "", BackupPath = r.GetProperty("backupPath").GetString() ?? "", ManifestPath = file });
            } catch { }
        }
        return rows;
    }

    public void RestoreBackup(string backupPath, bool serverRunning)
    {
        if (serverRunning) throw new InvalidOperationException("Stop PalServer before restoring a pre-import backup.");
        if (!Directory.Exists(backupPath)) throw new DirectoryNotFoundException(backupPath);
        var saveRootBackup = Path.Combine(backupPath, "SaveGames");
        if (!Directory.Exists(saveRootBackup)) throw new InvalidDataException("The selected import backup does not contain SaveGames.");
        if (Directory.Exists(settings.SaveRoot)) Directory.Delete(settings.SaveRoot, true);
        CopyDirectory(saveRootBackup, settings.SaveRoot, _ => true);
    }

    private string BackupSaveRoot()
    {
        var root = Path.Combine(ImportsRoot, "Backups", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")); Directory.CreateDirectory(root);
        if (Directory.Exists(settings.SaveRoot)) CopyDirectory(settings.SaveRoot, Path.Combine(root, "SaveGames"), _ => true);
        return root;
    }
    private string WriteManifest(WorldImportScanResult scan, WorldImportPlan plan, string destination, string backup, string quarantine, string status)
    {
        Directory.CreateDirectory(HistoryRoot); var file = Path.Combine(HistoryRoot, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Path.GetFileName(destination) + ".json");
        var level = Path.Combine(destination, "Level.sav");
        var manifest = new { importedUtc = DateTime.UtcNow, sourceArchive = Path.GetFileName(scan.ArchivePath), sourcePath = scan.ArchivePath, sourceSha256 = scan.ArchiveSha256, destinationWorldId = Path.GetFileName(destination), destinationWorldPath = destination, levelSaveSha256 = HashFile(level), playerSaveCount = scan.PlayerSaveCount, worldOptionMode = plan.WorldOptionMode.ToString(), backupPath = backup, quarantinePath = quarantine, status };
        File.WriteAllText(file, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true })); return file;
    }
    private void ValidateStagedWorld(string root, WorldImportScanResult scan)
    {
        var level = Path.Combine(root, "Level.sav"); if (!File.Exists(level) || new FileInfo(level).Length == 0) throw new InvalidDataException("Staged Level.sav is missing or empty.");
        var players = Path.Combine(root, "Players");
        var discovered = playerSaveDiscovery.DiscoverFromPlayersDirectory(players).Accepted.Count;
        if (scan.PlayerSaveCount > 0 && discovered < scan.PlayerSaveCount) throw new InvalidDataException("Not all valid player saves were staged.");
    }
    private static bool IsAllowed(string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || !IsSafePath(relative)) return false;
        if (BlockedExtensions.Contains(Path.GetExtension(relative), StringComparer.OrdinalIgnoreCase)) return false;
        if (AllowedRootFiles.Contains(relative)) return true;
        return relative.StartsWith("Players/", StringComparison.OrdinalIgnoreCase) && relative.EndsWith(".sav", StringComparison.OrdinalIgnoreCase) && relative.Count(c => c == '/') == 1;
    }
    private static bool IsSafePath(string path) => !Path.IsPathRooted(path) && !path.Split('/').Any(p => p is ".." or ".") && !path.Contains(':');
    private static (string RootPrefix, WorldArchiveLayout Layout, string Warning) DetectWorldRoot(List<string> paths)
    {
        var levelCandidates = paths
            .Where(p => p.Equals("Level.sav", StringComparison.OrdinalIgnoreCase)
                        || p.EndsWith("/Level.sav", StringComparison.OrdinalIgnoreCase))
            .Where(p => !ContainsPathSegment(p, "backup"))
            .Select(p => new
            {
                Path = p,
                Prefix = p.Equals("Level.sav", StringComparison.OrdinalIgnoreCase)
                    ? ""
                    : p[..^"Level.sav".Length],
                Depth = p.Count(c => c == '/')
            })
            .OrderBy(x => x.Depth)
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (levelCandidates.Count == 0)
            return ("", WorldArchiveLayout.Unknown, "No non-backup Level.sav was found.");

        var selected = levelCandidates[0];
        var sameDepthCount = levelCandidates.Count(x => x.Depth == selected.Depth);
        var warning = sameDepthCount > 1
            ? "Multiple possible active worlds were found. The first shallowest world was selected: " + selected.Path
            : "";

        var layout = string.IsNullOrEmpty(selected.Prefix)
            ? WorldArchiveLayout.FlatWorld
            : WorldArchiveLayout.WrappedWorld;
        return (selected.Prefix, layout, warning);
    }

    private static bool IsBackupPath(string relative) => ContainsPathSegment(relative, "backup");

    private static bool ContainsPathSegment(string path, string segment) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part.Equals(segment, StringComparison.OrdinalIgnoreCase));
    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');
    private static string RemovePrefix(string path, string prefix) => !string.IsNullOrEmpty(prefix) && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? path[prefix.Length..] : path;
    private static string NormalizeWorldId(string value) => new string((value ?? "").Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
    private static string HashFile(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)); }
    private static void CopyDirectory(string source, string destination, Func<string,bool> include)
    {
        Directory.CreateDirectory(destination);
        foreach (var dir in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, dir)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) if (include(file)) { var target = Path.Combine(destination, Path.GetRelativePath(source, file)); Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target, true); }
    }
}
