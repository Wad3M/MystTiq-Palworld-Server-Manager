using PalworldManager.Models;

namespace PalworldManager.Services;

public sealed class PlayerHistoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly AppSettings settings;
    private readonly string filePath;
    private readonly object gate = new();
    private List<PlayerHistoryRecord> records;

    public PlayerHistoryService(string dataRoot, AppSettings settings)
    {
        this.settings = settings;
        var serverKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(settings.ServerRoot.Trim().ToUpperInvariant())))[..12];
        filePath = Path.Combine(dataRoot, $"players-{serverKey}.json");
        records = Load();
        foreach (var record in records)
            record.IsOnline = false;
    }

    public string FilePath => filePath;

    public IReadOnlyList<PlayerHistoryRecord> Snapshot()
    {
        lock (gate)
            return records.Select(Clone).ToList();
    }

    public int DiscoverWorldPlayerSaves() => DiscoverWorldPlayerSaves("");

    public int DiscoverWorldPlayerSaves(string? worldPath)
    {
        var discovered = 0;
        var searchRoot = !string.IsNullOrWhiteSpace(worldPath) && Directory.Exists(worldPath) ? worldPath : settings.SaveRoot;
        if (!Directory.Exists(searchRoot)) return 0;

        lock (gate)
        {
            // Palworld replaces save files and folders while writing. Snapshot the paths
            // and tolerate entries that disappear between enumeration and inspection.
            foreach (var playersDir in SafeEnumerateDirectories(searchRoot, "Players"))
            {
                foreach (var path in SafeEnumerateFiles(playersDir, "*.sav"))
                {
                    if (!IsStablePlayerSave(path)) continue;
                    var id = Path.GetFileNameWithoutExtension(path).Trim();
                    if (string.IsNullOrWhiteSpace(id) || id.EndsWith("_dps", StringComparison.OrdinalIgnoreCase)) continue;
                    if (records.Any(r => IdentifiersEqual(r.PlayerId, id))) continue;

                    DateTime stamp;
                    try
                    {
                        var info = new FileInfo(path);
                        if (!info.Exists) continue;
                        stamp = info.LastWriteTimeUtc > DateTime.UnixEpoch ? info.LastWriteTimeUtc : DateTime.UtcNow;
                    }
                    catch (IOException) { continue; }
                    catch (UnauthorizedAccessException) { continue; }

                    records.Add(new PlayerHistoryRecord
                    {
                        Key = "player:" + id.ToLowerInvariant(),
                        Name = "Unknown imported player",
                        PlayerId = id,
                        FirstSeenUtc = stamp,
                        LastSeenUtc = stamp,
                        Platform = "Unknown",
                        Source = "Imported save"
                    });
                    discovered++;
                }
            }
            if (discovered > 0) SaveLocked();
        }
        return discovered;
    }

    public void MergeOnline(IEnumerable<LivePlayerSnapshot> players)
    {
        var now = DateTime.UtcNow;
        lock (gate)
        {
            var previouslyOnlineKeys = records.Where(r => r.IsOnline).Select(r => r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in records)
                existing.IsOnline = false;

            foreach (var live in players)
            {
                var record = FindBestMatch(live);
                var wasOnline = record is not null && previouslyOnlineKeys.Contains(record.Key);
                if (record is null)
                {
                    record = new PlayerHistoryRecord
                    {
                        Key = BuildKey(live),
                        FirstSeenUtc = now,
                        ObservedSessions = 1,
                        Source = "REST"
                    };
                    records.Add(record);
                }
                else if (!wasOnline)
                {
                    record.ObservedSessions = Math.Max(1, record.ObservedSessions + 1);
                }

                if (!string.IsNullOrWhiteSpace(live.Name)) record.Name = live.Name;
                if (!string.IsNullOrWhiteSpace(live.UserId)) record.UserId = live.UserId;
                if (!string.IsNullOrWhiteSpace(live.SteamId)) record.SteamId = live.SteamId;
                if (!string.IsNullOrWhiteSpace(live.PlayerId)) record.PlayerId = live.PlayerId;
                if (!string.IsNullOrWhiteSpace(live.Ip)) record.Ip = live.Ip;
                if (!string.IsNullOrWhiteSpace(live.Ping)) record.Ping = live.Ping;
                if (!string.IsNullOrWhiteSpace(live.Platform)) record.Platform = live.Platform;
                if (!string.IsNullOrWhiteSpace(live.Level)) record.Level = live.Level;
                if (!string.IsNullOrWhiteSpace(live.BuildingCount)) record.BuildingCount = live.BuildingCount;
                record.LastSeenUtc = now;
                record.IsOnline = true;
                record.Key = BuildKey(live, record);
            }
            SaveLocked();
        }
    }

    public void MarkBanned(string key, bool banned)
    {
        lock (gate)
        {
            var record = records.FirstOrDefault(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (record is null) return;
            record.IsBanned = banned;
            SaveLocked();
        }
    }

    public void SaveNotes(string key, string notes)
    {
        lock (gate)
        {
            var record = records.FirstOrDefault(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (record is null) return;
            record.Notes = notes ?? "";
            SaveLocked();
        }
    }


    public bool Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        lock (gate)
        {
            var removed = records.RemoveAll(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed) SaveLocked();
            return removed;
        }
    }


    public int RemoveMatching(PlayerRow row)
    {
        lock (gate)
        {
            var removed = records.RemoveAll(r =>
                (!string.IsNullOrWhiteSpace(row.UserId) && r.UserId.Equals(row.UserId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(row.SteamId) && r.SteamId.Equals(row.SteamId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(row.PlayerId) && IdentifiersEqual(r.PlayerId, row.PlayerId)) ||
                (!string.IsNullOrWhiteSpace(row.SavePath) && IdentifiersEqual(r.PlayerId, Path.GetFileNameWithoutExtension(row.SavePath))));
            if (removed > 0) SaveLocked();
            return removed;
        }
    }

    private static bool IdentifiersEqual(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        return Normalize(left) == Normalize(right);
    }

    public string ResolveKey(PlayerRow row)
    {
        lock (gate)
        {
            var match = records.FirstOrDefault(r =>
                (!string.IsNullOrWhiteSpace(row.UserId) && r.UserId.Equals(row.UserId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(row.SteamId) && r.SteamId.Equals(row.SteamId, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(row.PlayerId) && r.PlayerId.Equals(row.PlayerId, StringComparison.OrdinalIgnoreCase)));
            return match?.Key ?? "";
        }
    }

    private PlayerHistoryRecord? FindBestMatch(LivePlayerSnapshot live)
    {
        return records.FirstOrDefault(r =>
            (!string.IsNullOrWhiteSpace(live.UserId) && r.UserId.Equals(live.UserId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(live.SteamId) && r.SteamId.Equals(live.SteamId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(live.PlayerId) && r.PlayerId.Equals(live.PlayerId, StringComparison.OrdinalIgnoreCase)));
    }

    private static string BuildKey(LivePlayerSnapshot live, PlayerHistoryRecord? fallback = null)
    {
        if (!string.IsNullOrWhiteSpace(live.UserId)) return "user:" + live.UserId.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(live.SteamId)) return "steam:" + live.SteamId.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(live.PlayerId)) return "player:" + live.PlayerId.Trim().ToLowerInvariant();
        return fallback?.Key ?? "unknown:" + Guid.NewGuid().ToString("N");
    }

    private List<PlayerHistoryRecord> Load()
    {
        try
        {
            if (!File.Exists(filePath)) return [];
            return JsonSerializer.Deserialize<List<PlayerHistoryRecord>>(File.ReadAllText(filePath), JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveLocked()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temp = filePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(records, JsonOptions));
        File.Move(temp, filePath, true);
    }

    private static PlayerHistoryRecord Clone(PlayerHistoryRecord r) => new()
    {
        Key = r.Key, Name = r.Name, UserId = r.UserId, SteamId = r.SteamId, PlayerId = r.PlayerId,
        Ip = r.Ip, Ping = r.Ping, Platform = r.Platform, Level = r.Level, BuildingCount = r.BuildingCount,
        FirstSeenUtc = r.FirstSeenUtc, LastSeenUtc = r.LastSeenUtc, ObservedSessions = r.ObservedSessions,
        IsOnline = r.IsOnline, IsBanned = r.IsBanned, Notes = r.Notes, Source = r.Source
    };
    private static IReadOnlyList<string> SafeEnumerateDirectories(string root, string name)
    {
        try { return Directory.EnumerateDirectories(root, name, SearchOption.AllDirectories).ToList(); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    private static IReadOnlyList<string> SafeEnumerateFiles(string root, string pattern)
    {
        try { return Directory.EnumerateFiles(root, pattern, SearchOption.TopDirectoryOnly).ToList(); }
        catch (IOException) { return []; }
        catch (UnauthorizedAccessException) { return []; }
    }

    private static bool IsStablePlayerSave(string path)
    {
        var name = Path.GetFileName(path);
        return name.EndsWith(".sav", StringComparison.OrdinalIgnoreCase)
            && !name.Contains(".TMP", StringComparison.OrdinalIgnoreCase)
            && !name.Contains('~');
    }

}
