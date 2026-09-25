using System.Diagnostics;
using System.Text.Json;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.8.13.0: the English display names of items and Pals ("Pal Sphere", "Lamball"), read from this server's own game
// pak. The pak is Oodle-compressed; the only Oodle decompressor on hand is the "ooz" Python module the PlM/Oodle save
// tooling already installs (the open-source ooz code is GPL, so it is not built into MystTiq). So MystTiq ships a small
// read-only extractor script of its own (Tools/extract_game_names.py) and runs it with the user's Python when the pak
// changes; nothing from the game is bundled. The result is cached under the manager runtime root. Without Python or
// ooz everything keeps working on ids, and the reason is reported.
public sealed record GameNameCatalog(string Language, IReadOnlyDictionary<string, string> Items, IReadOnlyDictionary<string, string> Pals)
{
    public static readonly GameNameCatalog Empty = new("en",
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    private const string AlphaPrefix = "BOSS_";

    public bool HasNames => Items.Count > 0 || Pals.Count > 0;

    // Ids match ignoring case: the save writes "Sheepball" where the name table has "SheepBall".
    public string? ItemName(string? id) => !string.IsNullOrWhiteSpace(id) && Items.TryGetValue(id.Trim(), out var name) ? name : null;

    public string? PalName(string? characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)) return null;
        var id = characterId.Trim();
        if (id.StartsWith(AlphaPrefix, StringComparison.OrdinalIgnoreCase)) id = id[AlphaPrefix.Length..];
        return Pals.TryGetValue(id, out var name) ? name : null;
    }

    // Pure (logic harness): the extractor's JSON, {"lang": .., "items": {id: name}, "pals": {id: name}}.
    public static GameNameCatalog? FromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            static Dictionary<string, string> Read(JsonElement root, string name)
            {
                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (root.TryGetProperty(name, out var obj) && obj.ValueKind == JsonValueKind.Object)
                    foreach (var p in obj.EnumerateObject())
                        if (p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.Value.GetString()) && !string.IsNullOrWhiteSpace(p.Name))
                            map[p.Name.Trim()] = p.Value.GetString()!.Trim();
                return map;
            }
            var lang = root.TryGetProperty("lang", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() ?? "en" : "en";
            return new GameNameCatalog(lang, Read(root, "items"), Read(root, "pals"));
        }
        catch (JsonException) { return null; }
    }
}

public sealed record GameNameStatus(bool Available, string Detail, int ItemCount, int PalCount);

public sealed class HeadlessGameNameService
{
    public const string Language = "en";
    public static readonly TimeSpan ExtractorTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(10);

    private readonly IServerPathProfile paths;
    private readonly string scriptPath;
    private readonly Func<string?> findPython;
    private readonly object gate = new();
    private (string Pak, long Length, DateTime WriteUtc)? loadedStamp;
    private GameNameCatalog catalog = GameNameCatalog.Empty;
    private GameNameStatus status = new(false, "Names have not been read yet.", 0, 0);
    private (string Pak, long Length, DateTime WriteUtc, DateTimeOffset At)? lastFailure;

    public HeadlessGameNameService(IServerPathProfile paths, string? scriptPath = null, Func<string?>? findPython = null)
    {
        this.paths = paths;
        this.scriptPath = scriptPath ?? Path.Combine(AppContext.BaseDirectory, "Tools", "extract_game_names.py");
        this.findPython = findPython ?? FindPython;
    }

    public string CachePath => Path.Combine(paths.ManagerRuntimeRoot, "game-names", $"{Language}.json");

    // The names for this server, reading the cache or running the extractor when the pak changed. Never throws.
    public (GameNameCatalog Catalog, GameNameStatus Status) Get()
    {
        lock (gate)
        {
            var pak = FindPak();
            if (pak is null)
                return Set(null, GameNameCatalog.Empty, Unavailable("no game pak (Pal-*.pak) was found under Pal/Content/Paks"));
            var stamp = (pak.FullName, pak.Length, pak.LastWriteTimeUtc);
            if (loadedStamp == stamp) return (catalog, status);

            if (TryReadCache(stamp) is { } cached)
                return Set(stamp, cached, Ready(cached, pak.Name));

            if (lastFailure is { } failed && (failed.Pak, failed.Length, failed.WriteUtc) == stamp && DateTimeOffset.UtcNow - failed.At < RetryAfterFailure)
                return (catalog, status);

            var (built, reason) = RunExtractor(pak.FullName);
            if (built is null)
            {
                lastFailure = (stamp.FullName, stamp.Length, stamp.LastWriteTimeUtc, DateTimeOffset.UtcNow);
                return Set(null, GameNameCatalog.Empty, Unavailable(reason));
            }
            WriteCache(stamp, built);
            return Set(stamp, built, Ready(built, pak.Name));
        }
    }

    private (GameNameCatalog, GameNameStatus) Set((string, long, DateTime)? stamp, GameNameCatalog value, GameNameStatus state)
    {
        loadedStamp = stamp; catalog = value; status = state;
        return (catalog, status);
    }

    private static GameNameStatus Ready(GameNameCatalog names, string pakName) =>
        new(true, $"Names from the game files ({pakName}): {names.Items.Count} items and {names.Pals.Count} Pals.", names.Items.Count, names.Pals.Count);

    public static GameNameStatus Unavailable(string reason) =>
        new(false, $"Showing ids only: {reason}. Names come from this server's own game files and need Python with the Oodle module (ooz), which the PlM/Oodle save tooling installs.", 0, 0);

    private FileInfo? FindPak()
    {
        var dir = Path.Combine(paths.ServerRoot, "Pal", "Content", "Paks");
        try
        {
            return Directory.Exists(dir)
                ? Directory.EnumerateFiles(dir, "Pal-*.pak", SearchOption.TopDirectoryOnly).Select(p => new FileInfo(p)).OrderByDescending(f => f.Length).FirstOrDefault()
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    private GameNameCatalog? TryReadCache((string Pak, long Length, DateTime WriteUtc) stamp)
    {
        try
        {
            if (!File.Exists(CachePath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(CachePath));
            var root = doc.RootElement;
            if (!root.TryGetProperty("pak", out var pak) || pak.GetString() != stamp.Pak) return null;
            if (!root.TryGetProperty("pakLength", out var length) || length.GetInt64() != stamp.Length) return null;
            if (!root.TryGetProperty("pakWriteUtc", out var write) || write.GetDateTime().ToUniversalTime() != stamp.WriteUtc) return null;
            var names = GameNameCatalog.FromJson(root.GetRawText());
            return names is { HasNames: true } ? names : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException) { return null; }
    }

    private void WriteCache((string Pak, long Length, DateTime WriteUtc) stamp, GameNameCatalog names)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            var body = new Dictionary<string, object>
            {
                ["pak"] = stamp.Pak, ["pakLength"] = stamp.Length, ["pakWriteUtc"] = stamp.WriteUtc, ["builtUtc"] = DateTime.UtcNow,
                ["lang"] = names.Language, ["items"] = names.Items, ["pals"] = names.Pals
            };
            var temp = CachePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(body));
            File.Move(temp, CachePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private (GameNameCatalog? Names, string Reason) RunExtractor(string pak)
    {
        if (!File.Exists(scriptPath)) return (null, "the name extractor is missing from this MystTiq install");
        var python = findPython();
        if (python is null) return (null, "Python was not found");
        var start = new ProcessStartInfo(python)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
        };
        start.ArgumentList.Add(scriptPath); start.ArgumentList.Add("--pak"); start.ArgumentList.Add(pak);
        start.ArgumentList.Add("--lang"); start.ArgumentList.Add(Language);
        start.Environment["PYTHONIOENCODING"] = "utf-8";
        try
        {
            using var process = Process.Start(start);
            if (process is null) return (null, "Python could not be started");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(ExtractorTimeout))
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return (null, "reading the game files took too long");
            }
            var error = stderr.GetAwaiter().GetResult().Trim();
            return process.ExitCode switch
            {
                0 => GameNameCatalog.FromJson(stdout.GetAwaiter().GetResult()) is { HasNames: true } names ? (names, string.Empty) : (null, "the game files gave no names"),
                2 => (null, "Python's Oodle module (ooz) is not installed"),
                _ => (null, $"the game files could not be read ({LastLine(error)})")
            };
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return (null, $"Python could not be run ({ex.Message})");
        }
    }

    // The last line: a Python traceback ends with the actual error (its first line is only "Traceback ...").
    private static string LastLine(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).LastOrDefault(l => l.Length > 0) ?? "no detail";

    private static string? FindPython()
    {
        var names = OperatingSystem.IsWindows() ? new[] { "python.exe", "py.exe" } : ["python3", "python"];
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            // The WindowsApps "python.exe" is the Microsoft Store stub, which only opens the Store.
            if (dir.Contains("WindowsApps", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var name in names)
            {
                try { var candidate = Path.Combine(dir.Trim(), name); if (File.Exists(candidate)) return candidate; }
                catch (ArgumentException) { }
            }
        }
        return null;
    }
}
