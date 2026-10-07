// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
using MystTiq.Core.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessWorldExplorerService
{
    private const int MaximumFiles = 5000;
    private readonly IServerPathProfile paths;

    public HeadlessWorldExplorerService(IServerPathProfile paths)
    {
        this.paths = paths;
    }

    public HeadlessWorldExplorerSnapshot Explore()
    {
        if (!Directory.Exists(paths.SaveRoot))
        {
            return new HeadlessWorldExplorerSnapshot(
                false,
                paths.SaveRoot,
                null,
                null,
                0,
                0,
                0,
                0,
                null,
                null,
                null,
                [],
                [],
                new HeadlessWorldStatistics(0, 0, 0, 0, 0, null, null),
                new HeadlessWorldIntegrity("Unavailable", ["SaveRoot does not exist."], false),
                DateTimeOffset.UtcNow,
                $"Palworld SaveRoot does not exist: {paths.SaveRoot}");
        }

        var worlds = DiscoverWorlds();
        var active = worlds
            .Where(world => world.LevelSaveExists)
            .OrderByDescending(world => world.LevelLastWriteUtc)
            .FirstOrDefault();

        if (active is null)
        {
            return new HeadlessWorldExplorerSnapshot(
                false,
                paths.SaveRoot,
                null,
                null,
                worlds.Count,
                0,
                0,
                0,
                null,
                null,
                null,
                worlds,
                [],
                new HeadlessWorldStatistics(0, 0, 0, 0, 0, null, null),
                new HeadlessWorldIntegrity("Unavailable", ["No active Level.sav was discovered."], false),
                DateTimeOffset.UtcNow,
                "No Palworld world containing Level.sav was discovered.");
        }

        var files = EnumerateWorldFiles(active.WorldPath);
        var playerSaveCount = files.Count(file =>
            file.Category == "Player Save" &&
            file.RelativePath.StartsWith("Players/", StringComparison.OrdinalIgnoreCase));
        var totalBytes = files.Sum(file => file.SizeBytes);
        var lastWorldSaveUtc = active.LevelLastWriteUtc;
        // v1.0.0.4: one clock reader (HeadlessWorldClockService), which also says whether the decoded copy is current.
        var clock = HeadlessWorldClockService.Read(active.WorldPath, HeadlessWorldClockService.CacheRootFor(paths));
        (long Day, string Time)? split = clock.Ticks is { } ticks and >= 0 ? WorldClock.Split(ticks) : null;
        var statistics = BuildStatistics(files);
        var integrity = BuildIntegrity(files);

        return new HeadlessWorldExplorerSnapshot(
            true,
            paths.SaveRoot,
            active.WorldId,
            active.WorldPath,
            worlds.Count,
            files.Count,
            playerSaveCount,
            totalBytes,
            lastWorldSaveUtc,
            split?.Day,
            split?.Time,
            worlds,
            files,
            statistics,
            integrity,
            DateTimeOffset.UtcNow,
            $"Active world {active.WorldId}: {files.Count} file(s), {playerSaveCount} player save(s).")
        {
            WorldClockCurrent = clock.Current,
            WorldClockAsOfUtc = clock.DecodedUtc is { } at ? new DateTimeOffset(at, TimeSpan.Zero) : null,
        };
    }

    public HeadlessWorldExplorerSnapshot ExploreDashboard()
    {
        if (!Directory.Exists(paths.SaveRoot)) return Explore();
        var worlds = DiscoverWorlds();
        var active = worlds.Where(world => world.LevelSaveExists).OrderByDescending(world => world.LevelLastWriteUtc).FirstOrDefault();
        if (active is null) return Explore();

        var playersPath = Path.Combine(active.WorldPath, "Players");
        var playerSaveCount = 0;
        try
        {
            if (Directory.Exists(playersPath))
                playerSaveCount = Directory.EnumerateFiles(playersPath, "*.sav", SearchOption.TopDirectoryOnly).Count();
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        // v1.0.0.4: the day comes from the decoded copy; say whether it is as new as Level.sav (it was three days old live).
        var clock = HeadlessWorldClockService.Read(active.WorldPath, HeadlessWorldClockService.CacheRootFor(paths));
        (long Day, string Time)? split = clock.Ticks is { } ticks and >= 0 ? WorldClock.Split(ticks) : null;
        return new HeadlessWorldExplorerSnapshot(
            true, paths.SaveRoot, active.WorldId, active.WorldPath, worlds.Count, 1, playerSaveCount,
            active.LevelSizeBytes, active.LevelLastWriteUtc, split?.Day, split?.Time,
            worlds, [], new HeadlessWorldStatistics(1, playerSaveCount, 0, 0, 0, active.LevelLastWriteUtc, active.LevelLastWriteUtc),
            new HeadlessWorldIntegrity("Dashboard Summary", [], true), DateTimeOffset.UtcNow,
            $"Dashboard world summary for {active.WorldId}.")
        {
            WorldClockCurrent = clock.Current,
            WorldClockAsOfUtc = clock.DecodedUtc is { } at ? new DateTimeOffset(at, TimeSpan.Zero) : null,
        };
    }

    private IReadOnlyList<HeadlessWorldCandidate> DiscoverWorlds()
    {
        var candidates = new List<HeadlessWorldCandidate>();
        var backupRoot = NormalizePath(paths.BackupRoot);

        try
        {
            foreach (var worldPath in EnumerateCanonicalWorldDirectories(paths.SaveRoot))
            {
                var normalized = NormalizePath(worldPath);
                if (!string.IsNullOrWhiteSpace(backupRoot) && normalized.StartsWith(backupRoot, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (IsBackupLikeWorldPath(worldPath))
                    continue;

                var levelPath = Path.Combine(worldPath, "Level.sav");
                if (!File.Exists(levelPath))
                    continue;

                var worldId = Path.GetFileName(worldPath);
                if (string.IsNullOrWhiteSpace(worldId))
                    continue;

                var info = new FileInfo(levelPath);
                candidates.Add(new HeadlessWorldCandidate(
                    worldId,
                    worldPath,
                    true,
                    info.Length,
                    info.LastWriteTimeUtc,
                    Directory.Exists(Path.Combine(worldPath, "Players"))));
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }

        return candidates
            .GroupBy(candidate => candidate.WorldPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(candidate => candidate.LevelLastWriteUtc)
            .ToList();
    }

    private static IEnumerable<string> EnumerateCanonicalWorldDirectories(string saveRoot)
    {
        // Palworld dedicated servers normally use SaveGames/0/<WorldId>.  Scan only
        // direct world containers instead of recursively searching every Level.sav;
        // recursive discovery caused backup snapshots copied beneath SaveGames to be
        // misidentified as live worlds.
        foreach (var firstLevel in Directory.EnumerateDirectories(saveRoot))
        {
            if (File.Exists(Path.Combine(firstLevel, "Level.sav")))
                yield return firstLevel;

            foreach (var secondLevel in Directory.EnumerateDirectories(firstLevel))
                if (File.Exists(Path.Combine(secondLevel, "Level.sav")))
                    yield return secondLevel;
        }
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        }
        catch { return string.Empty; }
    }

    private static bool IsBackupLikeWorldPath(string path)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
        return name.Contains("backup", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
               name.EndsWith(".old", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<HeadlessWorldFile> EnumerateWorldFiles(string worldPath)
    {
        var files = new List<HeadlessWorldFile>();

        try
        {
            foreach (var path in Directory.EnumerateFiles(
                         worldPath,
                         "*",
                         SearchOption.AllDirectories)
                     .Take(MaximumFiles))
            {
                var info = new FileInfo(path);
                var relative = Path.GetRelativePath(worldPath, path)
                    .Replace(Path.DirectorySeparatorChar, '/');

                files.Add(new HeadlessWorldFile(
                    relative,
                    Categorize(relative),
                    info.Length,
                    info.LastWriteTimeUtc,
                    GetStatus(relative, info.Length)));
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Partial read-only inventory is preferable to failing the entire explorer.
        }
        catch (IOException)
        {
            // Partial read-only inventory is preferable to failing the entire explorer.
        }

        return files
            .OrderBy(file => CategoryOrder(file.Category))
            .ThenBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string Categorize(string relativePath)
    {
        if (relativePath.StartsWith("Players/", StringComparison.OrdinalIgnoreCase) &&
            relativePath.EndsWith(".sav", StringComparison.OrdinalIgnoreCase))
            return "Player Save";

        var name = Path.GetFileName(relativePath);

        if (name.Equals("Level.sav", StringComparison.OrdinalIgnoreCase))
            return "World";
        if (name.Equals("LevelMeta.sav", StringComparison.OrdinalIgnoreCase))
            return "World Metadata";
        if (name.Equals("WorldOption.sav", StringComparison.OrdinalIgnoreCase))
            return "World Options";
        if (name.EndsWith(".sav", StringComparison.OrdinalIgnoreCase))
            return "Save Data";
        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return "Decoded / Diagnostic";

        return "Other";
    }

    private static string GetStatus(string relativePath, long sizeBytes)
    {
        if (sizeBytes <= 0)
            return "Empty";

        if (relativePath.Equals("Level.sav", StringComparison.OrdinalIgnoreCase))
            return "Required / Present";

        return "Present";
    }

    private static int CategoryOrder(string category) => category switch
    {
        "World" => 0,
        "World Metadata" => 1,
        "World Options" => 2,
        "Player Save" => 3,
        "Save Data" => 4,
        "Decoded / Diagnostic" => 5,
        _ => 6
    };

    private static HeadlessWorldStatistics BuildStatistics(IReadOnlyList<HeadlessWorldFile> files) => new(
        files.Count(x => x.Category is "World" or "World Metadata" or "World Options" or "Save Data"),
        files.Count(x => x.Category == "Player Save"),
        files.Count(x => x.Category == "Decoded / Diagnostic"),
        files.Count(x => x.Category == "Other"),
        files.Count(x => x.SizeBytes == 0),
        files.Count == 0 ? null : files.Min(x => x.LastWriteUtc),
        files.Count == 0 ? null : files.Max(x => x.LastWriteUtc));

    private static HeadlessWorldIntegrity BuildIntegrity(IReadOnlyList<HeadlessWorldFile> files)
    {
        var findings = new List<string>();
        if (!files.Any(x => x.RelativePath.Equals("Level.sav", StringComparison.OrdinalIgnoreCase) && x.SizeBytes > 0)) findings.Add("Required Level.sav is missing or empty.");
        if (files.Any(x => x.SizeBytes == 0)) findings.Add($"{files.Count(x => x.SizeBytes == 0)} empty file(s) require review.");
        var invalidPlayers = files.Count(x => x.Category == "Player Save" && !Regex.IsMatch(Path.GetFileNameWithoutExtension(x.RelativePath), "^[0-9A-Fa-f]{32}$"));
        if (invalidPlayers > 0) findings.Add($"{invalidPlayers} player save filename(s) do not match the canonical 32-hex identity format.");
        return new(findings.Count == 0 ? "Healthy" : "Needs Review", findings, findings.Count == 0);
    }
}

public sealed record HeadlessWorldCandidate(
    string WorldId,
    string WorldPath,
    bool LevelSaveExists,
    long LevelSizeBytes,
    DateTimeOffset LevelLastWriteUtc,
    bool PlayersDirectoryExists);

public sealed record HeadlessWorldFile(
    string RelativePath,
    string Category,
    long SizeBytes,
    DateTimeOffset LastWriteUtc,
    string Status);

public sealed record HeadlessWorldExplorerSnapshot(
    bool Available,
    string SaveRoot,
    string? ActiveWorldId,
    string? ActiveWorldPath,
    int WorldCount,
    int FileCount,
    int PlayerSaveCount,
    long TotalSizeBytes,
    DateTimeOffset? LastWorldSaveUtc,
    long? WorldDayNumber,
    string? WorldTimeText,
    IReadOnlyList<HeadlessWorldCandidate> Worlds,
    IReadOnlyList<HeadlessWorldFile> Files,
    HeadlessWorldStatistics Statistics,
    HeadlessWorldIntegrity Integrity,
    DateTimeOffset ObservedAt,
    string Detail)
{
    // v1.0.0.4: whether the day above comes from a decoded copy as new as Level.sav, and when that copy was made.
    public bool WorldClockCurrent { get; init; } = true;
    public DateTimeOffset? WorldClockAsOfUtc { get; init; }
}

public sealed record HeadlessWorldStatistics(int SaveDataFiles, int PlayerFiles, int DiagnosticFiles, int OtherFiles, int EmptyFiles, DateTimeOffset? OldestFileUtc, DateTimeOffset? NewestFileUtc);
public sealed record HeadlessWorldIntegrity(string State, IReadOnlyList<string> Findings, bool RequiredFilesPresent);
