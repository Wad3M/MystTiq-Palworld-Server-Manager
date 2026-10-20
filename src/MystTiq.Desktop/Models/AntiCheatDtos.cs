// MystTiq v1.1.2.0: file reviewed for this release (2026-10-19).
namespace MystTiq.Desktop.Models;

public sealed class InvalidSteamIdRuleDto
{
    public bool Enabled { get; set; } = true;
    public string Response { get; set; } = "Flag";
}

public sealed class ImpossibleLevelRuleDto
{
    public bool Enabled { get; set; } = true;
    public int MaxAllowedLevel { get; set; } = 80;
    public string Response { get; set; } = "Flag";
}

public sealed class PalStatAnomalyRuleDto
{
    public bool Enabled { get; set; } = true;
    public string Response { get; set; } = "Flag";
}

// v1.0.9.3 (roadmap B-2): reports only, so there is no response to choose.
public sealed class LevelGapRuleDto
{
    public bool Enabled { get; set; } = true;
    public int SlackLevels { get; set; } = 3;
}

public sealed class AntiCheatRuleSetDto
{
    public InvalidSteamIdRuleDto InvalidSteamId { get; set; } = new();
    public ImpossibleLevelRuleDto ImpossibleLevel { get; set; } = new();
    public PalStatAnomalyRuleDto PalStatAnomaly { get; set; } = new();
    public LevelGapRuleDto LevelGap { get; set; } = new();
}

public sealed class AntiCheatFindingDto
{
    public string RuleKey { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public string Detail { get; set; } = string.Empty;
    public DateTimeOffset ObservedAt { get; set; }
    public string ResponseTaken { get; set; } = "Flag";
}
