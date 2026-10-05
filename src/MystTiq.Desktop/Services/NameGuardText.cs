// MystTiq v1.0.0.6: file reviewed for this release (2026-10-05).
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

/// <summary>v1.0.0.2: the Unique player names card's rules and text. Pure, so the harness checks it.</summary>
public static class NameGuardText
{
    /// <summary>The form names are compared in, as the service does: case ignored, spaces trimmed and collapsed.</summary>
    public static string Key(string? name) =>
        string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    public static string Describe(NameGuardConfigDto config) =>
        !config.Enabled ? $"Unique names are off: {config.Claims.Count} names are kept but not checked."
        : config.KickDuplicates ? $"Unique names are on: {config.Claims.Count} names are taken. A player using another account's name is kicked."
        : $"Unique names are on: {config.Claims.Count} names are taken. A player using another account's name is reported, not kicked.";

    /// <summary>The list with the name reserved for the owner (empty: nobody may use it), or null and why not.</summary>
    public static (List<NameClaimDto>? Claims, string Message) Reserve(IEnumerable<NameClaimDto> current, string? name, string? owner, DateTimeOffset now)
    {
        var cleanName = string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (cleanName.Length == 0) return (null, "Enter the name to reserve.");
        var account = (owner ?? string.Empty).Trim();
        if (account.Length == 17 && account.All(char.IsAsciiDigit)) account = "steam_" + account;
        if (account.Any(char.IsWhiteSpace)) return (null, "The owner is a Steam ID such as steam_76561197960287930, or empty for nobody.");
        var key = Key(cleanName);
        var claims = current.Where(c => Key(c.Name) != key).ToList();
        claims.Add(new NameClaimDto { Name = cleanName, OwnerId = account, OwnerName = string.Empty, Reserved = true, ClaimedAt = now });
        claims.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
        return (claims, account.Length == 0 ? "Blocked for everyone once you save." : "Reserved once you save.");
    }
}
