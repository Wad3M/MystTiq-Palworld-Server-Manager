// MystTiq v1.1.5.0: file reviewed for this release (2026-10-22).
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace MystTiq.Core.Services;

// v1.0.7.5 (roadmap Q-10): a half-finished install or update is never reported as installed.
//
// PalServer.exe can exist while the files around it are half updated: SteamCMD killed, MystTiq stopped, or the computer
// switched off during an update. Two signals say so:
//   - a marker MystTiq writes in the server folder before SteamCMD runs, removed only once Steam reports the install
//     complete. A marker left behind (and no update running in this process) means an update did not finish.
//   - Steam's own app manifest (steamapps/appmanifest_2394010.acf): "StateFlags" "4" is a fully installed app; anything
//     else (update required, update running) is not finished. No manifest (a copied or hand-made server) says nothing.
public static partial class ServerInstallIntegrity
{
    public const string MarkerFileName = ".mysttiq-update-in-progress";
    public const string PalworldAppId = "2394010";
    private const int FullyInstalled = 4;

    private static readonly ConcurrentDictionary<string, byte> Running = new(StringComparer.OrdinalIgnoreCase);

    public static string MarkerPath(string serverRoot) => Path.Combine(serverRoot, MarkerFileName);

    /// <summary>Steam's StateFlags for Palworld in this folder, or null when there is no readable manifest.</summary>
    public static int? ManifestStateFlags(string serverRoot)
    {
        try
        {
            var manifest = Path.Combine(serverRoot, "steamapps", $"appmanifest_{PalworldAppId}.acf");
            if (!File.Exists(manifest)) return null;
            var match = StateFlagsPattern().Match(File.ReadAllText(manifest));
            return match.Success && int.TryParse(match.Groups[1].Value, out var flags) ? flags : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    public static bool IsUpdateRunning(string serverRoot) => Running.ContainsKey(Normalize(serverRoot));

    /// <summary>Why the install in this folder is not finished, or null when nothing says so (or an update is running now).</summary>
    public static string? Incomplete(string serverRoot)
    {
        if (string.IsNullOrWhiteSpace(serverRoot) || IsUpdateRunning(serverRoot)) return null;
        var marker = MarkerPath(serverRoot);
        if (File.Exists(marker))
        {
            var started = ReadStarted(marker);
            return $"An update of the server files{(started is { } at ? $" started {at:yyyy-MM-dd HH:mm} UTC" : string.Empty)} did not finish (SteamCMD or MystTiq stopped during it). Run Update again before starting the server.";
        }
        return ManifestStateFlags(serverRoot) is { } flags && flags != FullyInstalled
            ? $"Steam reports this install as not finished (state {flags}). Run Update with Validate before starting the server."
            : null;
    }

    /// <summary>Called before SteamCMD runs: the marker stays until <see cref="EndUpdate"/> finds the install complete.</summary>
    public static void BeginUpdate(string serverRoot, bool validate)
    {
        Directory.CreateDirectory(serverRoot);
        Running[Normalize(serverRoot)] = 0;
        File.WriteAllText(MarkerPath(serverRoot), $"started={DateTimeOffset.UtcNow:O}\nvalidate={validate}\n");
    }

    /// <summary>
    /// Called when SteamCMD has exited (or failed to run). The marker goes when the executable exists and Steam reports the
    /// app fully installed (or there is no manifest, after a successful run); otherwise it stays, and the status says so.
    /// </summary>
    public static void EndUpdate(string serverRoot, string serverExecutable, bool succeeded)
    {
        try
        {
            var flags = ManifestStateFlags(serverRoot);
            var complete = File.Exists(serverExecutable) && (flags == FullyInstalled || (flags is null && succeeded));
            if (complete) File.Delete(MarkerPath(serverRoot));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* the marker stays: safe side */ }
        finally { Running.TryRemove(Normalize(serverRoot), out _); }
    }

    private static DateTimeOffset? ReadStarted(string marker)
    {
        try
        {
            var line = File.ReadLines(marker).FirstOrDefault(l => l.StartsWith("started=", StringComparison.Ordinal));
            return line is not null && DateTimeOffset.TryParse(line[8..], out var at) ? at.ToUniversalTime() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; }
    }

    private static string Normalize(string serverRoot) => Path.GetFullPath(serverRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    [GeneratedRegex("\"StateFlags\"\\s+\"(\\d+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex StateFlagsPattern();
}
