using System.Globalization;
using System.Text.Json;

namespace MystTiq.HeadlessHost;

// v0.8.11.0: where owned Pals are, read from the decoded Level.sav.json the guild/base explorer already parses. Wild
// Pals are not in the save.
//
// A Pal's LastJumpedLocation only means "it is here" for Pals that are out in the world, so every Pal is classified by
// the container its SlotId points at:
//   - a base's worker container (BaseCampSaveData ... WorkerDirector container_id): working at that base;
//   - a small container (the 5-slot party): travelling with its owner, at the last spot the save recorded;
//   - a large container (the 960-slot Palbox): stored. Its LastJumpedLocation is wherever it was last let out, which
//     on the real save is up to a kilometre from anything, so it is counted but never drawn.
// Positions the game writes for "nowhere" are dropped the same way as for players: 0,0, a placeholder altitude, and
// spots within 20 m of the world origin (seen on the real save for Pals that were never placed).
public sealed record SavedPalLocation(
    string InstanceId, string Species, bool IsAlpha, int Level, string NickName, string OwnerPlayerId,
    string Placement, string BaseId, double X, double Y, double Z);

public sealed record SavedPalLocations(
    IReadOnlyList<SavedPalLocation> OnMap, int TotalPals, int InPalbox, int WithoutPosition, int Unplaced)
{
    public static readonly SavedPalLocations Empty = new([], 0, 0, 0, 0);
}

public static class SavePalLocationReader
{
    public const string BaseWorker = "BaseWorker";
    public const string Party = "Party";
    private const string Palbox = "Palbox";

    private const double PlaceholderAltitude = 900000;
    private const double OriginRadius = 2000;
    // The party holds 5; the Palbox 960. Anything up to this many slots is carried by its owner.
    private const int LargestCarriedContainer = 10;
    private const string AlphaPrefix = "BOSS_";

    public static SavedPalLocations Read(JsonElement root)
    {
        var workerContainers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var containerSlots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var characterMaps = new List<JsonElement>();
        FindRoots(root, workerContainers, containerSlots, characterMaps, 0);

        var onMap = new List<SavedPalLocation>();
        int total = 0, inPalbox = 0, withoutPosition = 0, unplaced = 0;
        foreach (var map in characterMaps)
        {
            var entries = map;
            if (entries.ValueKind == JsonValueKind.Object && entries.TryGetProperty("value", out var inner)) entries = inner;
            if (entries.ValueKind != JsonValueKind.Array) continue;

            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object || !TryFindExact(entry, "SaveParameter", 0, out var parameter)) continue;
                if (TryFindExact(parameter, "IsPlayer", 0, out var isPlayer) && ReadBool(isPlayer)) continue;
                if (!TryFindExact(parameter, "CharacterID", 0, out var characterId)) continue;
                var rawSpecies = ReadScalar(characterId).Trim();
                if (rawSpecies.Length == 0) continue;
                total++;

                var container = TryFindExact(parameter, "SlotId", 0, out var slot) && TryFindExact(slot, "ID", 0, out var containerId)
                    ? ReadScalar(containerId) : string.Empty;
                string placement;
                var baseId = string.Empty;
                if (container.Length > 0 && workerContainers.TryGetValue(container, out var owningBase)) { placement = BaseWorker; baseId = owningBase; }
                else if (container.Length > 0 && containerSlots.TryGetValue(container, out var slots))
                    placement = slots <= LargestCarriedContainer ? Party : Palbox;
                else { unplaced++; continue; }

                if (placement == Palbox) { inPalbox++; continue; }
                if (!TryReadPosition(parameter, out var x, out var y, out var z)) { withoutPosition++; continue; }

                var isAlpha = rawSpecies.StartsWith(AlphaPrefix, StringComparison.OrdinalIgnoreCase);
                var species = isAlpha ? rawSpecies[AlphaPrefix.Length..] : rawSpecies;
                var level = TryFindExact(parameter, "Level", 0, out var levelProperty) ? (int)ReadNumber(levelProperty) : 0;
                var nickName = TryFindExact(parameter, "NickName", 0, out var nick) ? ReadScalar(nick).Trim() : string.Empty;
                var owner = TryFindExact(parameter, "OwnerPlayerUId", 0, out var ownerId) ? NormalizeId(ReadScalar(ownerId)) : string.Empty;
                if (owner.All(c => c == '0')) owner = string.Empty;
                var instanceId = entry.TryGetProperty("key", out var key) && TryFindExact(key, "InstanceId", 0, out var instance)
                    ? NormalizeId(ReadScalar(instance)) : string.Empty;

                onMap.Add(new SavedPalLocation(instanceId, species, isAlpha, level, nickName, owner, placement, baseId, x, y, z));
            }
        }

        return new SavedPalLocations(
            onMap.OrderBy(p => p.Placement, StringComparer.Ordinal).ThenBy(p => p.BaseId, StringComparer.Ordinal)
                 .ThenBy(p => p.Species, StringComparer.Ordinal).ThenBy(p => p.InstanceId, StringComparer.Ordinal).ToArray(),
            total, inPalbox, withoutPosition, unplaced);
    }

    private static bool TryReadPosition(JsonElement parameter, out double x, out double y, out double z)
    {
        x = y = z = 0;
        if (!TryFindExact(parameter, "LastJumpedLocation", 0, out var location)) return false;
        var vector = location.ValueKind == JsonValueKind.Object && location.TryGetProperty("value", out var inner) ? inner : location;
        if (vector.ValueKind != JsonValueKind.Object || !TryNumber(vector, "x", out x) || !TryNumber(vector, "y", out y)) return false;
        TryNumber(vector, "z", out z);
        if (x == 0 && y == 0) return false;
        if (Math.Abs(z) >= PlaceholderAltitude) return false;
        return Math.Sqrt(x * x + y * y) >= OriginRadius;
    }

    private static void FindRoots(JsonElement element, Dictionary<string, string> workerContainers, Dictionary<string, int> containerSlots,
        List<JsonElement> characterMaps, int depth)
    {
        if (depth > 80) return;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "CharacterSaveParameterMap": characterMaps.Add(property.Value); continue;
                    case "BaseCampSaveData": ReadWorkerContainers(property.Value, workerContainers); continue;
                    case "CharacterContainerSaveData": ReadContainerSlots(property.Value, containerSlots); continue;
                }
                FindRoots(property.Value, workerContainers, containerSlots, characterMaps, depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) FindRoots(item, workerContainers, containerSlots, characterMaps, depth + 1);
        }
    }

    private static void ReadWorkerContainers(JsonElement root, Dictionary<string, string> into)
    {
        foreach (var entry in Entries(root))
        {
            if (!entry.TryGetProperty("key", out var key) || !entry.TryGetProperty("value", out var value)) continue;
            var baseId = NormalizeId(ReadScalar(key));
            if (baseId.Length < 16) continue;
            if (!TryFindExact(value, "WorkerDirector", 0, out var director) || !TryFindExact(director, "container_id", 0, out var container)) continue;
            var containerId = ReadScalar(container);
            if (containerId.Length > 0) into[containerId] = baseId;
        }
    }

    private static void ReadContainerSlots(JsonElement root, Dictionary<string, int> into)
    {
        foreach (var entry in Entries(root))
        {
            if (!entry.TryGetProperty("key", out var key) || !entry.TryGetProperty("value", out var value)) continue;
            var containerId = TryFindExact(key, "ID", 0, out var id) ? ReadScalar(id) : ReadScalar(key);
            if (containerId.Length == 0 || !value.TryGetProperty("SlotNum", out var slotNum)) continue;
            into[containerId] = (int)ReadNumber(slotNum);
        }
    }

    private static IEnumerable<JsonElement> Entries(JsonElement root)
    {
        var entries = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("value", out var inner) ? inner : root;
        if (entries.ValueKind != JsonValueKind.Array) yield break;
        foreach (var entry in entries.EnumerateArray())
            if (entry.ValueKind == JsonValueKind.Object) yield return entry;
    }

    // First property with exactly this name (case matters: the save has both "ID" and "id"), searching direct
    // properties before descending.
    private static bool TryFindExact(JsonElement element, string name, int depth, out JsonElement found)
    {
        found = default;
        if (depth > 10 || element.ValueKind != JsonValueKind.Object) return false;
        if (element.TryGetProperty(name, out found)) return true;
        foreach (var property in element.EnumerateObject())
            if (TryFindExact(property.Value, name, depth + 1, out found)) return true;
        return false;
    }

    // Unwraps {"value": ...} layers, e.g. Level is {"value":{"type":"None","value":7}}.
    private static JsonElement Unwrap(JsonElement value)
    {
        for (var i = 0; i < 4 && value.ValueKind == JsonValueKind.Object && value.TryGetProperty("value", out var inner); i++) value = inner;
        return value;
    }

    private static string ReadScalar(JsonElement property)
    {
        var value = Unwrap(property);
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty
        };
    }

    private static double ReadNumber(JsonElement property)
    {
        var value = Unwrap(property);
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number)) return number;
        return value.ValueKind == JsonValueKind.String &&
               double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : 0;
    }

    private static bool ReadBool(JsonElement property)
    {
        var value = Unwrap(property);
        return value.ValueKind == JsonValueKind.True ||
               (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed);
    }

    private static bool TryNumber(JsonElement vector, string name, out double number)
    {
        number = 0;
        if (!vector.TryGetProperty(name, out var value)) return false;
        if (value.ValueKind == JsonValueKind.Number) return value.TryGetDouble(out number);
        return value.ValueKind == JsonValueKind.String &&
               double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    private static string NormalizeId(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
}
