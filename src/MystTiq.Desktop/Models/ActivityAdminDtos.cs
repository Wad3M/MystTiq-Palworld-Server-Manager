using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class ActivityLogSnapshotDto
{
    [JsonPropertyName("available")] public bool Available { get; init; }
    [JsonPropertyName("fileName")] public string FileName { get; init; } = string.Empty;
    [JsonPropertyName("lines")] public IReadOnlyList<string> Lines { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}

public sealed class PlayerAdminActionRequestDto
{
    [JsonPropertyName("action")] public string Action { get; init; } = string.Empty;
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("item")] public string? Item { get; init; }
}

public sealed class PlayerAdminActionResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("supported")] public bool Supported { get; init; }
    [JsonPropertyName("action")] public string Action { get; init; } = string.Empty;
    [JsonPropertyName("playerId")] public string PlayerId { get; init; } = string.Empty;
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}
