// MystTiq v1.1.0.3: file reviewed for this release (2026-10-13).
namespace MystTiq.HeadlessHost;

/// <summary>One UE4SS MOD line of <c>mods.txt</c>, in the order UE4SS loads it.</summary>
public sealed record ModsTxtEntry(string Package, bool Enabled);

/// <summary>
/// v1.0.9.2 (roadmap B-3): UE4SS loads the MODs <c>mods.txt</c> lists in the order it lists them; that order is the only
/// load order MystTiq can set (PAKs have none a file name controls: the game resolves their conflicts by asset). Pure text
/// work so the logic harness covers it: comments, blank lines and each line's own spelling are kept, and only the entry
/// lines change places.
/// </summary>
public static class ModLoadOrder
{
    /// <summary>The entries in file order; comments (<c>;</c> or <c>#</c>), blank lines and malformed lines are skipped.</summary>
    public static IReadOnlyList<ModsTxtEntry> Parse(IEnumerable<string> lines)
    {
        var entries = new List<ModsTxtEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
            if (TryParse(raw, out var entry) && seen.Add(entry.Package)) entries.Add(entry);
        return entries;
    }

    /// <summary>
    /// The lines with the entries in a new order: <paramref name="first"/> (in its order), then every other entry in its
    /// current order. Each entry keeps its own line; comments and blank lines keep their places. With
    /// <paramref name="strict"/>, a name that is not an entry is refused (a stale list from the desktop); otherwise it is
    /// skipped (a MOD profile naming a MOD this server does not have).
    /// </summary>
    public static IReadOnlyList<string> Reorder(IReadOnlyList<string> lines, IEnumerable<string> first, bool strict)
    {
        var entryLines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var slots = new List<int>();
        var current = new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (!TryParse(lines[i], out var entry) || entryLines.ContainsKey(entry.Package)) continue;
            entryLines[entry.Package] = lines[i];
            slots.Add(i);
            current.Add(entry.Package);
        }

        var order = new List<string>();
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in first)
        {
            var package = (name ?? string.Empty).Trim();
            if (!entryLines.ContainsKey(package))
            {
                if (strict) throw new InvalidDataException($"'{package}' is not in mods.txt. Refresh the MOD list and try again.");
                continue;
            }
            if (placed.Add(package)) order.Add(package);
        }
        order.AddRange(current.Where(placed.Add));

        var result = lines.ToList();
        for (var i = 0; i < slots.Count; i++) result[slots[i]] = entryLines[order[i]];
        return result;
    }

    private static bool TryParse(string raw, out ModsTxtEntry entry)
    {
        entry = null!;
        var line = (raw ?? string.Empty).Trim();
        if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) return false;
        var sep = line.LastIndexOf(':');
        if (sep <= 0) return false;
        var name = line[..sep].Trim();
        if (name.Length == 0) return false;
        entry = new ModsTxtEntry(name, line[(sep + 1)..].Trim() == "1");
        return true;
    }
}
