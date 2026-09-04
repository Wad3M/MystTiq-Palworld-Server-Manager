namespace MystTiq.Desktop.Models;

// v0.6.4.0 unified diagnostics. State mirrors Core's DiagnosticState int values exactly
// (Pass=0,Warning=1,Fail=2,Starting=3,Skipped=4,Unknown=5) -- see NetworkDiagnosticModels.cs's
// own comment for why Unknown was appended rather than inserted first.
public sealed class DiagnosticFindingDto
{
    public string Id { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public int State { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string? ActionKind { get; set; }
    public bool ActionSupported { get; set; }
    public string? UnavailableReason { get; set; }
    public DateTimeOffset ObservedAt { get; set; }

    public string StateText => State switch { 0 => "PASS", 1 => "WARNING", 2 => "FAIL", 3 => "STARTING", 4 => "SKIPPED", _ => "UNKNOWN" };
    public bool CanFix => ActionKind is not null;
}

public sealed class DiagnosticsReportDto
{
    public int OverallHealth { get; set; }
    public string OverallHealthDetail { get; set; } = string.Empty;
    public int Passed { get; set; }
    public int Warnings { get; set; }
    public int Failures { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public List<DiagnosticFindingDto> Findings { get; set; } = [];

    // Mirrors ServerHealthState (Unknown=0, Ready=1, Degraded=2, Attention=3, Transitioning=4).
    public string OverallHealthText => OverallHealth switch { 1 => "READY", 2 => "DEGRADED", 3 => "ATTENTION", 4 => "TRANSITIONING", _ => "UNKNOWN" };
}

public sealed record HeadlessDiagnosticFixResultDto(bool Success, string Message);
