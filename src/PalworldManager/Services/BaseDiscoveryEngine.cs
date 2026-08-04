using System.Globalization;
using System.Text.Json;
using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Read-only schema parser for BaseCampSaveData. Production bases are created
/// only from authoritative BaseCampSaveData map entries with a valid 32-digit
/// Palworld identifier. Broad whole-document base discovery is intentionally
/// not used because ownership/container fields create false base records.
/// </summary>
public sealed class BaseDiscoveryEngine
{
    public IReadOnlyList<BaseDiscoveryRecord> Discover(string jsonPath) =>
        DiscoverWithDiagnostics(jsonPath).Records;

    public BaseDiscoveryResult DiscoverWithDiagnostics(string jsonPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(jsonPath));
        var state = new DiscoveryState();
        FindRoots(document.RootElement, "$", state, 0);

        foreach (var root in state.Roots)
            ParseBaseCampSaveData(root.Element, root.Path, state);

        var records = state.Records.Values
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.BaseId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        state.Diagnostics.Add($"Authoritative BaseCampSaveData roots: {state.Roots.Count}");
        state.Diagnostics.Add($"BaseCampSaveData entries inspected: {state.EntriesInspected}");
        state.Diagnostics.Add($"Typed base entries accepted: {records.Count}");
        state.Diagnostics.Add($"Malformed base entries rejected: {state.EntriesRejected}");
        state.Diagnostics.Add("Base parser trace:");
        state.Diagnostics.AddRange(state.Trace.Take(100).Select(x => "  " + x));

        return new BaseDiscoveryResult
        {
            Records = records,
            Diagnostics = state.Diagnostics.Distinct().ToArray(),
            Rejections = state.Rejections.Distinct().Take(100).ToArray()
        };
    }

    public void Enrich(WorldSnapshot snapshot, IEnumerable<BaseDiscoveryRecord> records)
    {
        snapshot.Bases = records.Select(x => new WorldBaseRecord
        {
            BaseId = x.BaseId,
            GuildId = x.GuildId,
            DisplayName = string.IsNullOrWhiteSpace(x.Name) ? "Base " + Short(x.BaseId) : x.Name,
            X = x.X,
            Y = x.Y,
            Z = x.Z,
            Health = EntityHealth.Unresolved
        }).ToList();
    }

    internal static bool IsValidPalworldId(string? value)
    {
        var normalized = LiveWorldScanner.NormalizeId(value ?? string.Empty);
        return normalized.Length == 32 && normalized.All(Uri.IsHexDigit);
    }

    private static void FindRoots(JsonElement element, string path, DiscoveryState state, int depth)
    {
        if (depth > 80) return;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var childPath = path + "." + property.Name;
                if (NormalizeKey(property.Name) == "basecampsavedata")
                {
                    state.Roots.Add(new RootCandidate(property.Value, childPath));
                    continue;
                }
                FindRoots(property.Value, childPath, state, depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
                FindRoots(item, path + "[" + index++ + "]", state, depth + 1);
        }
    }

    private static void ParseBaseCampSaveData(JsonElement root, string rootPath, DiscoveryState state)
    {
        if (!TryGetArray(root, out var entries))
        {
            Reject(state, rootPath, "BaseCampSaveData did not contain its expected value array.");
            return;
        }

        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var entryPath = $"{rootPath}.value[{index++}]";
            state.EntriesInspected++;

            if (!TryGetObjectProperty(entry, "value", out var valueStruct))
            {
                Reject(state, entryPath, "map entry did not contain value.");
                continue;
            }

            var raw = UnwrapRawData(valueStruct);
            var keyId = TryGetObjectProperty(entry, "key", out var keyElement) ? Scalar(keyElement) : string.Empty;
            var rawId = ReadScalar(raw, "base_id", "base_camp_id", "basecamp_id", "id");
            var baseId = NormalizeId(IsValidPalworldId(rawId) ? rawId : keyId);

            if (!IsValidPalworldId(baseId))
            {
                Reject(state, entryPath, $"invalid Base ID '{FirstNonEmpty(rawId, keyId)}'.");
                continue;
            }

            var guildId = NormalizeId(ReadScalar(raw,
                "group_id_belong_to", "guild_id", "group_id", "owner_group_id", "owner_guild_id"));
            if (!string.IsNullOrWhiteSpace(guildId) && !IsValidPalworldId(guildId))
                guildId = string.Empty;

            var record = new BaseDiscoveryRecord
            {
                BaseId = baseId,
                GuildId = guildId,
                PalboxId = NormalizeValidId(ReadScalar(raw,
                    "map_object_instance_id_base_camp_point", "palbox_id", "map_object_id")),
                Name = ReadScalar(raw, "base_name", "display_name", "name"),
                InternalName = ReadScalar(raw, "base_name", "display_name", "name"),
                X = ReadNumber(raw, "x", "location_x", "pos_x"),
                Y = ReadNumber(raw, "y", "location_y", "pos_y"),
                Z = ReadNumber(raw, "z", "location_z", "pos_z"),
                SourcePath = entryPath + ".value.RawData.value"
            };

            state.Records[baseId] = record;
            state.Trace.Add($"{entryPath}: accepted Base ID={baseId}; Guild={Display(guildId)}; Palbox={Display(record.PalboxId)}");
        }
    }

    private static JsonElement UnwrapRawData(JsonElement valueStruct)
    {
        if (TryGetObjectProperty(valueStruct, "RawData", out var rawData) &&
            TryGetObjectProperty(rawData, "value", out var rawValue))
            return rawValue;
        return valueStruct;
    }

    private static bool TryGetArray(JsonElement element, out JsonElement array)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            array = element;
            return true;
        }
        if (TryGetObjectProperty(element, "value", out var value) && value.ValueKind == JsonValueKind.Array)
        {
            array = value;
            return true;
        }
        array = default;
        return false;
    }

    private static bool TryGetObjectProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var property in element.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
        value = default;
        return false;
    }

    private static string ReadScalar(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return string.Empty;
        foreach (var name in names)
            foreach (var property in element.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    var value = Scalar(property.Value);
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
        return string.Empty;
    }

    private static double ReadNumber(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out var primitive))
            return primitive;
        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(element.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out primitive))
            return primitive;

        var direct = ReadScalar(element, names);
        if (double.TryParse(direct, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            return number;

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var nested = ReadNumber(property.Value, names);
                if (nested != 0) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray())
            {
                var nested = ReadNumber(item, names);
                if (nested != 0) return nested;
            }
        return 0;
    }

    private static string Scalar(JsonElement element, int depth = 0)
    {
        if (depth > 10) return string.Empty;
        if (element.ValueKind == JsonValueKind.String) return element.GetString() ?? string.Empty;
        if (element.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) return element.ToString();
        if (element.ValueKind != JsonValueKind.Object) return string.Empty;
        foreach (var key in new[] { "value", "Value", "id", "ID", "guid", "Guid", "key", "Key" })
            if (TryGetObjectProperty(element, key, out var nested))
            {
                var value = Scalar(nested, depth + 1);
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
        return string.Empty;
    }

    private static void Reject(DiscoveryState state, string path, string reason)
    {
        state.EntriesRejected++;
        state.Rejections.Add($"{path}: {reason}");
        state.Trace.Add($"{path}: rejected — {reason}");
    }

    private static string NormalizeId(string? value) => LiveWorldScanner.NormalizeId(value ?? string.Empty);
    private static string NormalizeValidId(string? value) => IsValidPalworldId(value) ? NormalizeId(value) : string.Empty;
    private static string NormalizeKey(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static string Short(string value) => value[..Math.Min(8, value.Length)];
    private static string FirstNonEmpty(params string[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
    private static string Display(string value) => string.IsNullOrWhiteSpace(value) ? "unassigned" : value;

    private sealed class DiscoveryState
    {
        public List<RootCandidate> Roots { get; } = [];
        public Dictionary<string, BaseDiscoveryRecord> Records { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Diagnostics { get; } = [];
        public List<string> Rejections { get; } = [];
        public List<string> Trace { get; } = [];
        public int EntriesInspected { get; set; }
        public int EntriesRejected { get; set; }
    }

    private readonly record struct RootCandidate(JsonElement Element, string Path);
}
