using System.Text;

namespace MystTiq.Core.Services;

// v0.7.94.0: the plain-text form the starter-kit editor uses, one entry per line:
//   item PalSphere 10
//   pal WeaselDragon 5        (Pal id, then level)
// Shorthand "PalSphere 10" (no keyword) is an item. Blank lines and lines starting with # are ignored.
// Kept in Core so the parsing is unit-tested without any UI.
public sealed record KitTextEntry(string Type, string Id, int Amount);

public static class KitEntryText
{
    public static bool TryParse(string? text, out IReadOnlyList<KitTextEntry> entries, out string error)
    {
        var result = new List<KitTextEntry>();
        entries = result;
        error = string.Empty;
        var lineNumber = 0;
        foreach (var raw in (text ?? string.Empty).Split('\n'))
        {
            lineNumber++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var tokens = line.Split([' ', '\t', ':', ','], StringSplitOptions.RemoveEmptyEntries);
            var type = "Item";
            var start = 0;
            if (tokens[0].Equals("item", StringComparison.OrdinalIgnoreCase)) start = 1;
            else if (tokens[0].Equals("pal", StringComparison.OrdinalIgnoreCase)) { type = "Pal"; start = 1; }

            if (tokens.Length <= start) { error = $"Line {lineNumber}: missing the id after '{tokens[0]}'."; return false; }
            var id = tokens[start];
            var amount = 1;
            if (tokens.Length > start + 1 && !int.TryParse(tokens[start + 1], out amount))
            { error = $"Line {lineNumber}: '{tokens[start + 1]}' is not a whole number."; return false; }
            if (tokens.Length > start + 2) { error = $"Line {lineNumber}: too many values (expected: {(type == "Pal" ? "pal <PalId> <level>" : "item <ItemId> <amount>")})."; return false; }

            result.Add(new KitTextEntry(type, id, amount));
        }

        return true;
    }

    // v0.8.3.0: what the item picker adds. A new line at the end, after any text already typed (which is left as is,
    // even if it does not parse yet, so the picker never throws away someone's half-typed line).
    public static string Append(string? text, KitTextEntry entry)
    {
        var line = Format([entry]);
        var existing = (text ?? string.Empty).TrimEnd();
        return existing.Length == 0 ? line : existing + "\n" + line;
    }

    // v0.8.3.0: the picker's amount box, with the same limits the server enforces (HeadlessKitService.Validate):
    // an item amount of 1 to 1,000,000, a Pal level of 1 to 100.
    public static bool TryParseAmount(string type, string? text, out int amount, out string error)
    {
        var isPal = type.Equals("Pal", StringComparison.OrdinalIgnoreCase);
        var (label, max) = isPal ? ("Level", 100) : ("Amount", 1_000_000);
        error = string.Empty;
        if (!int.TryParse((text ?? string.Empty).Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out amount))
        { error = $"{label} must be a whole number."; return false; }
        if (amount < 1 || amount > max)
        { error = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{label} must be 1 to {max:N0}."); return false; }
        return true;
    }

    public static string Format(IEnumerable<KitTextEntry> entries)
    {
        var builder = new StringBuilder();
        foreach (var entry in entries)
            builder.Append(entry.Type.Equals("Pal", StringComparison.OrdinalIgnoreCase) ? "pal " : "item ").Append(entry.Id).Append(' ').Append(entry.Amount).Append('\n');
        return builder.ToString().TrimEnd('\n');
    }
}
