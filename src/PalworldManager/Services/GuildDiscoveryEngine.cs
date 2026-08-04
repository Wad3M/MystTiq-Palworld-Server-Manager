using System.Text.Json;
using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Read-only schema parser for Palworld GroupSaveDataMap.
/// Production guilds are created only from decoded GroupSaveDataMap entries
/// whose Value.GroupType is EPalGroupType::Guild. RawData.value is parsed by
/// its known schema; broad recursive discovery is retained for diagnostics only.
/// </summary>
public sealed class GuildDiscoveryEngine
{
    private const string GuildType = "EPalGroupType::Guild";

    public IReadOnlyList<GuildDiscoveryRecord> Discover(string jsonPath) =>
        DiscoverWithDiagnostics(jsonPath).Records;

    public GuildDiscoveryResult DiscoverWithDiagnostics(string jsonPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var state = new DiscoveryState();

        FindAuthoritativeRoots(document.RootElement, "$", state, 0);
        foreach (var root in state.Roots)
            ParseGroupSaveDataMap(root.Element, root.Path, state);

        var records = state.Records.Values
            .Select(FinalizeRecord)
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.GuildId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        state.Diagnostics.Add($"Authoritative GroupSaveDataMap roots: {state.Roots.Count}");
        state.Diagnostics.Add($"GroupSaveDataMap entries inspected: {state.EntriesInspected}");
        state.Diagnostics.Add($"Typed Guild entries accepted: {records.Count}");
        state.Diagnostics.Add($"Non-guild group entries ignored: {state.NonGuildEntries}");
        state.Diagnostics.Add($"Malformed Guild entries rejected: {state.EntriesRejected}");
        state.Diagnostics.Add($"Guild members decoded: {records.Sum(x => x.MemberGuids.Count)}");
        state.Diagnostics.Add($"Guild base references decoded: {records.Sum(x => x.BaseIds.Count)}");

        if (state.Roots.Count == 0)
        {
            state.Diagnostics.Add("No authoritative GroupSaveDataMap property was found. Heuristic paths below are diagnostic only.");
            FindDiagnosticGroupPaths(document.RootElement, "$", state, 0);
        }

        state.Diagnostics.Add("Guild parser trace:");
        state.Diagnostics.AddRange(state.Trace.Take(150).Select(x => "  " + x));

        return new GuildDiscoveryResult
        {
            Records = records,
            Diagnostics = state.Diagnostics.Distinct().ToArray(),
            CandidatePaths = state.CandidatePaths.Distinct().Take(250).ToArray(),
            Rejections = state.Rejections.Distinct().Take(100).ToArray()
        };
    }

    public void Enrich(WorldSnapshot snapshot, IEnumerable<GuildDiscoveryRecord> records)
    {
        snapshot.Guilds = records.Select(x => new WorldGuildRecord
        {
            GuildId = x.GuildId,
            GuildName = string.IsNullOrWhiteSpace(x.Name) ? "Unnamed Guild" : x.Name,
            LeaderPlayerGuid = x.LeaderGuid,
            MemberPlayerGuids = x.MemberGuids.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            BaseIds = x.BaseIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Health = EntityHealth.Unresolved
        }).ToList();

        foreach (var player in snapshot.Players)
        {
            var playerId = LiveWorldScanner.NormalizeId(player.PlayerGuid);
            var guild = snapshot.Guilds.FirstOrDefault(g =>
                g.MemberPlayerGuids.Any(member => LiveWorldScanner.NormalizeId(member) == playerId));
            if (guild is not null) player.GuildId = guild.GuildId;
        }
    }

    private static void FindAuthoritativeRoots(JsonElement element, string path, DiscoveryState state, int depth)
    {
        if (depth > 80) return;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = path + "." + property.Name;
                if (NormalizeKey(property.Name) == "groupsavedatamap")
                {
                    state.Roots.Add(new RootCandidate(property.Value, propertyPath));
                    state.CandidatePaths.Add("Authoritative root: " + propertyPath);
                    continue;
                }
                FindAuthoritativeRoots(property.Value, propertyPath, state, depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
                FindAuthoritativeRoots(item, path + "[" + index++ + "]", state, depth + 1);
        }
    }

    private static void ParseGroupSaveDataMap(JsonElement root, string rootPath, DiscoveryState state)
    {
        if (!TryGetMapEntries(root, out var entries))
        {
            state.Rejections.Add($"{rootPath}: GroupSaveDataMap did not contain its expected value array.");
            return;
        }

        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var entryPath = $"{rootPath}.value[{index++}]";
            state.EntriesInspected++;

            if (!TryGetObjectProperty(entry, "value", out var groupStruct))
            {
                Reject(state, entryPath, "map entry did not contain a Value struct");
                continue;
            }

            var groupType = ReadPropertyScalar(groupStruct, "GroupType");
            state.Trace.Add($"{entryPath}: GroupType={Display(groupType)}");
            if (!string.Equals(groupType, GuildType, StringComparison.OrdinalIgnoreCase))
            {
                state.NonGuildEntries++;
                state.Trace.Add($"{entryPath}: ignored non-guild group");
                continue;
            }

            if (!TryGetObjectProperty(groupStruct, "RawData", out var rawDataProperty) ||
                !TryUnwrapValue(rawDataProperty, out var rawData) ||
                rawData.ValueKind != JsonValueKind.Object)
            {
                Reject(state, entryPath, "Guild RawData.value was not a decoded object");
                continue;
            }

            TryParseGuildRawData(rawData, entryPath + ".value.RawData.value", state);
        }
    }

    private static void TryParseGuildRawData(JsonElement rawData, string path, DiscoveryState state)
    {
        var rawType = ReadDirectScalar(rawData, "group_type");
        if (!string.IsNullOrWhiteSpace(rawType) &&
            !string.Equals(rawType, GuildType, StringComparison.OrdinalIgnoreCase))
        {
            Reject(state, path, $"RawData group_type was {rawType}, not {GuildType}");
            return;
        }

        var id = NormalizeGuidLike(ReadDirectScalar(rawData, "group_id"));
        var guildName = CleanName(ReadDirectScalar(rawData, "guild_name"));
        var groupName = CleanName(ReadDirectScalar(rawData, "group_name"));
        var name = !string.IsNullOrWhiteSpace(guildName) ? guildName : groupName;
        var leader = NormalizeGuidLike(ReadDirectScalar(rawData, "admin_player_uid"));
        var bases = ReadGuidArray(rawData, "base_ids");
        var members = new List<string>();
        var memberNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (TryGetDirectProperty(rawData, "players", out var players) && players.ValueKind == JsonValueKind.Array)
        {
            foreach (var player in players.EnumerateArray())
            {
                if (player.ValueKind != JsonValueKind.Object) continue;
                var uid = NormalizeGuidLike(ReadDirectScalar(player, "player_uid"));
                if (!LiveWorldScanner.LooksLikeId(uid)) continue;

                members.Add(uid);
                if (TryGetDirectProperty(player, "player_info", out var playerInfo) && playerInfo.ValueKind == JsonValueKind.Object)
                {
                    var playerName = CleanName(ReadDirectScalar(playerInfo, "player_name"));
                    if (!string.IsNullOrWhiteSpace(playerName)) memberNames[uid] = playerName;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(leader) && !members.Contains(leader, StringComparer.OrdinalIgnoreCase))
            members.Insert(0, leader);

        if (!LiveWorldScanner.LooksLikeId(id))
        {
            Reject(state, path, "Guild RawData did not contain a valid group_id");
            return;
        }

        // A decoded Guild should normally have an admin or players. Preserve an
        // empty named guild for diagnostics/recovery, but reject empty unnamed data.
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(leader) && members.Count == 0)
        {
            Reject(state, path, "Guild RawData had no name, admin, or players");
            return;
        }

        var candidate = new GuildDiscoveryRecord
        {
            GuildId = id,
            Name = name,
            LeaderGuid = leader,
            MemberGuids = members,
            MemberNames = memberNames,
            BaseIds = bases,
            SourcePath = path,
            EvidenceScore = 100,
            HasExplicitGuildType = true
        };

        state.CandidatePaths.Add($"Typed Guild: {path} (ID={id}, Name={Display(name)}, Members={members.Count}, Bases={bases.Count})");
        state.Trace.Add($"{path}: accepted Guild ID={id}; Name={Display(name)}; Leader={Display(leader)}; Members={members.Count}; Bases={bases.Count}");

        if (state.Records.TryGetValue(id, out var existing)) Merge(existing, candidate);
        else state.Records[id] = candidate;
    }

    private static bool TryGetMapEntries(JsonElement root, out JsonElement entries)
    {
        entries = default;
        if (root.ValueKind == JsonValueKind.Array)
        {
            entries = root;
            return true;
        }

        if (root.ValueKind != JsonValueKind.Object) return false;
        if (TryGetDirectProperty(root, "value", out var value))
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                entries = value;
                return true;
            }
            if (value.ValueKind == JsonValueKind.Object && TryGetDirectProperty(value, "values", out var values) && values.ValueKind == JsonValueKind.Array)
            {
                entries = values;
                return true;
            }
        }
        return false;
    }

    private static string ReadPropertyScalar(JsonElement parent, string propertyName)
    {
        if (!TryGetObjectProperty(parent, propertyName, out var property)) return string.Empty;
        return TryUnwrapValue(property, out var value) ? Scalar(value) : Scalar(property);
    }

    private static string ReadDirectScalar(JsonElement parent, string propertyName)
    {
        if (!TryGetDirectProperty(parent, propertyName, out var value)) return string.Empty;
        return Scalar(value);
    }

    private static List<string> ReadGuidArray(JsonElement parent, string propertyName)
    {
        if (!TryGetDirectProperty(parent, propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            return [];

        return array.EnumerateArray()
            .Select(Scalar)
            .Select(NormalizeGuidLike)
            .Where(LiveWorldScanner.LooksLikeId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryGetObjectProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object) return false;
        return TryGetDirectProperty(element, propertyName, out value);
    }

    private static bool TryGetDirectProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object) return false;
        var normalized = NormalizeKey(propertyName);
        foreach (var property in element.EnumerateObject())
        {
            if (NormalizeKey(property.Name) == normalized)
            {
                value = property.Value;
                return true;
            }
        }
        return false;
    }

    private static bool TryUnwrapValue(JsonElement element, out JsonElement value)
    {
        value = element;
        for (var depth = 0; depth < 8; depth++)
        {
            if (value.ValueKind != JsonValueKind.Object) return true;
            if (!TryGetDirectProperty(value, "value", out var nested)) return true;
            value = nested;
        }
        return true;
    }

    private static string Scalar(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) return element.GetString() ?? string.Empty;
        if (element.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) return element.ToString();
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (TryGetDirectProperty(element, "value", out var value)) return Scalar(value);
            if (TryGetDirectProperty(element, "id", out var id)) return Scalar(id);
            if (TryGetDirectProperty(element, "guid", out var guid)) return Scalar(guid);
        }
        return string.Empty;
    }

    private static GuildDiscoveryRecord FinalizeRecord(GuildDiscoveryRecord record)
    {
        record.LeaderGuid = NormalizeGuidLike(record.LeaderGuid);
        record.MemberGuids = record.MemberGuids.Select(NormalizeGuidLike)
            .Where(LiveWorldScanner.LooksLikeId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        record.BaseIds = record.BaseIds.Select(NormalizeGuidLike)
            .Where(LiveWorldScanner.LooksLikeId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        record.MemberNames = record.MemberNames
            .Where(kv => record.MemberGuids.Contains(NormalizeGuidLike(kv.Key), StringComparer.OrdinalIgnoreCase))
            .GroupBy(kv => NormalizeGuidLike(kv.Key), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        return record;
    }

    private static void Merge(GuildDiscoveryRecord target, GuildDiscoveryRecord source)
    {
        if (string.IsNullOrWhiteSpace(target.Name) && !string.IsNullOrWhiteSpace(source.Name)) target.Name = source.Name;
        if (string.IsNullOrWhiteSpace(target.LeaderGuid) && !string.IsNullOrWhiteSpace(source.LeaderGuid)) target.LeaderGuid = source.LeaderGuid;
        target.MemberGuids.AddRange(source.MemberGuids);
        target.BaseIds.AddRange(source.BaseIds);
        foreach (var pair in source.MemberNames)
            if (!string.IsNullOrWhiteSpace(pair.Value)) target.MemberNames[pair.Key] = pair.Value;
        target.MemberGuids = target.MemberGuids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        target.BaseIds = target.BaseIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void Reject(DiscoveryState state, string path, string reason)
    {
        state.EntriesRejected++;
        state.Rejections.Add($"{path}: {reason}.");
        state.Trace.Add($"{path}: rejected - {reason}");
    }

    private static void FindDiagnosticGroupPaths(JsonElement element, string path, DiscoveryState state, int depth)
    {
        if (depth > 40 || state.CandidatePaths.Count >= 250) return;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = path + "." + property.Name;
                if (NormalizeKey(property.Name).Contains("group", StringComparison.Ordinal))
                    state.CandidatePaths.Add("Diagnostic only: " + propertyPath);
                FindDiagnosticGroupPaths(property.Value, propertyPath, state, depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
                FindDiagnosticGroupPaths(item, path + "[" + index++ + "]", state, depth + 1);
        }
    }

    private static string NormalizeKey(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static string NormalizeGuidLike(string value) => LiveWorldScanner.NormalizeId(value);

    private static string CleanName(string value)
    {
        value = value?.Trim() ?? string.Empty;
        return value.Length > 128 ? value[..128] : value;
    }

    private static string Display(string value) => string.IsNullOrWhiteSpace(value) ? "<empty>" : value;

    private sealed class DiscoveryState
    {
        public List<RootCandidate> Roots { get; } = [];
        public Dictionary<string, GuildDiscoveryRecord> Records { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Diagnostics { get; } = [];
        public List<string> CandidatePaths { get; } = [];
        public List<string> Rejections { get; } = [];
        public List<string> Trace { get; } = [];
        public int EntriesInspected { get; set; }
        public int NonGuildEntries { get; set; }
        public int EntriesRejected { get; set; }
    }

    private readonly record struct RootCandidate(JsonElement Element, string Path);
}
