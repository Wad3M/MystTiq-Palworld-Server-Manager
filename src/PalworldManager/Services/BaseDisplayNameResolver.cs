using System.Globalization;
using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Converts Palworld's internal/template BaseCamp labels into stable,
/// user-facing names without losing the original decoded value.
/// </summary>
public static class BaseDisplayNameResolver
{
    private static readonly string[] InternalMarkers =
    {
        "(仮)", "（仮）", "テンプレート", "template", "placeholder", "default", "newbasecamp"
    };

    public static string Resolve(string? decodedName, string? guildName, string? baseId, int ordinal)
    {
        var candidate = (decodedName ?? string.Empty).Trim();
        if (IsMeaningful(candidate)) return candidate;

        var owner = (guildName ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(owner) &&
            !owner.Equals("Unassigned", StringComparison.OrdinalIgnoreCase) &&
            !owner.Equals("Unknown guild", StringComparison.OrdinalIgnoreCase))
            return ordinal <= 1 ? $"{owner} Base" : $"{owner} Base {ordinal}";

        return ordinal <= 1 ? "Base 1" : $"Base {ordinal}";
    }

    public static bool IsMeaningful(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var text = value.Trim();
        if (text.Length < 2) return false;
        if (InternalMarkers.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase))) return false;

        // Replacement characters and control-heavy strings indicate a failed decode.
        if (text.Contains('\uFFFD') || text.Any(char.IsControl)) return false;
        return true;
    }

    public static string DescribeRawName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Not stored" : value.Trim();
}
