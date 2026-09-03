using System.Text.RegularExpressions;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessEnvironmentChecklistService
{
    private readonly IServerPathProfile paths;

    public HeadlessEnvironmentChecklistService(IServerPathProfile paths) => this.paths = paths;

    public EnvironmentChecklistSnapshot GetSnapshot()
    {
        var rows = new List<EnvironmentChecklistItem>();
        var steamRoot = FindSteamRoot();
        Add(rows, "Steam Client", steamRoot is not null, steamRoot ?? "Not detected",
            steamRoot is null ? "Steam is optional, but required to discover local Workshop mods." : "Steam installation detected.",
            steamRoot is null ? "RESCAN" : "VERIFY", true);

        var palworldClient = FindPalworldClient(steamRoot);
        Add(rows, "Palworld Client", palworldClient is not null, palworldClient ?? "Not detected",
            palworldClient is null ? "Local Palworld client was not found in the detected Steam libraries." : "Palworld App ID 1623730 detected.",
            palworldClient is null ? "RESCAN" : "VERIFY", true);

        var python = FindOnPath(OperatingSystem.IsWindows() ? ["python.exe", "py.exe"] : ["python3", "python"]);
        Add(rows, "Python Runtime", python is not null, python ?? "Not detected",
            python is null ? "Required for palworld-save-tools and shared world discovery." : "Python is available for save decoding.",
            python is null ? "INSTALL" : "VERIFY", python is not null,
            python is null ? "BACKEND REQUIRED: Python installation is not yet owned by a safe headless package-management service." : null);

        Add(rows, "SteamCMD", File.Exists(paths.SteamCmdExecutable), paths.SteamCmdExecutable,
            File.Exists(paths.SteamCmdExecutable) ? "Ready for install, update, and repair operations." : "Required for automated dedicated-server installation and updates.",
            File.Exists(paths.SteamCmdExecutable) ? "VERIFY" : "INSTALL", true);

        Add(rows, "Palworld Dedicated Server", File.Exists(paths.ServerExecutable), paths.ServerExecutable,
            File.Exists(paths.ServerExecutable) ? "Server executable detected." : "Dedicated server App ID 2394010 is not installed at the configured location.",
            File.Exists(paths.ServerExecutable) ? "VERIFY" : "INSTALL", true);

        var ue4ss = DetectUe4ss();
        rows.Add(new EnvironmentChecklistItem("UE4SS Runtime", ue4ss.Status, paths.RuntimeBinaryRoot, ue4ss.Detail, ue4ss.Status == "MISSING" ? "INSTALL" : "MANAGE", ue4ss.Status != "MISSING",
            ue4ss.Status == "MISSING" ? "BACKEND REQUIRED: UE4SS installation is scheduled for the MOD/UE4SS restoration phase." : null));

        var saveTools = FindFirstExisting(
            Path.Combine(paths.ServerRoot, "Tools", "palworld-save-tools", "convert.py"),
            Path.Combine(paths.ServerRoot, "Tools", "PalworldSaveTools", "convert.py"));
        Add(rows, "Palworld Save Tools", saveTools is not null, saveTools ?? Path.Combine(paths.ServerRoot, "Tools", "palworld-save-tools", "convert.py"),
            saveTools is not null ? "Official save converter detected for shared world discovery." : "Required to decode Level.sav for Players, Guilds, Bases, and Inspector data.",
            saveTools is not null ? "VERIFY" : "INSTALL", saveTools is not null,
            saveTools is null ? "BACKEND REQUIRED: Palworld Save Tools installation requires a dedicated headless installer." : null);

        var cpp = DetectCppToolchain();
        Add(rows, "Microsoft C++ Build Tools", cpp is not null, cpp ?? (OperatingSystem.IsWindows() ? "Visual Studio Build Tools" : "Native compiler toolchain"),
            cpp is not null ? "Native compiler detected for Python/native save dependencies." : "Required for native Python dependencies such as pyooz.",
            cpp is not null ? "VERIFY" : "INSTALL", cpp is not null,
            cpp is null ? "BACKEND REQUIRED: compiler/toolchain installation must remain platform-owned and explicit." : null);

        var plm = FindFirstExisting(
            Path.Combine(paths.ServerRoot, "Tools", "palworld-plm-tools", "convert.py"),
            Path.Combine(paths.ServerRoot, "Tools", "palworld-plm-tools", ".myst-install.json"));
        Add(rows, "PlM/Oodle Decoder", plm is not null, plm ?? Path.Combine(paths.ServerRoot, "Tools", "palworld-plm-tools"),
            plm is not null ? "PlM/Oodle-capable save tooling detected." : "Required for newer PlM Level.sav containers.",
            plm is not null ? "VERIFY" : "INSTALL", plm is not null,
            plm is null ? "BACKEND REQUIRED: PlM/Oodle tooling installation has no safe current management API." : null);

        var ini = Path.Combine(paths.ConfigRoot, "PalWorldSettings.ini");
        Add(rows, "Default Server Settings", File.Exists(ini), ini,
            File.Exists(ini) ? "Active PalWorldSettings.ini detected." : "A valid default configuration has not been created.",
            File.Exists(ini) ? "VERIFY" : "CREATE", true);

        var text = SafeRead(ini);
        var restReady = text.Contains("RESTAPIEnabled=True", StringComparison.OrdinalIgnoreCase);
        rows.Add(new EnvironmentChecklistItem("REST API", restReady ? "READY" : "DISABLED", "PalWorldSettings.ini",
            restReady ? "Enabled in the active configuration." : "Not enabled in the active configuration.", restReady ? "VERIFY" : "ENABLE", true));

        var rconReady = text.Contains("RCONEnabled=True", StringComparison.OrdinalIgnoreCase);
        rows.Add(new EnvironmentChecklistItem("RCON", rconReady ? "READY" : "DISABLED", ini,
            rconReady ? "Enabled in the active configuration." : "Optional remote administration is disabled.", rconReady ? "VERIFY" : "ENABLE", true));

        Add(rows, "Backup Storage", Directory.Exists(paths.BackupRoot), paths.BackupRoot,
            Directory.Exists(paths.BackupRoot) ? "Backup folder is available." : "Create backup storage before relying on managed recovery.",
            Directory.Exists(paths.BackupRoot) ? "VERIFY" : "CREATE", Directory.Exists(paths.BackupRoot),
            Directory.Exists(paths.BackupRoot) ? null : "BACKEND REQUIRED: backup-root creation/validation must be performed by a server-side storage operation." );

        var ready = rows.Count(x => x.Status == "READY");
        return new EnvironmentChecklistSnapshot(ready, rows.Count, rows, DateTimeOffset.UtcNow);
    }

    private (string Status, string Detail) DetectUe4ss()
    {
        if (!Directory.Exists(paths.RuntimeBinaryRoot)) return ("MISSING", "Palworld runtime folder was not found.");
        var loaders = new[] { "dwmapi.dll", "xinput1_3.dll", "xinput1_4.dll", "winhttp.dll", "UE4SS.dll" };
        if (loaders.Any(x => File.Exists(Path.Combine(paths.RuntimeBinaryRoot, x)))) return ("READY", "UE4SS runtime loader detected.");
        if (loaders.Any(x => File.Exists(Path.Combine(paths.RuntimeBinaryRoot, x + ".myst-disabled")))) return ("DISABLED", "UE4SS runtime is installed but disabled.");
        if (Directory.Exists(paths.Ue4ssRoot)) return ("READY", "UE4SS runtime folder detected.");
        return ("MISSING", "Optional runtime for UE4SS-based server mods was not detected.");
    }

    private static string? DetectCppToolchain()
    {
        if (OperatingSystem.IsWindows())
        {
            var roots = new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) };
            foreach (var root in roots.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var vs = Path.Combine(root, "Microsoft Visual Studio", "2022");
                if (!Directory.Exists(vs)) continue;
                try
                {
                    var cl = Directory.EnumerateFiles(vs, "cl.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (cl is not null) return cl;
                }
                catch { }
            }
            return FindOnPath(new[] { "cl.exe" });
        }
        return FindOnPath(new[] { "g++", "gcc", "clang++" });
    }

    private static string? FindSteamRoot()
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam") }
            : new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".steam", "steam"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Steam") };
        return candidates.FirstOrDefault(x => Directory.Exists(x) && (File.Exists(Path.Combine(x, OperatingSystem.IsWindows() ? "steam.exe" : "steam.sh")) || Directory.Exists(Path.Combine(x, "steamapps"))));
    }

    private static string? FindPalworldClient(string? steamRoot)
    {
        if (steamRoot is null) return null;
        var libraries = new List<string> { steamRoot };
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
        {
            try
            {
                foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "\\\"path\\\"\\s+\\\"(?<p>[^\\\"]+)\\\""))
                {
                    var candidate = match.Groups["p"].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(candidate) && !libraries.Contains(candidate, StringComparer.OrdinalIgnoreCase)) libraries.Add(candidate);
                }
            }
            catch { }
        }
        foreach (var library in libraries)
        {
            var candidate = Path.Combine(library, "steamapps", "common", "Palworld");
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string? FindOnPath(IEnumerable<string> names)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            foreach (var name in names)
            {
                try { var full = Path.Combine(dir, name); if (File.Exists(full)) return full; } catch { }
            }
        return null;
    }

    private static string? FindFirstExisting(params string[] candidates) => candidates.FirstOrDefault(File.Exists);
    private static string SafeRead(string path) { try { return File.Exists(path) ? File.ReadAllText(path) : string.Empty; } catch { return string.Empty; } }
    private static void Add(List<EnvironmentChecklistItem> rows, string component, bool ready, string location, string details, string action, bool actionSupported, string? unavailableReason = null)
        => rows.Add(new(component, ready ? "READY" : "MISSING", location, details, action, actionSupported, unavailableReason));
}

public sealed record EnvironmentChecklistSnapshot(int ReadyCount, int TotalCount, IReadOnlyList<EnvironmentChecklistItem> Items, DateTimeOffset ObservedAt);
public sealed record EnvironmentChecklistItem(string Component, string Status, string Location, string Details, string Action, bool ActionSupported = true, string? UnavailableReason = null);
