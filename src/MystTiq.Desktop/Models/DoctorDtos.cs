namespace MystTiq.Desktop.Models;

public sealed class DoctorReportDto
{
    public string Version { get; set; } = string.Empty;
    public string Status { get; set; } = "UNKNOWN";
    public int Passed { get; set; }
    public int Warnings { get; set; }
    public int Failures { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public List<DoctorCheckDto> Checks { get; set; } = [];
}

public sealed class DoctorCheckDto
{
    public string Component { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
}
