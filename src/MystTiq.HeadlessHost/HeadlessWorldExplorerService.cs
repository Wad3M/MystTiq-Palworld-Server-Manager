using MystTiq.Core.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessWorldExplorerService
{
    private const int MaximumFiles = 5000;
    private const long TicksPerDay = 864_000_000_000L;
    private static readonly byte[] WorldClockNeedle = Encoding.UTF8.GetBytes("\"GameDateTimeTicks\"");
    private static readonly Regex WrappedWorldClockValue = new("\"value\"\\s*:\\s*\"?(?<ticks>-?\\d+)\"?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DirectWorldClockValue = new("^\\s*:\\s*\"?(?<ticks>-?\\d+)\"?", RegexOptions.Compiled);
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
        var (worldDayNumber, worldTimeText) = ReadAuthoritativeWorldClock(active.WorldPath, lastWorldSaveUtc);
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
            worldDayNumber,
            worldTimeText,
            worlds,
            files,
            statistics,
            integrity,
            DateTimeOffset.UtcNow,
            $"Active world {active.WorldId}: {files.Count} file(s), {playerSaveCount} player save(s).");
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

        var (worldDayNumber, worldTimeText) = ReadAuthoritativeWorldClock(active.WorldPath, active.LevelLastWriteUtc);
        return new HeadlessWorldExplorerSnapshot(
            true, paths.SaveRoot, active.WorldId, active.WorldPath, worlds.Count, 1, playerSaveCount,
            active.LevelSizeBytes, active.LevelLastWriteUtc, worldDayNumber, worldTimeText,
            worlds, [], new HeadlessWorldStatistics(1, playerSaveCount, 0, 0, 0, active.LevelLastWriteUtc, active.LevelLastWriteUtc),
            new HeadlessWorldIntegrity("Dashboard Summary", [], true), DateTimeOffset.UtcNow,
            $"Dashboard world summary for {active.WorldId}.");
    }

    private static (long? DayNumber, string? TimeText) ReadAuthoritativeWorldClock(string worldPath, DateTimeOffset sourceWriteUtc)
    {
        var candidates = new[]
        {
            Path.Combine(worldPath, "Level.sav.json"),
            Path.Combine(worldPath, "Level.json"),
            Path.Combine(worldPath, "Level.sav.decoded.json")
        };
        var decodedPath = candidates.FirstOrDefault(File.Exists);
        if (decodedPath is null) return (null, null);

        var ticks = TryReadGameDateTimeTicks(decodedPath);
        if (ticks is null || ticks < 0) return (null, null);
        var day = ticks.Value / TicksPerDay;
        var time = TimeSpan.FromTicks(ticks.Value % TicksPerDay);
        return (day, $"{(int)time.TotalHours:00}:{time.Minutes:00}");
    }

    private static long? TryReadGameDateTimeTicks(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var buffer = new byte[64 * 1024];
            var matched = 0;
            while (true)
            {
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0) return null;
                for (var i = 0; i < read; i++)
                {
                    var value = buffer[i];
                    if (value == WorldClockNeedle[matched])
                    {
                        matched++;
                        if (matched != WorldClockNeedle.Length) continue;
                        var tail = new byte[4096];
                        var copied = Math.Min(tail.Length, read - (i + 1));
                        if (copied > 0) Buffer.BlockCopy(buffer, i + 1, tail, 0, copied);
                        if (copied < tail.Length) copied += stream.Read(tail, copied, tail.Length - copied);
                        var text = Encoding.UTF8.GetString(tail, 0, copied);
                        var wrapped = WrappedWorldClockValue.Match(text);
                        if (wrapped.Success && long.TryParse(wrapped.Groups["ticks"].Value, out var wrappedTicks)) return wrappedTicks;
                        var direct = DirectWorldClockValue.Match(text);
                        if (direct.Success && long.TryParse(direct.Groups["ticks"].Value, out var directTicks)) return directTicks;
                        return null;
                    }
                    matched = value == WorldClockNeedle[0] ? 1 : 0;
                }
            }
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
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
    string Detail);

public sealed record HeadlessWorldStatistics(int SaveDataFiles, int PlayerFiles, int DiagnosticFiles, int OtherFiles, int EmptyFiles, DateTimeOffset? OldestFileUtc, DateTimeOffset? NewestFileUtc);
public sealed record HeadlessWorldIntegrity(string State, IReadOnlyList<string> Findings, bool RequiredFilesPresent);
