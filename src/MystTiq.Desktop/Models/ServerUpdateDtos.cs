// MystTiq v1.1.5.0: file reviewed for this release (2026-10-25).
using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

/// <summary>v1.1.4.0 (roadmap U-1): GET /api/v1/server/update (HeadlessServerUpdateFlowService.ServerUpdateCard).</summary>
public sealed class ServerUpdateCardDto
{
    [JsonPropertyName("installed")] public string Installed { get; init; } = "Unknown";
    [JsonPropertyName("latest")] public string Latest { get; init; } = "Unknown";
    [JsonPropertyName("state")] public string State { get; init; } = "Unavailable";
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
    [JsonPropertyName("checkedAt")] public DateTimeOffset? CheckedAt { get; init; }
    [JsonPropertyName("job")] public ServerUpdateJobDto? Job { get; init; }
    // v1.1.5.0: MystTiq's own release (the service's check), for the card's MystTiq line.
    [JsonPropertyName("mystTiqInstalled")] public string? MystTiqInstalled { get; init; }
    [JsonPropertyName("mystTiqLatest")] public string? MystTiqLatest { get; init; }
    [JsonPropertyName("mystTiqState")] public string? MystTiqState { get; init; }
    [JsonPropertyName("mystTiqDetail")] public string? MystTiqDetail { get; init; }
}

/// <summary>v1.1.4.0 (roadmap U-1): one run of the server update. Phase: Checking, Stopping, Backing up, Updating, Starting, Done, Failed.</summary>
public sealed class ServerUpdateJobDto
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("phase")] public string Phase { get; init; } = string.Empty;
    [JsonPropertyName("running")] public bool Running { get; init; }
    [JsonPropertyName("success")] public bool? Success { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("startedAt")] public DateTimeOffset StartedAt { get; init; }
    [JsonPropertyName("finishedAt")] public DateTimeOffset? FinishedAt { get; init; }
    [JsonPropertyName("wasRunning")] public bool WasRunning { get; init; }
    [JsonPropertyName("requestedBy")] public string? RequestedBy { get; init; }
}
