using System.Globalization;
using System.Text.Json;

namespace MystTiq.HeadlessHost;

// v0.7.100.0: where each player character last was, read from the decoded Level.sav.json that the
// guild/base explorer already parses. This is what the map shows for players who are offline.
//
// The value is the character's LastJumpedLocation. It is the last position the game recorded for the
// player at save time, not a live position and not necessarily the exact spot they logged out at, so
// the map labels it as "last known". Online players are still drawn from the live REST position.
public sealed record SavedPlayerLocation(string PlayerId, string Name, double X, double Y, double Z);

public static class SavePlayerLocationReader
{
    // Palworld writes this for characters that have no real position, and 0,0 for an unset one.
    private const double PlaceholderAltitude = 900000;

    public static IReadOnlyList<SavedPlayerLocation> Read(JsonElement root)
    {
        var roots = new List<JsonElement>();
        FindCharacterRoots(root, roots, 0);

        var byPlayer = new Dictionary<string, SavedPlayerLocation>(StringComparer.OrdinalIgnoreCase);
        foreach (var characterRoot in roots)
        {
            var entries = characterRoot;
            if (entries.ValueKind == JsonValueKind.Object && entries.TryGetProperty("value", out var inner)) entries = inner;
            if (entries.ValueKind != JsonValueKind.Array) continue;

            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) continue;
                if (!TryFind(entry, "IsPlayer", 0, out var isPlayer) || !ReadBool(isPlayer)) continue;
                if (!entry.TryGetProperty("key", out var key) || !TryFind(key, "PlayerUId", 0, out var uid)) continue;

                var playerId = NormalizeId(ReadScalar(uid));
                if (playerId.Length < 16 || playerId.All(c => c == '0')) continue;
                if (!TryFind(entry, "LastJumpedLocation", 0, out var location)) continue;
                if (!location.TryGetProperty("value", out var vector) || vector.ValueKind != JsonValueKind.Object) continue;
                if (!TryNumber(vector, "x", out var x) || !TryNumber(vector, "y", out var y)) continue;
                TryNumber(vector, "z", out var z);
                if (x == 0 && y == 0) continue;
                if (Math.Abs(z) >= PlaceholderAltitude) continue;

                var name = TryFind(entry, "NickName", 0, out var nick) ? ReadScalar(nick).Trim() : string.Empty;
                byPlayer[playerId] = new SavedPlayerLocation(playerId, name, x, y, z);
            }
        }

        return byPlayer.Values.OrderBy(p => p.PlayerId, StringComparer.Ordinal).ToArray();
    }

    private static void FindCharacterRoots(JsonElement element, List<JsonElement> roots, int depth)
    {
        if (depth > 80) return;
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (NormalizeKey(property.Name) == "charactersaveparametermap") { roots.Add(property.Value); continue; }
                FindCharacterRoots(property.Value, roots, depth + 1);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) FindCharacterRoots(item, roots, depth + 1);
        }
    }

    // First property with this name, searching direct properties before descending.
    private static bool TryFind(JsonElement element, string name, int depth, out JsonElement found)
    {
        found = default;
        if (depth > 10 || element.ValueKind != JsonValueKind.Object) return false;
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { found = property.Value; return true; }
        }
        foreach (var property in element.EnumerateObject())
        {
            if (TryFind(property.Value, name, depth + 1, out found)) return true;
        }
        return false;
    }

    private static bool ReadBool(JsonElement property)
    {
        var value = property.ValueKind == JsonValueKind.Object && property.TryGetProperty("value", out var inner) ? inner : property;
        return value.ValueKind == JsonValueKind.True ||
               (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed);
    }

    private static string ReadScalar(JsonElement property)
    {
        var value = property.ValueKind == JsonValueKind.Object && property.TryGetProperty("value", out var inner) ? inner : property;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty
        };
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

    private static string NormalizeKey(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}
