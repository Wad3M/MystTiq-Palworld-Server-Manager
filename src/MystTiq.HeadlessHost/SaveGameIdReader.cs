using System.Text.Json;
using System.Text.RegularExpressions;

namespace MystTiq.HeadlessHost;

// v0.8.3.0: the item and Pal ids that actually occur in this world, read from the decoded Level.sav.json the explorers
// already use. Every item slot in the save carries its item's internal id ("static_id"), and every Pal its species
// ("CharacterID"), exactly as the installed game version names them. That makes these ids valid for this server by
// construction, unlike a hand-written list that drifts with each game update. What it cannot know about is an item
// nobody in this world has ever had; the picker says so, and typing an id by hand still works.
//
// Streaming (Utf8JsonReader, no DOM), because a big world's decoded save runs to hundreds of MB.
public sealed record SaveGameIds(
    IReadOnlyDictionary<string, int> Items,
    IReadOnlyDictionary<string, int> Pals,
    IReadOnlySet<string> AlphaPals);

public static partial class SaveGameIdReader
{
    // Alpha Pals are saved as "BOSS_<id>"; the species id to give is the part after the prefix.
    private const string AlphaPrefix = "BOSS_";

    // The same id rule the give/kit validation applies, so nothing listed can be refused for its shape.
    [GeneratedRegex("^[A-Za-z0-9_]{1,64}$")]
    private static partial Regex IdPattern();

    public static SaveGameIds Read(ReadOnlySpan<byte> json)
    {
        var items = new Dictionary<string, int>(StringComparer.Ordinal);
        var pals = new Dictionary<string, int>(StringComparer.Ordinal);
        var alphas = new HashSet<string>(StringComparer.Ordinal);
        var reader = new Utf8JsonReader(json, new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            MaxDepth = 1024
        });

        // Depth of the CharacterID object being read ({"id":..,"value":"Alpaca","type":"NameProperty"}), or -1.
        var characterDepth = -1;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth == characterDepth)
            {
                characterDepth = -1;
                continue;
            }

            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            if (reader.ValueTextEquals("static_id"u8))
            {
                if (reader.Read() && reader.TokenType == JsonTokenType.String) Add(items, reader.GetString());
            }
            else if (reader.ValueTextEquals("CharacterID"u8))
            {
                if (!reader.Read()) break;
                if (reader.TokenType == JsonTokenType.String) AddPal(pals, alphas, reader.GetString());
                else if (reader.TokenType == JsonTokenType.StartObject) characterDepth = reader.CurrentDepth;
            }
            else if (characterDepth >= 0 && reader.CurrentDepth == characterDepth + 1 && reader.ValueTextEquals("value"u8))
            {
                if (reader.Read() && reader.TokenType == JsonTokenType.String) AddPal(pals, alphas, reader.GetString());
            }
        }

        return new SaveGameIds(items, pals, alphas);
    }

    private static void AddPal(Dictionary<string, int> pals, HashSet<string> alphas, string? raw)
    {
        var id = (raw ?? string.Empty).Trim();
        if (id.StartsWith(AlphaPrefix, StringComparison.OrdinalIgnoreCase) && id.Length > AlphaPrefix.Length)
        {
            id = id[AlphaPrefix.Length..];
            if (IdPattern().IsMatch(id)) alphas.Add(id);
        }
        Add(pals, id);
    }

    private static void Add(Dictionary<string, int> into, string? raw)
    {
        var id = (raw ?? string.Empty).Trim();
        // Empty slots are written with an empty id or "None".
        if (id.Length == 0 || id.Equals("None", StringComparison.OrdinalIgnoreCase) || !IdPattern().IsMatch(id)) return;
        into[id] = into.TryGetValue(id, out var count) ? count + 1 : 1;
    }
}
