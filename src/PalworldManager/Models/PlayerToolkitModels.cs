namespace PalworldManager.Models;

public sealed record PlayerHealthCheckRow(
    string Component,
    string Status,
    string Confidence,
    string Detail,
    string Recommendation);

public sealed class PlayerHealthReport
{
    public string PlayerKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public int Score { get; init; }
    public string OverallStatus { get; init; } = "Unknown";
    public List<PlayerHealthCheckRow> Checks { get; init; } = [];
    public List<string> DuplicateFindings { get; init; } = [];
    public List<string> RepairRecommendations { get; init; } = [];
}

public sealed record PlayerComparisonRow(string Field, string Source, string Destination, string Result);
