using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MystTiq.Core.Services;

// v0.8.18.0: bandwidth. The Palworld server's own network limits live in its Engine.ini, section
// [/Script/OnlineSubsystemUtils.IpNetDriver]: MaxClientRate / MaxInternetClientRate (bytes per second, per player) and
// NetServerMaxTickRate (network updates per second). MystTiq keeps its own policy per server and writes those three keys
// just before every start (see EngineNetworkSettings.ApplyBeforeStart), because the engine rewrites Engine.ini from memory
// when it exits, so an edit made while it runs would be lost. Every other line of Engine.ini is left as it is.
[JsonConverter(typeof(JsonStringEnumConverter<ServerNetworkMode>))]
public enum ServerNetworkMode { GameDefault, Custom }

public sealed record ServerNetworkPolicy(
    ServerNetworkMode Mode = ServerNetworkMode.GameDefault,
    double PerPlayerMbps = 8,
    int TickRate = 60,
    double? UploadBudgetMbps = null)
{
    public const double MinimumPerPlayerMbps = 0.25;
    public const double MaximumPerPlayerMbps = 100;
    public const int MinimumTickRate = 10;
    public const int MaximumTickRate = 120;

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(Mode)) errors.Add("Unknown bandwidth mode.");
        if (double.IsNaN(PerPlayerMbps) || PerPlayerMbps is < MinimumPerPlayerMbps or > MaximumPerPlayerMbps)
            errors.Add($"The limit per player must be between {MinimumPerPlayerMbps} and {MaximumPerPlayerMbps} Mbit/s.");
        if (TickRate is < MinimumTickRate or > MaximumTickRate)
            errors.Add($"The network update rate must be between {MinimumTickRate} and {MaximumTickRate} per second.");
        if (UploadBudgetMbps is { } budget && (double.IsNaN(budget) || budget is < 1 or > 100_000))
            errors.Add("The upload speed must be between 1 and 100000 Mbit/s.");
        return errors;
    }

    // Bytes per second, as the engine counts them.
    public long PerPlayerBytesPerSecond => (long)Math.Round(PerPlayerMbps * 1_000_000 / 8);
}

// What the game itself ships, read from the Palworld pak for v0.8.18.0 (game v1.0.4): Pal/Config/DefaultEngine.ini sets
// MaxClientRate = MaxInternetClientRate = 8000000 bytes/s (64 Mbit/s) and NetServerMaxTickRate = 60, and
// Pal/Config/LinuxServer/LinuxServerEngine.ini lowers the tick rate to 20 for the Linux server. (The Unreal engine's own
// BaseEngine.ini would be 100000 bytes/s and 30; widely copied hosting advice of MaxClientRate=100000 therefore cuts the
// per-player limit to about 0.8 Mbit/s.)
public static class PalworldNetworkDefaults
{
    public const long PerPlayerBytesPerSecond = 8_000_000;
    public static double PerPlayerMbps => PerPlayerBytesPerSecond * 8d / 1_000_000;
    public static int TickRate(bool linux) => linux ? 20 : 60;
}

// The upload a server can need at most, and a per-player limit that fits a connection. Worst case means every player
// receiving at their full limit at once; real play usually needs far less, but the limit is what keeps a full server
// from saturating the upload.
public static class BandwidthPlanner
{
    // Leave a fifth of the upload for everything else on the connection.
    public const double Headroom = 0.8;

    public static double WorstCaseMbps(int players, double perPlayerMbps) => Math.Max(0, players) * Math.Max(0, perPlayerMbps);

    // The largest quarter-Mbit/s step that fits players x limit into the upload with headroom, within the allowed range.
    public static double FitPerPlayerMbps(double uploadMbps, int players)
    {
        if (players <= 0 || uploadMbps <= 0) return ServerNetworkPolicy.MaximumPerPlayerMbps;
        var fit = Math.Floor(uploadMbps * Headroom / players * 4) / 4;
        return Math.Clamp(fit, ServerNetworkPolicy.MinimumPerPlayerMbps, ServerNetworkPolicy.MaximumPerPlayerMbps);
    }
}

public readonly record struct EngineNetworkValues(long? MaxClientRate, long? MaxInternetClientRate, int? NetServerMaxTickRate)
{
    public bool Any => MaxClientRate is not null || MaxInternetClientRate is not null || NetServerMaxTickRate is not null;
}

// Pure text edits of Engine.ini, so the logic harness can check them without a server.
public static class EngineIniNetworkSection
{
    public const string Section = "[/Script/OnlineSubsystemUtils.IpNetDriver]";
    private static readonly string[] Keys = ["MaxClientRate", "MaxInternetClientRate", "NetServerMaxTickRate"];

    // The values the file sets now (the last one wins, as in the engine); null when a key is not set.
    public static EngineNetworkValues Read(string text)
    {
        long? client = null, internet = null; int? tick = null;
        foreach (var (key, value) in KeyValues(text))
        {
            if (Eq(key, "MaxClientRate") && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c)) client = c;
            else if (Eq(key, "MaxInternetClientRate") && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) internet = i;
            else if (Eq(key, "NetServerMaxTickRate") && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var t)) tick = t;
        }
        return new EngineNetworkValues(client, internet, tick);
    }

    // The file with MystTiq's three keys set (Custom) or removed (GameDefault). Every other line, section and line ending
    // is kept; the section is matched without regard to case (guides often write it in lower case) and, if missing, added.
    public static string Apply(string text, ServerNetworkPolicy policy)
    {
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Length == 0 ? new List<string>() : text.Replace("\r\n", "\n").Split('\n').ToList();
        var trailingNewline = lines.Count > 0 && lines[^1].Length == 0;
        if (trailingNewline) lines.RemoveAt(lines.Count - 1);

        // Remove the three keys from every matching section, remembering where the first section's keys go.
        int? insertAt = null;
        var inSection = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                if (inSection && insertAt is null) insertAt = LastContentLine(lines, i) + 1;
                inSection = string.Equals(trimmed, Section, StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (inSection && IsOurKey(trimmed)) { lines.RemoveAt(i); i--; }
        }
        if (inSection && insertAt is null) insertAt = LastContentLine(lines, lines.Count) + 1;

        if (policy.Mode == ServerNetworkMode.Custom)
        {
            var values = new[]
            {
                $"MaxClientRate={policy.PerPlayerBytesPerSecond.ToString(CultureInfo.InvariantCulture)}",
                $"MaxInternetClientRate={policy.PerPlayerBytesPerSecond.ToString(CultureInfo.InvariantCulture)}",
                $"NetServerMaxTickRate={policy.TickRate.ToString(CultureInfo.InvariantCulture)}",
            };
            if (insertAt is { } at) lines.InsertRange(at, values);
            else
            {
                if (lines.Count > 0 && lines[^1].Trim().Length > 0) lines.Add(string.Empty);
                lines.Add(Section);
                lines.AddRange(values);
            }
        }
        var result = string.Join(newline, lines);
        return trailingNewline || (policy.Mode == ServerNetworkMode.Custom && insertAt is null) ? result + newline : result;
    }

    private static int LastContentLine(List<string> lines, int before)
    {
        var i = before - 1;
        while (i >= 0 && lines[i].Trim().Length == 0) i--;
        return i;
    }

    private static bool IsOurKey(string trimmed)
    {
        var eq = trimmed.IndexOf('=');
        return eq > 0 && Keys.Any(k => Eq(trimmed[..eq].Trim(), k));
    }

    private static IEnumerable<(string Key, string Value)> KeyValues(string text)
    {
        var inSection = false;
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']')) { inSection = string.Equals(line, Section, StringComparison.OrdinalIgnoreCase); continue; }
            var eq = line.IndexOf('=');
            if (inSection && eq > 0) yield return (line[..eq].Trim(), line[(eq + 1)..].Trim());
        }
    }

    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

// The file side: the policy is kept in the server's runtime folder and written into Engine.ini before every start.
public static class EngineNetworkSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static string PolicyPath(IServerPathProfile paths) => Path.Combine(paths.ManagerRuntimeRoot, "network-policy.json");
    public static string EngineIniPath(IServerPathProfile paths) => Path.Combine(paths.ConfigRoot, "Engine.ini");
    public static string OriginalBackupPath(IServerPathProfile paths) => EngineIniPath(paths) + ".mysttiq-original";

    public static ServerNetworkPolicy LoadPolicy(IServerPathProfile paths)
    {
        try
        {
            var path = PolicyPath(paths);
            if (!File.Exists(path)) return new ServerNetworkPolicy();
            var loaded = JsonSerializer.Deserialize<ServerNetworkPolicy>(File.ReadAllText(path), JsonOptions);
            return loaded is not null && loaded.Validate().Count == 0 ? loaded : new ServerNetworkPolicy();
        }
        catch { return new ServerNetworkPolicy(); }
    }

    public static void SavePolicy(IServerPathProfile paths, ServerNetworkPolicy policy)
    {
        Directory.CreateDirectory(paths.ManagerRuntimeRoot);
        var path = PolicyPath(paths);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(policy, JsonOptions));
        File.Move(path + ".tmp", path, overwrite: true);
    }

    public static EngineNetworkValues ReadEngineIni(IServerPathProfile paths)
    {
        try { var path = EngineIniPath(paths); return File.Exists(path) ? EngineIniNetworkSection.Read(File.ReadAllText(path)) : default; }
        catch { return default; }
    }

    // Writes the stored policy into Engine.ini when the file does not already say it. Returns a line for the console log,
    // or null when nothing changed. Never throws: a server must still start if its Engine.ini cannot be written.
    public static string? ApplyBeforeStart(IServerPathProfile paths)
    {
        try
        {
            var policy = LoadPolicy(paths);
            var enginePath = EngineIniPath(paths);
            var exists = File.Exists(enginePath);
            if (!exists && policy.Mode == ServerNetworkMode.GameDefault) return null;
            var before = exists ? File.ReadAllText(enginePath) : string.Empty;
            var after = EngineIniNetworkSection.Apply(before, policy);
            if (string.Equals(before, after, StringComparison.Ordinal)) return null;
            Directory.CreateDirectory(paths.ConfigRoot);
            // The Engine.ini as it was before MystTiq first changed it, kept once.
            if (exists && !File.Exists(OriginalBackupPath(paths))) File.Copy(enginePath, OriginalBackupPath(paths));
            File.WriteAllText(enginePath + ".tmp", after, new UTF8Encoding(false));
            File.Move(enginePath + ".tmp", enginePath, overwrite: true);
            return policy.Mode == ServerNetworkMode.Custom
                ? $"Bandwidth: Engine.ini set to {policy.PerPlayerMbps.ToString("0.##", CultureInfo.InvariantCulture)} Mbit/s per player and {policy.TickRate} network updates per second."
                : "Bandwidth: MystTiq's limits removed from Engine.ini; the game's defaults apply.";
        }
        catch (Exception ex)
        {
            return "Bandwidth: Engine.ini could not be written, the server starts with what it has: " + ex.Message;
        }
    }
}
