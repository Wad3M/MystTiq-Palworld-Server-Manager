using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

// v0.7.45.0: Update Center Overhaul -- mirrors MystTiq.HeadlessHost's ComponentVersionInfo.
public sealed class ComponentVersionDto
{
    [JsonPropertyName("group")] public string Group { get; init; } = string.Empty;
    [JsonPropertyName("component")] public string Component { get; init; } = string.Empty;
    [JsonPropertyName("installedVersion")] public string InstalledVersion { get; init; } = string.Empty;
    [JsonPropertyName("latestVersion")] public string LatestVersion { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; init; } = string.Empty;
    [JsonPropertyName("lastChecked")] public DateTimeOffset LastChecked { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;

    public string StatusText => Status switch
    {
        "UpToDate" => "Up to date",
        "UpdateAvailable" => "Update available",
        "NotInstalled" => "Not installed",
        "SelfUpdating" => "Self-updating",
        "CheckManually" => "Check manually",
        "NotApplicable" => "Not applicable",
        _ => "Unknown"
    };

    // v0.7.82.0: direct live feedback ("the update center where it says update available should
    // allow us to click on it to update"). pip is the one row with a real, safe, one-command
    // in-place update (HeadlessComponentUpdateService.UpdatePipAsync); every other row stays
    // read-only comparison data, no fabricated update action for components this app can't safely
    // update unattended.
    public bool CanUpdateInPlace => Component == "pip" && Status == "UpdateAvailable";

    // v0.7.82.0: direct live feedback ("we have a couple that say unknown, we need to do better and
    // have a way to check"). Several components (Python, VC++ Runtime, MSVC Build Tools, PIM/Oodle)
    // genuinely have no reliable unattended "latest version" feed (see
    // HeadlessComponentUpdateService's own header comment) -- rather than fabricate one, this opens
    // the real, official page a human can check manually. Keyed primarily by Component (Source text
    // is shared verbatim across unrelated rows, e.g. both VC++ Runtime and MSVC Build Tools report
    // "Windows Registry (no reliable \"latest\" feed)"), falling back to parsing Source for the
    // GitHub:/PyPI: rows where the label itself already names the real target.
    public string? SourceUrl => Component switch
    {
        "Python Runtime" => "https://www.python.org/downloads/windows/",
        "Visual C++ Runtime" => "https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist",
        "Microsoft C++ Build Tools" => "https://visualstudio.microsoft.com/visual-cpp-build-tools/",
        "PIM/Oodle Decoder" => "https://github.com/Wad3M/MystTiq-Palworld-Server-Manager/tree/main/Tools/palworld-plm-tools",
        ".NET Runtime" => "https://dotnet.microsoft.com/en-us/download/dotnet",
        _ when Source.StartsWith("GitHub: ", StringComparison.Ordinal) => $"https://github.com/{Source["GitHub: ".Length..]}/releases",
        _ when Source.StartsWith("PyPI: ", StringComparison.Ordinal) => $"https://pypi.org/project/{Source["PyPI: ".Length..]}/",
        _ => null
    };
    public bool HasSourceUrl => !string.IsNullOrEmpty(SourceUrl);
}

public sealed class ComponentVersionSnapshotDto
{
    [JsonPropertyName("components")] public IReadOnlyList<ComponentVersionDto> Components { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
}

public sealed class ComponentUpdateResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}
