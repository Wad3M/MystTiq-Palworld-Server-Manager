using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class EnvironmentChecklistSnapshotDto
{
    [JsonPropertyName("readyCount")] public int ReadyCount { get; init; }
    [JsonPropertyName("totalCount")] public int TotalCount { get; init; }
    [JsonPropertyName("items")] public IReadOnlyList<EnvironmentChecklistItemDto> Items { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
}

public sealed class EnvironmentChecklistItemDto
{
    [JsonPropertyName("component")] public string Component { get; init; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("location")] public string Location { get; init; } = string.Empty;
    [JsonPropertyName("details")] public string Details { get; init; } = string.Empty;
    [JsonPropertyName("action")] public string Action { get; init; } = string.Empty;
    [JsonPropertyName("actionSupported")] public bool ActionSupported { get; init; } = true;
    [JsonPropertyName("unavailableReason")] public string? UnavailableReason { get; init; }
    public string ActionDisplay => ActionSupported ? Action : "BACKEND REQUIRED";
    public string ActionToolTip => ActionSupported ? Details : (UnavailableReason ?? "A safe headless/API implementation is required before this action can be enabled.");
    public bool IsReady => Status.Equals("READY", StringComparison.OrdinalIgnoreCase);
    public bool IsDisabled => Status.Equals("DISABLED", StringComparison.OrdinalIgnoreCase) || Status.Equals("OPTIONAL", StringComparison.OrdinalIgnoreCase);
    public bool IsMissing => !IsReady && !IsDisabled;
    public bool NeedsAttention => !IsReady;
}
