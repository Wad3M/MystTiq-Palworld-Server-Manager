// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
using System.Text.Json.Nodes;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.6.0 (roadmap S-3, owner decision D-7: add and remove a Pal; D-2: guarded edits only): one Pal added to, or removed
/// from, one player's Pal box in the decoded world. Pure, so the logic harness covers it on a copy of a real save. Grounded
/// on the clone's decoded world (2026-10-06):
///   - a Pal is a CharacterSaveParameterMap entry: key {PlayerUId: zero, InstanceId, DebugName}, value.RawData.value =
///     {object:{SaveParameter:{value:{CharacterID, Level, OwnerPlayerUId, OldOwnerPlayerUIds, SlotId:{ContainerId,
///     SlotIndex}, ...}}}, group_id (the owner's guild), ...};
///   - the Pal box is the player's PalStorageContainerId (Players/{uid}.sav), a CharacterContainerSaveData entry that
///     lists only filled slots, each {SlotIndex, RawData:{value:{player_uid: zero, instance_id}}}; the Pal's SlotId names
///     the same container and index;
///   - the owner's guild (GroupSaveDataMap) lists every member character, Pals included, in individual_character_handle_ids
///     as {guid: zero, instance_id}.
/// An added Pal is a copy of a Pal of that species already in a Pal box in this world (so its level, HP and talents agree,
/// as the game wrote them), given a new id, the new owner, the first free box slot and the owner's guild, without the
/// original's nickname. Only Pals in the player's own Pal box are removed (not the party, not base workers); the Pal's
/// record, its box slot and its guild entry go together.
/// </summary>
public static class PalBoxEdits
{
    public const string ZeroGuid = "00000000-0000-0000-0000-000000000000";

    public sealed record Pal(int SlotIndex, string InstanceId, string Species, int Level, string? Nickname, string Gender);

    // What the verification compares before and after: every character id, the box's slots, the guild's member ids.
    public sealed record Footprint(IReadOnlySet<string> Characters, IReadOnlyList<Pal> Box, IReadOnlySet<string> GuildMembers);

    private static JsonNode? World(JsonNode level) => level["properties"]?["worldSaveData"]?["value"];
    public static JsonArray Characters(JsonNode level) => World(level)?["CharacterSaveParameterMap"]?["value"] as JsonArray ?? throw new InvalidDataException("The world has no character list.");
    private static JsonArray Containers(JsonNode level) => World(level)?["CharacterContainerSaveData"]?["value"] as JsonArray ?? throw new InvalidDataException("The world has no Pal containers.");
    private static JsonArray Groups(JsonNode level) => World(level)?["GroupSaveDataMap"]?["value"] as JsonArray ?? throw new InvalidDataException("The world has no guild list.");

    public static string? PlayerUid(JsonNode player) => player["properties"]?["SaveData"]?["value"]?["PlayerUId"]?["value"]?.GetValue<string>();
    public static string? PalBoxContainerId(JsonNode player) =>
        player["properties"]?["SaveData"]?["value"]?["PalStorageContainerId"]?["value"]?["ID"]?["value"]?.GetValue<string>();

    private static string InstanceIdOf(JsonNode entry) => entry["key"]?["InstanceId"]?["value"]?.GetValue<string>() ?? string.Empty;
    private static JsonObject? SaveParameter(JsonNode entry) => entry["value"]?["RawData"]?["value"]?["object"]?["SaveParameter"]?["value"] as JsonObject;
    private static bool IsPlayer(JsonObject p) => p.ContainsKey("IsPlayer");

    public static JsonObject? FindContainer(JsonNode level, string containerId) =>
        Containers(level).FirstOrDefault(c => string.Equals(c?["key"]?["ID"]?["value"]?.GetValue<string>(), containerId, StringComparison.OrdinalIgnoreCase))?["value"] as JsonObject;

    private static JsonArray SlotsOf(JsonObject container) => container["Slots"]?["value"]?["values"] as JsonArray ?? throw new InvalidDataException("The Pal container has no slot list.");
    public static int Capacity(JsonObject container) => container["SlotNum"]?["value"]?.GetValue<int>() ?? 0;

    private static int ReadInt(JsonNode? node) => node switch
    {
        JsonValue v when v.TryGetValue<int>(out var i) => i,
        JsonValue v when v.TryGetValue<long>(out var l) => (int)l,
        JsonObject o => ReadInt(o["value"]),
        _ => 0,
    };

    public static IReadOnlyList<Pal> ReadPalBox(JsonNode level, string containerId)
    {
        var container = FindContainer(level, containerId) ?? throw new InvalidDataException("The player's Pal box was not found in the world.");
        var byId = Characters(level).Where(e => e is not null).GroupBy(e => InstanceIdOf(e!), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First()!, StringComparer.OrdinalIgnoreCase);
        var pals = new List<Pal>();
        foreach (var slot in SlotsOf(container))
        {
            var id = slot?["RawData"]?["value"]?["instance_id"]?.GetValue<string>() ?? ZeroGuid;
            var index = ReadInt(slot?["SlotIndex"]);
            if (id == ZeroGuid || !byId.TryGetValue(id, out var entry) || SaveParameter(entry) is not { } p) continue;
            pals.Add(new Pal(index, id, p["CharacterID"]?["value"]?.GetValue<string>() ?? "?", ReadInt(p["Level"]),
                p["NickName"]?["value"]?.GetValue<string>(), p["Gender"]?["value"]?["value"]?.GetValue<string>()?.Split("::").Last() ?? string.Empty));
        }
        return pals.OrderBy(x => x.SlotIndex).ToList();
    }

    // The player's guild: the group_id on the player's own character record.
    public static string? GuildOf(JsonNode level, string playerUid)
    {
        foreach (var e in Characters(level))
        {
            if (e is null || SaveParameter(e) is not { } p || !IsPlayer(p)) continue;
            if (!string.Equals(e["key"]?["PlayerUId"]?["value"]?.GetValue<string>(), playerUid, StringComparison.OrdinalIgnoreCase)) continue;
            var g = e["value"]?["RawData"]?["value"]?["group_id"]?.GetValue<string>();
            return string.IsNullOrEmpty(g) || g == ZeroGuid ? null : g;
        }
        return null;
    }

    private static JsonArray? GuildMembers(JsonNode level, string guildId) =>
        Groups(level).FirstOrDefault(g => string.Equals(g?["key"]?.GetValue<string>(), guildId, StringComparison.OrdinalIgnoreCase))
            ?["value"]?["RawData"]?["value"]?["individual_character_handle_ids"] as JsonArray;

    public static Footprint Measure(JsonNode level, string containerId, string guildId) => new(
        Characters(level).Where(e => e is not null).Select(e => InstanceIdOf(e!)).ToHashSet(StringComparer.OrdinalIgnoreCase),
        ReadPalBox(level, containerId),
        (GuildMembers(level, guildId) ?? []).Select(h => h?["instance_id"]?.GetValue<string>() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase));

    // Species that can be added: those with a Pal in a Pal box (a container the size of this player's box) to copy.
    public static IReadOnlyList<string> AddableSpecies(JsonNode level, string containerId)
    {
        var boxSize = Capacity(FindContainer(level, containerId) ?? throw new InvalidDataException("The player's Pal box was not found in the world."));
        return Templates(level, boxSize).Select(t => SaveParameter(t)!["CharacterID"]?["value"]?.GetValue<string>() ?? string.Empty)
            .Where(s => s.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<JsonNode> Templates(JsonNode level, int boxSize)
    {
        var sizes = Containers(level).Where(c => c?["key"]?["ID"]?["value"] is not null)
            .ToDictionary(c => c!["key"]!["ID"]!["value"]!.GetValue<string>(), c => c!["value"] is JsonObject v ? Capacity(v) : 0, StringComparer.OrdinalIgnoreCase);
        foreach (var e in Characters(level))
        {
            if (e is null || SaveParameter(e) is not { } p || IsPlayer(p)) continue;
            var container = p["SlotId"]?["value"]?["ContainerId"]?["value"]?["ID"]?["value"]?.GetValue<string>();
            if (container is not null && sizes.TryGetValue(container, out var size) && size == boxSize) yield return e;
        }
    }

    /// <summary>Removes one Pal from the player's Pal box: its record, its slot and its guild entry.</summary>
    public static Pal Remove(JsonNode level, string playerUid, string containerId, string instanceId)
    {
        var box = FindContainer(level, containerId) ?? throw new InvalidDataException("The player's Pal box was not found in the world.");
        var pal = ReadPalBox(level, containerId).FirstOrDefault(p => p.InstanceId.Equals(instanceId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("That Pal is not in this player's Pal box (Pals in the party or at a base are not removed here).");
        var characters = Characters(level);
        var entry = characters.First(e => e is not null && InstanceIdOf(e).Equals(instanceId, StringComparison.OrdinalIgnoreCase))!;
        var owner = SaveParameter(entry)?["OwnerPlayerUId"]?["value"]?.GetValue<string>();
        if (owner is not null && !owner.Equals(playerUid, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("That Pal belongs to another player.");
        var slots = SlotsOf(box);
        slots.Remove(slots.First(s => string.Equals(s?["RawData"]?["value"]?["instance_id"]?.GetValue<string>(), instanceId, StringComparison.OrdinalIgnoreCase)));
        characters.Remove(entry);
        foreach (var g in Groups(level))
            if (g?["value"]?["RawData"]?["value"]?["individual_character_handle_ids"] is JsonArray members)
                foreach (var h in members.Where(h => string.Equals(h?["instance_id"]?.GetValue<string>(), instanceId, StringComparison.OrdinalIgnoreCase)).ToList())
                    members.Remove(h);
        return pal;
    }

    /// <summary>Adds a Pal of this species to the player's Pal box, copied from one already in a Pal box in this world.</summary>
    public static Pal Add(JsonNode level, string playerUid, string containerId, string species, string newInstanceId, DateTime nowUtc)
    {
        var box = FindContainer(level, containerId) ?? throw new InvalidDataException("The player's Pal box was not found in the world.");
        var guild = GuildOf(level, playerUid) ?? throw new InvalidOperationException("The player is not in a guild in this world, so MystTiq cannot add a Pal for them.");
        var members = GuildMembers(level, guild) ?? throw new InvalidDataException("The player's guild has no member list.");
        var template = Templates(level, Capacity(box))
            .OrderByDescending(e => string.Equals(SaveParameter(e)!["OwnerPlayerUId"]?["value"]?.GetValue<string>(), playerUid, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(e => string.Equals(SaveParameter(e)!["CharacterID"]?["value"]?.GetValue<string>(), species, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No {species} is in a Pal box in this world, so MystTiq has none to copy.");
        var slots = SlotsOf(box);
        var used = slots.Select(s => ReadInt(s?["SlotIndex"])).ToHashSet();
        var free = Enumerable.Range(0, Capacity(box)).FirstOrDefault(i => !used.Contains(i), -1);
        if (free < 0) throw new InvalidOperationException($"The Pal box is full ({Capacity(box)} places).");
        var slotTemplate = slots.FirstOrDefault() ?? Containers(level).SelectMany(c => c?["value"] is JsonObject v && v["Slots"]?["value"]?["values"] is JsonArray a ? a : []).FirstOrDefault()
            ?? throw new InvalidDataException("No Pal container slot in this world to copy the layout from.");

        var entry = template.DeepClone();
        entry["key"]!["InstanceId"]!["value"] = newInstanceId;
        entry["key"]!["PlayerUId"]!["value"] = ZeroGuid;
        if (entry["key"]?["DebugName"] is JsonObject debug) debug["value"] = string.Empty;
        var p = SaveParameter(entry)!;
        p["OwnerPlayerUId"]!["value"] = playerUid;
        if (p["OldOwnerPlayerUIds"]?["value"] is JsonObject old) old["values"] = new JsonArray(playerUid);
        p["SlotId"]!["value"]!["ContainerId"]!["value"]!["ID"]!["value"] = containerId;
        p["SlotId"]!["value"]!["SlotIndex"]!["value"] = free;
        if (p["OwnedTime"] is JsonObject owned) owned["value"] = nowUtc.Ticks;
        p.Remove("NickName");
        p.Remove("LastNickNameModifierPlayerUid");
        entry["value"]!["RawData"]!["value"]!["group_id"] = guild;
        Characters(level).Add(entry);

        var slot = slotTemplate.DeepClone();
        slot["SlotIndex"]!["value"] = free;
        slot["RawData"]!["value"]!["instance_id"] = newInstanceId;
        slot["RawData"]!["value"]!["player_uid"] = ZeroGuid;
        slots.Add(slot);

        members.Add(new JsonObject { ["guid"] = ZeroGuid, ["instance_id"] = newInstanceId });
        return new Pal(free, newInstanceId, species, ReadInt(p["Level"]), null, p["Gender"]?["value"]?["value"]?.GetValue<string>()?.Split("::").Last() ?? string.Empty);
    }

    /// <summary>Exactly this one Pal differs, in all three places; everything else in them is as it was.</summary>
    public static void VerifyOnlyThisChanged(Footprint before, Footprint after, Pal changed, string action)
    {
        bool adding = action == "add";
        var characters = adding ? before.Characters.Append(changed.InstanceId) : before.Characters.Where(c => !c.Equals(changed.InstanceId, StringComparison.OrdinalIgnoreCase));
        var members = adding ? before.GuildMembers.Append(changed.InstanceId) : before.GuildMembers.Where(c => !c.Equals(changed.InstanceId, StringComparison.OrdinalIgnoreCase));
        var box = adding ? before.Box.Append(changed with { Nickname = null }) : before.Box.Where(p => !p.InstanceId.Equals(changed.InstanceId, StringComparison.OrdinalIgnoreCase));
        if (!after.Characters.SetEquals(characters)) throw new InvalidDataException("Verification: the world's characters are not exactly the old ones plus or minus this Pal.");
        if (!after.GuildMembers.SetEquals(members)) throw new InvalidDataException("Verification: the guild's members are not exactly the old ones plus or minus this Pal.");
        if (!after.Box.OrderBy(p => p.SlotIndex).SequenceEqual(box.OrderBy(p => p.SlotIndex)))
            throw new InvalidDataException("Verification: the Pal box is not exactly the old one plus or minus this Pal.");
    }
}
