namespace MystTiq.Desktop.Models;

public sealed class CrashFindingDto
{
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int MatchCount { get; set; }
    public List<string> Evidence { get; set; } = [];
    public string Display => $"{Severity.ToUpperInvariant()} · {Title} · {MatchCount} match(es)";
}

public sealed class CrashAnalysisSnapshotDto
{
    public string Id { get; set; } = string.Empty;
    public DateTimeOffset ObservedAt { get; set; }
    public int FilesScanned { get; set; }
    public int LinesScanned { get; set; }
    public List<CrashFindingDto> Findings { get; set; } = [];
    public List<string> IsolationPlan { get; set; } = [];
    public string Summary { get; set; } = string.Empty;
    public string Display => $"{ObservedAt.ToLocalTime():g} · {Findings.Count} finding(s) · {FilesScanned} log(s)";
}

public sealed class SaveToolsTestDto
{
    public string Name { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string Display => $"{(Success ? "PASS" : "FAIL")} · {Name} · exit {ExitCode}";
}

public sealed class SaveToolsDiagnosticsDto
{
    public DateTimeOffset ObservedAt { get; set; }
    public bool Ready { get; set; }
    public string? PythonPath { get; set; }
    public string? LegacyConverterPath { get; set; }
    public string? PlmConverterPath { get; set; }
    public string? OodlePath { get; set; }
    public string? ActiveLevelSavePath { get; set; }
    public long ActiveLevelSaveBytes { get; set; }
    public string ActiveLevelSignature { get; set; } = string.Empty;
    public List<SaveToolsTestDto> Tests { get; set; } = [];
    public string Detail { get; set; } = string.Empty;
}

public sealed class SaveFileDto
{
    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastWriteUtc { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public string Display => $"{Category} · {RelativePath} · {SizeBytes:N0} bytes";
}

public sealed class SaveFileInventoryDto
{
    public string SaveRoot { get; set; } = string.Empty;
    public List<SaveFileDto> Items { get; set; } = [];
    public DateTimeOffset ObservedAt { get; set; }
    public string Detail { get; set; } = string.Empty;
}
