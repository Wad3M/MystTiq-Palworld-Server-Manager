// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
using System.Text.Json.Nodes;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.4.0 (roadmap S-1/S-2, owner decision D-2: guarded edits only): one item removed from, or added to, one player's
/// main inventory in the decoded world (Level.sav's ItemContainerSaveData). Pure, so the logic harness covers it on a copy
/// of a real save. Grounded on decoded production data (2026-10-05):
///   - a container is {key:{ID:{value:guid}}, value:{SlotNum:{value:N}, Slots:{value:{values:[slot...]}}}}; only filled
///     slots are listed, each {RawData:{value:{slot_index, count, item:{static_id, dynamic_id:{created_world_id,
///     local_id_in_created_world}}, trailing_bytes}}, CustomVersionData}; an empty slot is simply absent;
///   - tools, armour, eggs and the like carry their own record (a non-zero dynamic id pointing into DynamicItemSaveData);
///     plain stacks (Money, ores, Pal Spheres ...) do not.
/// Only plain stacks are edited: removing an item with its own record would leave that record behind, and adding one
/// would need a record MystTiq cannot make yet. An item is added only if the world already holds it as a plain stack, so
/// its id is one the game itself writes that way.
/// </summary>
public static class InventoryEdits
{
    public const string ZeroGuid = "00000000-0000-0000-0000-000000000000";

    public sealed record Slot(int SlotIndex, string ItemId, int Count, bool HasOwnRecord);

    // The player's main inventory container id, from the decoded Players/{uid}.sav (SaveData.InventoryInfo.CommonContainerId).
    public static string? CommonContainerId(JsonNode playerRoot)
    {
        foreach (var node in FindAllByKey(playerRoot, "commoncontainerid"))
            if (FindGuid(node) is { } id) return id;
        return null;
    }

    public static JsonArray? ContainerEntries(JsonNode levelRoot)
    {
        if (levelRoot["properties"]?["worldSaveData"]?["value"]?["ItemContainerSaveData"]?["value"] is JsonArray direct) return direct;
        foreach (var node in FindAllByKey(levelRoot, "itemcontainersavedata"))
        {
            var entries = node is JsonObject o && o["value"] is JsonArray a ? a : node as JsonArray;
            if (entries is not null) return entries;
        }
        return null;
    }

    public static JsonObject? FindContainer(JsonNode levelRoot, string containerId)
    {
        foreach (var entry in ContainerEntries(levelRoot) ?? [])
        {
            if (entry?["key"] is not JsonNode key || FindGuid(key) is not { } id) continue;
            if (id.Equals(containerId, StringComparison.OrdinalIgnoreCase) && entry["value"] is JsonObject value) return value;
        }
        return null;
    }

    public static int Capacity(JsonObject container) => container["SlotNum"]?["value"]?.GetValue<int>() ?? 0;

    public static JsonArray SlotArray(JsonObject container) =>
        container["Slots"]?["value"]?["values"] as JsonArray ?? throw new InvalidDataException("The container has no slot list.");

    public static IReadOnlyList<Slot> ReadSlots(JsonObject container) =>
        SlotArray(container).Select(ToSlot).Where(s => s is not null).Select(s => s!).OrderBy(s => s.SlotIndex).ToList();

    private static Slot? ToSlot(JsonNode? slot)
    {
        var raw = slot?["RawData"]?["value"];
        if (raw is null) return null;
        var item = raw["item"];
        return new Slot(raw["slot_index"]?.GetValue<int>() ?? -1, item?["static_id"]?.GetValue<string>() ?? string.Empty,
            raw["count"]?.GetValue<int>() ?? 0, HasOwnRecord(item));
    }

    private static bool HasOwnRecord(JsonNode? item)
    {
        var dynamic = item?["dynamic_id"];
        var local = dynamic?["local_id_in_created_world"]?.GetValue<string>() ?? ZeroGuid;
        var world = dynamic?["created_world_id"]?.GetValue<string>() ?? ZeroGuid;
        return !local.Equals(ZeroGuid, StringComparison.OrdinalIgnoreCase) || !world.Equals(ZeroGuid, StringComparison.OrdinalIgnoreCase);
    }

    // Whether the world holds this item id as a plain stack somewhere (proof that the game writes it without a record).
    public static bool IsPlainStackInWorld(JsonNode levelRoot, string itemId) => PlainStackTemplate(levelRoot, itemId) is not null;

    // A plain-stack slot to copy the layout from: one holding this id if possible, else any plain stack.
    private static JsonNode? PlainStackTemplate(JsonNode levelRoot, string? itemId)
    {
        JsonNode? any = null;
        foreach (var entry in ContainerEntries(levelRoot) ?? [])
        {
            if (entry?["value"] is not JsonObject value || value["Slots"]?["value"]?["values"] is not JsonArray slots) continue;
            foreach (var slot in slots)
            {
                var s = ToSlot(slot);
                if (s is null || s.HasOwnRecord || s.Count <= 0) continue;
                if (itemId is not null && s.ItemId.Equals(itemId, StringComparison.Ordinal)) return slot;
                any ??= slot;
            }
        }
        return itemId is null ? any : null;
    }

    /// <summary>Removes the stack of this item (in the given slot, or the first one). Returns what was removed.</summary>
    public static Slot Remove(JsonObject container, string itemId, int? slotIndex)
    {
        var slots = SlotArray(container);
        for (var i = 0; i < slots.Count; i++)
        {
            var s = ToSlot(slots[i]);
            if (s is null || !s.ItemId.Equals(itemId, StringComparison.Ordinal) || (slotIndex is { } wanted && s.SlotIndex != wanted)) continue;
            if (s.HasOwnRecord)
                throw new InvalidOperationException($"{itemId} in slot {s.SlotIndex} has its own record (durability, contents); MystTiq only removes plain stacks.");
            slots.RemoveAt(i);
            return s;
        }
        throw new InvalidOperationException(slotIndex is { } n ? $"Slot {n} does not hold {itemId}." : $"The inventory holds no {itemId}.");
    }

    /// <summary>Adds a plain stack of this item in the first free slot. Refused when the inventory is full.</summary>
    public static Slot Add(JsonNode levelRoot, JsonObject container, string itemId, int count)
    {
        if (count < 1 || count > 9999) throw new ArgumentOutOfRangeException(nameof(count), "Add between 1 and 9999.");
        var template = PlainStackTemplate(levelRoot, itemId)
            ?? throw new InvalidOperationException($"The world holds no plain stack of {itemId}, so MystTiq cannot add it safely.");
        var slots = SlotArray(container);
        var used = slots.Select(ToSlot).Where(s => s is not null).Select(s => s!.SlotIndex).ToHashSet();
        var capacity = Capacity(container);
        var free = Enumerable.Range(0, capacity).FirstOrDefault(i => !used.Contains(i), -1);
        if (free < 0) throw new InvalidOperationException($"The inventory is full ({capacity} slots).");
        var slot = template.DeepClone();
        var raw = slot["RawData"]!["value"]!;
        raw["slot_index"] = free;
        raw["count"] = count;
        raw["item"]!["static_id"] = itemId;
        raw["item"]!["dynamic_id"]!["created_world_id"] = ZeroGuid;
        raw["item"]!["dynamic_id"]!["local_id_in_created_world"] = ZeroGuid;
        slots.Add(slot);
        return new Slot(free, itemId, count, false);
    }

    private static string? FindGuid(JsonNode node)
    {
        if (node is JsonValue v && v.TryGetValue<string>(out var s) && Guid.TryParse(s, out _)) return s;
        if (node is JsonObject o)
        {
            if (o["value"] is JsonNode val && FindGuid(val) is { } inValue) return inValue;
            foreach (var (_, child) in o) if (child is not null && FindGuid(child) is { } found) return found;
        }
        return null;
    }

    private static IEnumerable<JsonNode> FindAllByKey(JsonNode node, string key, int depth = 0)
    {
        if (depth > 12) yield break;
        if (node is JsonObject o)
        {
            foreach (var (name, child) in o)
            {
                if (child is null) continue;
                if (name.Equals(key, StringComparison.OrdinalIgnoreCase)) yield return child;
                foreach (var found in FindAllByKey(child, key, depth + 1)) yield return found;
            }
        }
        else if (node is JsonArray a && depth < 6)
        {
            foreach (var child in a) if (child is not null) foreach (var found in FindAllByKey(child, key, depth + 1)) yield return found;
        }
    }
}
