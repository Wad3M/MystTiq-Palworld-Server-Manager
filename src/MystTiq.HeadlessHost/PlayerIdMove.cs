// MystTiq v1.1.0.2: file reviewed for this release (2026-10-13).
using System.Text.Json.Nodes;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.1.0.2 (roadmap F-2): moving a player's character to the player ID the game now gives their account. Since the 1.0
/// update the game gives Steam accounts new player IDs that are not derived from the Steam ID (owner's server, 2026-10-13:
/// steam_76561197962020201 67D8D355 → E290DA9A, steam_76561198653223616 A3835C7B → 1467C601), so a returning player is
/// handed an empty character while their real one stays under the old ID. The move runs on the decoded world file and the
/// decoded character file:
/// <list type="bullet">
/// <item>the character already under the new ID must be a throwaway: one character body and, at most, a guild where it is
/// the only member and has no bases; anything else that names the new ID refuses the move;</item>
/// <item>the throwaway's body and solo guild are removed, then every value naming the old ID (the character body, owned
/// Pals, placed buildings, previous-owner records, guild membership, markers) is rewritten to the new ID;</item>
/// <item>in the character file, the two ID fields are rewritten; its Pal party and Pal box references stay.</item>
/// </list>
/// The same procedure moved the owner's character by hand on 2026-10-13 (216 world references, 2 in the character file).
/// Pure, so the logic harness covers it; the service around it does the backup, encode, verification and file moves.
/// </summary>
public static class PlayerIdMove
{
    /// <summary>A 32-hex player ID ("67D8D355000000000000000000000000") as the save writes it (a lower-case GUID).</summary>
    public static string Guid(string playerId)
    {
        var u = playerId.Trim().ToLowerInvariant();
        if (u.Length == 36 && u[8] == '-') return u;
        if (u.Length != 32) throw new ArgumentException($"Not a player ID: {playerId}", nameof(playerId));
        return $"{u[..8]}-{u[8..12]}-{u[12..16]}-{u[16..20]}-{u[20..32]}";
    }

    /// <summary>How many string values in the tree equal <paramref name="guid"/> (case ignored).</summary>
    public static int Count(JsonNode? node, string guid) => node switch
    {
        JsonObject o => o.Sum(p => Count(p.Value, guid)),
        JsonArray a => a.Sum(v => Count(v, guid)),
        JsonValue v when v.TryGetValue<string>(out var s) => string.Equals(s, guid, StringComparison.OrdinalIgnoreCase) ? 1 : 0,
        _ => 0
    };

    /// <summary>Rewrites every string value equal to <paramref name="from"/> to <paramref name="to"/>; returns how many.</summary>
    public static int Replace(JsonNode? node, string from, string to)
    {
        var replaced = 0;
        switch (node)
        {
            case JsonObject o:
                foreach (var key in o.Select(p => p.Key).ToList())
                {
                    if (IsValue(o[key], from)) { o[key] = to; replaced++; }
                    else replaced += Replace(o[key], from, to);
                }
                break;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++)
                {
                    if (IsValue(a[i], from)) { a[i] = to; replaced++; }
                    else replaced += Replace(a[i], from, to);
                }
                break;
        }
        return replaced;
    }

    private static bool IsValue(JsonNode? node, string guid) =>
        node is JsonValue v && v.TryGetValue<string>(out var s) && string.Equals(s, guid, StringComparison.OrdinalIgnoreCase);

    /// <summary>What a move would do, or why it cannot be done (<see cref="Problem"/> set).</summary>
    public sealed record Plan(string? Problem, int OldReferences, int NewReferences, int ThrowawayBodies, int ThrowawayGuilds, int Characters)
    {
        public bool CanMove => Problem is null;
    }

    private static JsonObject? WorldSaveData(JsonNode root) => root["properties"]?["worldSaveData"]?["value"] as JsonObject;
    private static JsonArray? Characters(JsonObject world) => world["CharacterSaveParameterMap"]?["value"] as JsonArray;
    private static JsonArray? Groups(JsonObject world) => world["GroupSaveDataMap"]?["value"] as JsonArray;
    private static string BodyPlayerId(JsonNode? entry) => (entry?["key"]?["PlayerUId"]?["value"]?.GetValue<string>() ?? string.Empty).ToLowerInvariant();
    private static JsonObject? GroupRaw(JsonNode? entry) => entry?["value"]?["RawData"]?["value"] as JsonObject;

    /// <summary>Checks the decoded world for a move from <paramref name="fromPlayerId"/> to <paramref name="toPlayerId"/>.</summary>
    public static Plan Check(JsonNode level, string fromPlayerId, string toPlayerId)
    {
        string from = Guid(fromPlayerId), to = Guid(toPlayerId);
        if (from == to) return new("The old and new player IDs are the same.", 0, 0, 0, 0, 0);
        var world = WorldSaveData(level);
        var characters = world is null ? null : Characters(world);
        var groups = world is null ? null : Groups(world);
        if (characters is null || groups is null) return new("The world file has no character or guild list (the save format may have changed).", 0, 0, 0, 0, 0);

        var oldReferences = Count(level, from);
        var newReferences = Count(level, to);
        var oldBodies = characters.Count(e => BodyPlayerId(e) == from);
        var newBodies = characters.Where(e => BodyPlayerId(e) == to).ToList();
        if (oldBodies != 1) return new($"Expected one character for the old ID, found {oldBodies}.", oldReferences, newReferences, 0, 0, characters.Count);
        if (newBodies.Count > 1) return new($"Expected at most one character for the new ID, found {newBodies.Count}.", oldReferences, newReferences, 0, 0, characters.Count);

        var newGroups = groups.Where(g => GroupRaw(g) is { } raw && Count(raw, to) > 0).ToList();
        foreach (var raw in newGroups.Select(GroupRaw))
        {
            var members = (raw!["players"] as JsonArray ?? []).Select(m => (m?["player_uid"]?.GetValue<string>() ?? string.Empty).ToLowerInvariant()).ToList();
            if (members.Count != 1 || members[0] != to)
                return new($"The character under the new ID is in a guild with {members.Count} member(s); move it by hand.", oldReferences, newReferences, newBodies.Count, newGroups.Count, characters.Count);
            if (raw["base_ids"] is JsonArray bases && bases.Count > 0)
                return new($"The character under the new ID has a guild with {bases.Count} base(s); move it by hand.", oldReferences, newReferences, newBodies.Count, newGroups.Count, characters.Count);
        }
        var throwawayReferences = newBodies.Sum(b => Count(b, to)) + newGroups.Sum(g => Count(g, to));
        if (throwawayReferences != newReferences)
            return new($"The new ID appears outside its own character and guild ({newReferences} references, {throwawayReferences} in them); move it by hand.", oldReferences, newReferences, newBodies.Count, newGroups.Count, characters.Count);
        return new(null, oldReferences, newReferences, newBodies.Count, newGroups.Count, characters.Count);
    }

    /// <summary>Moves the world from the old ID to the new one (after <see cref="Check"/> passed); returns references rewritten.</summary>
    public static int MoveWorld(JsonNode level, string fromPlayerId, string toPlayerId)
    {
        var plan = Check(level, fromPlayerId, toPlayerId);
        if (!plan.CanMove) throw new InvalidOperationException(plan.Problem);
        string from = Guid(fromPlayerId), to = Guid(toPlayerId);
        var world = WorldSaveData(level)!;
        var characters = Characters(world)!;
        var groups = Groups(world)!;
        foreach (var body in characters.Where(e => BodyPlayerId(e) == to).ToList()) characters.Remove(body);
        foreach (var group in groups.Where(g => GroupRaw(g) is { } raw && Count(raw, to) > 0).ToList()) groups.Remove(group);
        var rewritten = Replace(level, from, to);
        if (rewritten != plan.OldReferences || Count(level, from) != 0 || Count(level, to) != plan.OldReferences)
            throw new InvalidOperationException("The rewrite did not account for every reference.");
        return rewritten;
    }

    /// <summary>Moves a decoded character file to the new ID; returns references rewritten (the two ID fields).</summary>
    public static int MoveCharacterFile(JsonNode player, string fromPlayerId, string toPlayerId)
    {
        string from = Guid(fromPlayerId), to = Guid(toPlayerId);
        if (player["properties"]?["SaveData"]?["value"] is not JsonObject) throw new InvalidOperationException("The character file has no SaveData.");
        var before = Count(player, from);
        if (before == 0) throw new InvalidOperationException("The character file does not name the old ID.");
        var rewritten = Replace(player, from, to);
        if (Count(player, from) != 0 || Count(player, to) != before) throw new InvalidOperationException("The character file rewrite did not account for every reference.");
        return rewritten;
    }

    /// <summary>Checks a re-encoded world and character file after a move (decoded again from the staged files).</summary>
    public static string? Verify(JsonNode level, JsonNode player, string fromPlayerId, string toPlayerId, int expectedWorldReferences, int expectedCharacters, int expectedFileReferences)
    {
        string from = Guid(fromPlayerId), to = Guid(toPlayerId);
        var world = WorldSaveData(level);
        var characters = world is null ? null : Characters(world);
        if (characters is null) return "The staged world has no character list.";
        if (Count(level, from) != 0) return "The staged world still names the old ID.";
        if (Count(level, to) != expectedWorldReferences) return $"The staged world names the new ID {Count(level, to)} times, expected {expectedWorldReferences}.";
        if (characters.Count(e => BodyPlayerId(e) == to) != 1) return "The staged world does not have exactly one character for the new ID.";
        if (characters.Count != expectedCharacters) return $"The staged world has {characters.Count} characters, expected {expectedCharacters}.";
        if (Count(player, from) != 0 || Count(player, to) != expectedFileReferences) return "The staged character file does not name the new ID exactly where it named the old one.";
        return null;
    }
}
