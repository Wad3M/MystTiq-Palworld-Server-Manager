using System.Text.Json.Serialization;

namespace MystTiq.Core.Models;

// Anti-Cheat & Save-Integrity Scanning: every rule defaults to Flag (notify + log only) -- an
// admin must explicitly opt a rule into Kick/Ban. Matches this project's existing safety-first
// defaults (Pal Editor's explicit preview/apply, world mutations always requiring the server
// stopped).
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AntiCheatResponse { Flag, Kick, Ban }

public sealed record InvalidSteamIdRule(bool Enabled, AntiCheatResponse Response);

// MaxAllowedLevel defaults to 80 -- Palworld's real vanilla player/Pal level cap since the 1.0
// update (previously 65). Not read from any Palworld server config because the game does not
// expose a server-side "max level" setting outside of mods -- this must stay admin-adjustable
// rather than hardcoded, since a modded server can legitimately raise it.
public sealed record ImpossibleLevelRule(bool Enabled, int MaxAllowedLevel, AntiCheatResponse Response);

// Reuses the exact bounds HeadlessPalEditService.ValidateChanges already enforces on write
// (Talent 0-100, Rank 0-5) plus ImpossibleLevelRule.MaxAllowedLevel for a Pal's Level -- not
// redefined here.
public sealed record PalStatAnomalyRule(bool Enabled, AntiCheatResponse Response);

public sealed record AntiCheatRuleSet
{
    public InvalidSteamIdRule InvalidSteamId { get; init; } = new(true, AntiCheatResponse.Flag);
    public ImpossibleLevelRule ImpossibleLevel { get; init; } = new(true, 80, AntiCheatResponse.Flag);
    public PalStatAnomalyRule PalStatAnomaly { get; init; } = new(true, AntiCheatResponse.Flag);
}

public sealed record AntiCheatFinding(
    string RuleKey,
    string Severity,
    string? PlayerId,
    string? PlayerName,
    string Detail,
    DateTimeOffset ObservedAt,
    AntiCheatResponse ResponseTaken);
