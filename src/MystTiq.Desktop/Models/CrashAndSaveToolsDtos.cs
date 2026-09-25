namespace MystTiq.Desktop.Models;

public sealed class CrashFindingDto
{
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int MatchCount { get; set; }
    public List<string> Evidence { get; set; } = [];

    // v0.7.97.0: known-signature detail. All optional, so a report saved by an older build still
    // loads and simply shows no cause or fixes.
    public string SignatureId { get; set; } = string.Empty;
    public string Cause { get; set; } = string.Empty;
    public List<string> Fixes { get; set; } = [];
    public DateTimeOffset? FirstSeen { get; set; }
    public DateTimeOffset? LastSeen { get; set; }
    public List<string> MentionedMods { get; set; } = [];
    public bool IsNew { get; set; } = true;
    public string Key { get; set; } = string.Empty;

    public string Display => $"{(IsNew ? "NEW · " : "")}{Severity.ToUpperInvariant()} · {Title} · {MatchCount} match(es)";
    public bool HasCause => !string.IsNullOrWhiteSpace(Cause);
    public bool HasFixes => Fixes.Count > 0;
    public string FixesText => string.Join(Environment.NewLine, Fixes.Select((fix, index) => $"{index + 1}. {fix}"));
    public string EvidenceText => string.Join(Environment.NewLine, Evidence);
    public string ModsText => MentionedMods.Count == 0
        ? "No installed mod is named in this evidence."
        : $"Mod(s) named in this evidence: {string.Join(", ", MentionedMods)}. That is a lead, not proof of cause.";
    public string StatusText => IsNew ? "New since the last analysis." : "Already reported in an earlier analysis.";
    public string SeenText => (FirstSeen, LastSeen) switch
    {
        (null, null) => "No timestamps in the matching lines.",
        ({ } first, { } last) when first == last => $"Seen at {last.ToLocalTime():g}.",
        ({ } first, { } last) => $"First seen {first.ToLocalTime():g}, last seen {last.ToLocalTime():g}.",
        _ => string.Empty
    };
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
    public int NewFindings { get; set; }
    public int RepeatedFindings { get; set; }
    public string Display => NewFindings > 0
        ? $"{ObservedAt.ToLocalTime():g} · {Findings.Count} finding(s), {NewFindings} new · {FilesScanned} log(s)"
        : $"{ObservedAt.ToLocalTime():g} · {Findings.Count} finding(s) · {FilesScanned} log(s)";
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
