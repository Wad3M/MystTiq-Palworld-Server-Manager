using System.Globalization;
using System.Text;

namespace MystTiq.Core.Services;

// v0.7.113.0: the plain-text form the Teleport Points editor uses, one point per line:
//   spawn -358.5 270.25
//   base 120 -44 1500        (optional third number: height; left out, PalDefender finds the ground)
// Coordinates are exactly what PalDefender's `getpos` prints and `tp` accepts. Blank lines and lines starting
// with # are ignored. Kept in Core so the parsing is unit-tested without any UI, like KitEntryText.
public sealed record TeleportPointEntry(string Name, double X, double Y, double? Z);

public static class TeleportPointText
{
    public static bool TryParse(string? text, out IReadOnlyList<TeleportPointEntry> points, out string error)
    {
        var result = new List<TeleportPointEntry>();
        points = result;
        error = string.Empty;
        var lineNumber = 0;
        foreach (var raw in (text ?? string.Empty).Split('\n'))
        {
            lineNumber++;
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var tokens = line.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length is < 3 or > 4) { error = $"Line {lineNumber}: expected a name then X Y (and optionally Z), e.g. \"spawn -358 270\"."; return false; }

            var numbers = new double[tokens.Length - 1];
            for (var i = 1; i < tokens.Length; i++)
            {
                if (!double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i - 1]) || !double.IsFinite(numbers[i - 1]))
                { error = $"Line {lineNumber}: '{tokens[i]}' is not a number (use a dot for decimals)."; return false; }
            }

            result.Add(new TeleportPointEntry(tokens[0], numbers[0], numbers[1], numbers.Length == 3 ? numbers[2] : null));
        }

        return true;
    }

    public static string Format(IEnumerable<TeleportPointEntry> points)
    {
        var builder = new StringBuilder();
        foreach (var p in points)
        {
            builder.Append(p.Name).Append(' ').Append(FormatNumber(p.X)).Append(' ').Append(FormatNumber(p.Y));
            if (p.Z is { } z) builder.Append(' ').Append(FormatNumber(z));
            builder.Append('\n');
        }
        return builder.ToString().TrimEnd('\n');
    }

    public static string FormatNumber(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
