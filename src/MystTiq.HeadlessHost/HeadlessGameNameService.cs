// MystTiq v0.9.2.0: file reviewed for this release (2026-09-28).
using System.Diagnostics;
using System.Text.Json;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.8.13.0: the display names of items and Pals ("Pal Sphere", "Lamball"), read from this server's own game
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
    // v0.9.2.0: names in the Desktop's display language. The game ships its name tables for these (its source language,
    // Japanese, is the base table; the rest are L10N cultures with the same codes as the Desktop's languages). English
    // is the default and the fallback when a language's table is missing from the installed game.
    public const string DefaultLanguage = "en";
    public static readonly IReadOnlyList<string> Languages = ["en", "zh-Hans", "es", "pt-BR", "ru", "de", "fr", "ja", "ko", "it", "pl", "tr"];
    public static readonly TimeSpan ExtractorTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(10);

    private readonly IServerPathProfile paths;
    private readonly string scriptPath;
    private readonly Func<string?> findPython;
    private readonly object gate = new();
    private readonly Dictionary<string, LanguageState> states = new(StringComparer.Ordinal);

    private sealed class LanguageState
    {
        public (string Pak, long Length, DateTime WriteUtc)? LoadedStamp;
        public GameNameCatalog Catalog = GameNameCatalog.Empty;
        public GameNameStatus Status = new(false, "Names have not been read yet.", 0, 0);
        public (string Pak, long Length, DateTime WriteUtc, DateTimeOffset At)? LastFailure;
    }

    public HeadlessGameNameService(IServerPathProfile paths, string? scriptPath = null, Func<string?>? findPython = null)
    {
        this.paths = paths;
        this.scriptPath = scriptPath ?? Path.Combine(AppContext.BaseDirectory, "Tools", "extract_game_names.py");
        this.findPython = findPython ?? FindPython;
    }

    /// <summary>One of <see cref="Languages"/>, matched ignoring case; anything else is English.</summary>
    public static string NormalizeLanguage(string? code) =>
        Languages.FirstOrDefault(l => string.Equals(l, code?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? DefaultLanguage;

    public string CachePath => CachePathFor(DefaultLanguage);
    public string CachePathFor(string language) => Path.Combine(paths.ManagerRuntimeRoot, "game-names", $"{NormalizeLanguage(language)}.json");

    // The names for this server in a language, reading the cache or running the extractor when the pak changed. A
    // language the installed game has no table for falls back to English, and says so. Never throws.
    public (GameNameCatalog Catalog, GameNameStatus Status) Get(string? language = null)
    {
        var code = NormalizeLanguage(language);
        var (names, state) = GetExact(code);
        if (code == DefaultLanguage || names.HasNames) return (names, state);
        var (english, englishState) = GetExact(DefaultLanguage);
        return english.HasNames
            ? (english, englishState with { Detail = $"{englishState.Detail} Shown in English: the names in this language could not be read from the game files." })
            : (names, state);
    }

    private (GameNameCatalog Catalog, GameNameStatus Status) GetExact(string code)
    {
        lock (gate)
        {
            if (!states.TryGetValue(code, out var st)) states[code] = st = new LanguageState();
            var pak = FindPak();
            if (pak is null)
                return Set(st, null, GameNameCatalog.Empty, Unavailable("no game pak (Pal-*.pak) was found under Pal/Content/Paks"));
            var stamp = (pak.FullName, pak.Length, pak.LastWriteTimeUtc);
            if (st.LoadedStamp == stamp) return (st.Catalog, st.Status);

            if (TryReadCache(code, stamp) is { } cached)
                return Set(st, stamp, cached, Ready(cached, pak.Name));

            if (st.LastFailure is { } failed && (failed.Pak, failed.Length, failed.WriteUtc) == stamp && DateTimeOffset.UtcNow - failed.At < RetryAfterFailure)
                return (st.Catalog, st.Status);

            var (built, reason) = RunExtractor(pak.FullName, code);
            if (built is null)
            {
                st.LastFailure = (stamp.FullName, stamp.Length, stamp.LastWriteTimeUtc, DateTimeOffset.UtcNow);
                return Set(st, null, GameNameCatalog.Empty, Unavailable(reason));
            }
            WriteCache(code, stamp, built);
            return Set(st, stamp, built, Ready(built, pak.Name));
        }
    }

    private static (GameNameCatalog, GameNameStatus) Set(LanguageState st, (string, long, DateTime)? stamp, GameNameCatalog value, GameNameStatus state)
    {
        st.LoadedStamp = stamp; st.Catalog = value; st.Status = state;
        return (st.Catalog, st.Status);
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

    private GameNameCatalog? TryReadCache(string code, (string Pak, long Length, DateTime WriteUtc) stamp)
    {
        var cachePath = CachePathFor(code);
        try
        {
            if (!File.Exists(cachePath)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(cachePath));
            var root = doc.RootElement;
            if (!root.TryGetProperty("pak", out var pak) || pak.GetString() != stamp.Pak) return null;
            if (!root.TryGetProperty("pakLength", out var length) || length.GetInt64() != stamp.Length) return null;
            if (!root.TryGetProperty("pakWriteUtc", out var write) || write.GetDateTime().ToUniversalTime() != stamp.WriteUtc) return null;
            var names = GameNameCatalog.FromJson(root.GetRawText());
            return names is { HasNames: true } ? names : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or FormatException) { return null; }
    }

    private void WriteCache(string code, (string Pak, long Length, DateTime WriteUtc) stamp, GameNameCatalog names)
    {
        var cachePath = CachePathFor(code);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            var body = new Dictionary<string, object>
            {
                ["pak"] = stamp.Pak, ["pakLength"] = stamp.Length, ["pakWriteUtc"] = stamp.WriteUtc, ["builtUtc"] = DateTime.UtcNow,
                ["lang"] = names.Language, ["items"] = names.Items, ["pals"] = names.Pals
            };
            var temp = cachePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(body));
            File.Move(temp, cachePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private (GameNameCatalog? Names, string Reason) RunExtractor(string pak, string code)
    {
        if (!File.Exists(scriptPath)) return (null, "the name extractor is missing from this MystTiq install");
        var python = findPython();
        if (python is null) return (null, "Python was not found");
        var start = new ProcessStartInfo(python)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
        };
        start.ArgumentList.Add(scriptPath); start.ArgumentList.Add("--pak"); start.ArgumentList.Add(pak);
        start.ArgumentList.Add("--lang"); start.ArgumentList.Add(code);
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
                4 => (null, $"the installed game has no {code} name table"),
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
