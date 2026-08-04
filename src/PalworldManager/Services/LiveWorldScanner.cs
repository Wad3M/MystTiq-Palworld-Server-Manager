using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class LiveWorldScanner
{
    private static readonly string[] PlayerKeys = ["player_uid", "player_guid", "playerguid", "individualid"];
    private static readonly string[] GuildKeys = ["guild_id", "guildid", "group_id", "groupid"];
    private static readonly string[] BaseKeys = ["base_id", "baseid", "basecampid", "base_camp_id"];

    public LiveWorldScanResult Scan(string jsonPath, IEnumerable<string> playerSavePaths)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var result = new LiveWorldScanResult();
        result.Snapshot.WorldName = Path.GetFileName(Path.GetDirectoryName(jsonPath)) ?? "Imported World";
        SeedPlayerFiles(result.Snapshot, playerSavePaths);
        Visit(document.RootElement, "$", result);
        MergePlayers(result.Snapshot);
        result.Statistics.CandidatePlayers = result.Snapshot.Players.Count;
        result.Statistics.CandidateGuilds = result.Snapshot.Guilds.Count;
        result.Statistics.CandidateBases = result.Snapshot.Bases.Count;
        result.Diagnostics.Add($"Visited {result.Statistics.JsonObjectsVisited:N0} objects and {result.Statistics.JsonArraysVisited:N0} arrays.");
        return result;
    }

    private static void SeedPlayerFiles(WorldSnapshot snapshot, IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(p => p.EndsWith(".sav", StringComparison.OrdinalIgnoreCase) && !p.EndsWith("_dps.sav", StringComparison.OrdinalIgnoreCase)))
        {
            var id = NormalizeId(Path.GetFileNameWithoutExtension(path));
            snapshot.Players.Add(new WorldPlayerRecord { PlayerGuid=id, SaveFilePath=path, IsHostCandidate=id.Equals("00000000000000000000000000000001", StringComparison.OrdinalIgnoreCase), Health=EntityHealth.Unresolved });
        }
    }

    private static void Visit(JsonElement element, string path, LiveWorldScanResult result)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            result.Statistics.JsonObjectsVisited++;
            var props = element.EnumerateObject().ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
            var playerId = FindScalar(props, PlayerKeys);
            if (LooksLikeId(playerId)) result.Snapshot.Players.Add(new WorldPlayerRecord { PlayerGuid=NormalizeId(playerId), PlayerName=FindScalar(props,["player_name","nickname","name"]), PlatformId=FindScalar(props,["platform_id","steam_id","account_id"]), Health=EntityHealth.Unresolved });
            foreach (var property in props) Visit(property.Value, path + "." + property.Key, result);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            result.Statistics.JsonArraysVisited++;
            var i=0; foreach (var item in element.EnumerateArray()) Visit(item, path + "[" + i++ + "]", result);
        }
    }

    internal static string FindScalar(Dictionary<string,JsonElement> props, IEnumerable<string> keys)
    {
        foreach (var key in keys)
            if (props.TryGetValue(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? "";
                if (value.ValueKind == JsonValueKind.Number || value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) return value.ToString();
                var nested = FindNestedScalar(value);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        return "";
    }
    private static string FindNestedScalar(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) return "";
        foreach (var name in new[] { "value", "Value", "id", "ID", "Guid", "guid" })
            if (value.TryGetProperty(name, out var nested) && nested.ValueKind == JsonValueKind.String) return nested.GetString() ?? "";
        return "";
    }
    internal static string NormalizeId(string value) => Regex.Replace(value ?? "", "[^A-Fa-f0-9]", "").ToUpperInvariant();
    internal static bool LooksLikeId(string value) => NormalizeId(value).Length >= 16;
    private static void MergePlayers(WorldSnapshot snapshot)
    {
        snapshot.Players = snapshot.Players.GroupBy(x=>x.PlayerGuid,StringComparer.OrdinalIgnoreCase).Select(g=>
        {
            var records=g.ToList(); var file=records.FirstOrDefault(x=>!string.IsNullOrWhiteSpace(x.SaveFilePath)); var named=records.FirstOrDefault(x=>!string.IsNullOrWhiteSpace(x.PlayerName));
            return new WorldPlayerRecord { PlayerGuid=g.Key, SaveFilePath=file?.SaveFilePath??"", PlayerName=named?.PlayerName??"", PlatformId=records.Select(x=>x.PlatformId).FirstOrDefault(x=>!string.IsNullOrWhiteSpace(x))??"", IsHostCandidate=records.Any(x=>x.IsHostCandidate), Health=EntityHealth.Unresolved };
        }).ToList();
    }
}
