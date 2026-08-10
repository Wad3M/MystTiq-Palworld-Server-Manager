using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Single authoritative resolver for UE4SS runtime and MOD paths.
/// Consumers must use this service instead of constructing Win64\\Mods or Win64\\ue4ss\\Mods directly.
/// Phase 1 provides detection, runtime-log verification, session caching, and diagnostics only.
/// </summary>
public sealed class Ue4ssRuntimeResolver
{
    private static readonly Regex ModsRootLogPattern = new(
        "Loading\\s+mods\\s+from:\\s*[\"']?(?<path>.+?)[\"']?\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex StartedLuaModPattern = new(
        "Starting\\s+Lua\\s+mod\\s+[\"'](?<name>[^\"']+)[\"']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly AppSettings settings;
    private readonly IServerPathProfile paths;
    private readonly object sync = new();
    private Ue4ssRuntimeInfo? cached;

    public Ue4ssRuntimeResolver(AppSettings settings, IServerPathProfile? paths = null)
    {
        this.settings = settings;
        this.paths = paths ?? ServerPathProfile.ForCurrentPlatform(settings);
    }

    /// <summary>
    /// Returns the session-cached runtime snapshot. Call Refresh only when configuration or runtime state changes.
    /// </summary>
    public Ue4ssRuntimeInfo Resolve()
    {
        lock (sync)
            return cached ??= ResolveCore();
    }

    public string GetActiveModsRoot() => Resolve().ActiveModsRoot;

    /// <summary>
    /// Explicitly invalidates and recomputes the runtime snapshot.
    /// </summary>
    public Ue4ssRuntimeInfo Refresh()
    {
        lock (sync)
        {
            cached = ResolveCore();
            return cached;
        }
    }

    public void Invalidate()
    {
        lock (sync)
            cached = null;
    }

    public IReadOnlyList<string> BuildDiagnosticLines(Ue4ssRuntimeInfo? info = null)
    {
        info ??= Resolve();
        return
        [
            $"[UE4SS] Win64 Root: {info.Win64Root}",
            $"[UE4SS] UE4SS Root: {info.Ue4ssRoot}",
            $"[UE4SS] Modern Mods Root: {info.ModernModsRoot} (exists: {info.HasModernModsRoot})",
            $"[UE4SS] Legacy Mods Root: {info.LegacyModsRoot} (exists: {info.HasLegacyModsRoot})",
            $"[UE4SS] Active Mods Root: {info.ActiveModsRoot}",
            $"[UE4SS] Detection Method: {info.DetectionMethod}",
            $"[UE4SS] Runtime Mods Root: {(info.RuntimeModsRoot ?? "Not reported")}",
            $"[UE4SS] Runtime Verified: {info.RuntimeVerified}",
            $"[UE4SS] Active Mod Directories: {info.ActiveModDirectoryCount}",
            $"[UE4SS] Legacy Mod Directories: {info.LegacyModDirectoryCount}",
            $"[UE4SS] Lua Mods Loaded: {info.LoadedMods.Count}",
            $"[UE4SS] Runtime Health: {info.HealthState}" + (string.IsNullOrWhiteSpace(info.WarningMessage) ? "" : $" — {info.WarningMessage}")
        ];
    }

    private Ue4ssRuntimeInfo ResolveCore()
    {
        var win64Root = paths.RuntimeBinaryRoot;
        var ue4ssRoot = paths.Ue4ssRoot;
        var modernRoot = paths.Ue4ssModsRoot;
        var legacyRoot = paths.LegacyUe4ssModsRoot;

        var hasUe4ssRoot = Directory.Exists(ue4ssRoot);
        var hasModernRoot = Directory.Exists(modernRoot);
        var hasLegacyRoot = Directory.Exists(legacyRoot);
        var runtimeEvidence = TryReadRuntimeModsRoot(win64Root, ue4ssRoot);
        var loadedMods = ReadLoadedLuaMods(runtimeEvidence.LogPath);
        var legacyCount = SafeDirectoryCount(legacyRoot);

        string activeRoot;
        string detectionMethod;

        // A modern on-disk layout is authoritative when it exists. Runtime log evidence is
        // retained independently below so a disagreement becomes a real health warning.
        if (hasUe4ssRoot && hasModernRoot)
        {
            activeRoot = modernRoot;
            detectionMethod = "Modern UE4SS layout";
        }
        else if (!string.IsNullOrWhiteSpace(runtimeEvidence.Path))
        {
            activeRoot = NormalizeAbsolutePath(runtimeEvidence.Path!, win64Root);
            detectionMethod = "UE4SS runtime log";
        }
        else if (hasLegacyRoot)
        {
            activeRoot = legacyRoot;
            detectionMethod = "Legacy UE4SS layout";
        }
        else if (hasUe4ssRoot)
        {
            activeRoot = modernRoot;
            detectionMethod = "Expected modern UE4SS layout";
        }
        else
        {
            activeRoot = legacyRoot;
            detectionMethod = "Expected legacy UE4SS layout";
        }

        activeRoot = Path.GetFullPath(activeRoot);
        var runtimeRoot = string.IsNullOrWhiteSpace(runtimeEvidence.Path)
            ? null
            : Path.GetFullPath(NormalizeAbsolutePath(runtimeEvidence.Path!, win64Root));
        var runtimeVerified = runtimeRoot is not null;
        var runtimeMatches = !runtimeVerified || PathsEqual(activeRoot, runtimeRoot!);

        var health = !runtimeVerified ? "Unverified" : runtimeMatches ? "Healthy" : "Degraded";
        var warning = runtimeVerified && !runtimeMatches
            ? $"UE4SS Mod Root Mismatch. Manager: {activeRoot} | Runtime: {runtimeRoot}"
            : hasLegacyRoot && hasModernRoot
                ? "Both modern and legacy Mods roots exist. Modern root is active; legacy content must be treated as migration information."
                : "";

        return new Ue4ssRuntimeInfo(
            win64Root,
            ue4ssRoot,
            modernRoot,
            legacyRoot,
            activeRoot,
            runtimeRoot,
            detectionMethod,
            hasUe4ssRoot,
            hasModernRoot,
            hasLegacyRoot,
            runtimeVerified,
            runtimeMatches,
            health,
            warning,
            runtimeEvidence.LogPath,
            loadedMods,
            SafeDirectoryCount(activeRoot),
            legacyCount);
    }

    private static (string? Path, string? LogPath) TryReadRuntimeModsRoot(string win64Root, string ue4ssRoot)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine(win64Root, "UE4SS.log"),
            Path.Combine(ue4ssRoot, "UE4SS.log"),
            Path.Combine(ue4ssRoot, "Logs", "UE4SS.log"),
            Path.Combine(ue4ssRoot, "logs", "UE4SS.log")
        };

        try
        {
            if (Directory.Exists(win64Root))
            {
                foreach (var file in Directory.EnumerateFiles(win64Root, "UE4SS.log", SearchOption.AllDirectories))
                    candidates.Add(file);
            }
        }
        catch
        {
            // Discovery failure must not prevent deterministic filesystem fallback.
        }

        foreach (var logPath in candidates.Where(File.Exists).OrderByDescending(SafeLastWriteUtc))
        {
            try
            {
                string? lastMatch = null;
                foreach (var line in File.ReadLines(logPath))
                {
                    var match = ModsRootLogPattern.Match(line);
                    if (match.Success)
                        lastMatch = match.Groups["path"].Value.Trim().Trim('"', '\'');
                }

                if (!string.IsNullOrWhiteSpace(lastMatch))
                    return (lastMatch, logPath);
            }
            catch
            {
                // A locked or malformed runtime log is non-fatal; try the next candidate.
            }
        }

        return (null, null);
    }

    private static DateTime SafeLastWriteUtc(string path)
    {
        try { return File.GetLastWriteTimeUtc(path); }
        catch { return DateTime.MinValue; }
    }

    private static IReadOnlyList<string> ReadLoadedLuaMods(string? logPath)
    {
        if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath)) return [];
        var loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var line in File.ReadLines(logPath))
            {
                var match = StartedLuaModPattern.Match(line);
                if (match.Success) loaded.Add(match.Groups["name"].Value.Trim());
            }
        }
        catch { }
        return loaded.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static int SafeDirectoryCount(string path)
    {
        try { return string.IsNullOrWhiteSpace(path) || !Directory.Exists(path) ? 0 : Directory.EnumerateDirectories(path).Count(); }
        catch { return 0; }
    }

    private static string NormalizeAbsolutePath(string path, string win64Root)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        return Path.IsPathRooted(expanded) ? expanded : Path.Combine(win64Root, expanded);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            StringComparison.OrdinalIgnoreCase);
}
