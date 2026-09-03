using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class LifecycleOperationResultDto
{
    [JsonPropertyName("exitCode")] public int ExitCode { get; init; }
    [JsonPropertyName("snapshot")] public ServerStatusDto? Snapshot { get; init; }
    [JsonPropertyName("forced")] public bool Forced { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("success")] public bool Success { get; init; }
}
