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
}

public sealed class ComponentVersionSnapshotDto
{
    [JsonPropertyName("components")] public IReadOnlyList<ComponentVersionDto> Components { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
}
