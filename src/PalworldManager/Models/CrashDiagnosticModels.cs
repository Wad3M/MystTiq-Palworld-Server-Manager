namespace PalworldManager.Models;

public sealed class CrashDiagnosticReport
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public int ExitCode { get; init; }
    public string Phase { get; init; } = "Unknown";
    public string Result { get; init; } = "Unknown";
    public string Severity { get; init; } = "Info";
    public IReadOnlyList<string> EnabledMods { get; init; } = [];
    public IReadOnlyList<string> RecentEvidence { get; init; } = [];
    public string Summary { get; init; } = "No diagnostic summary.";
    public string LikelyContributor { get; init; } = "No clear contributor";
    public string Confidence { get; init; } = "Unknown";
    public string ConfidenceReason { get; init; } = "No weighted evidence was available.";
    public string RuntimeLayer { get; init; } = "Unknown";
    public string ActiveContext { get; init; } = "Unknown";
    public string NearbyActivity { get; init; } = "None identified";
    public IReadOnlyList<string> RankedSuspects { get; init; } = [];
    public string TriggerEvidence { get; init; } = "No direct error evidence was identified.";
    public string ReportPath { get; init; } = "";

    public string TimestampDisplay => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    public string EnabledModsDisplay => EnabledMods.Count == 0 ? "None" : string.Join(", ", EnabledMods);
}
