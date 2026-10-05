// MystTiq v1.0.0.3: file reviewed for this release (2026-10-05).
// MystTiq v1.0.0.1: startup-window console capture DTOs.
using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class ConsoleCaptureStatusDto
{
    [JsonPropertyName("packagedProxyAvailable")] public bool PackagedProxyAvailable { get; init; }
    [JsonPropertyName("installed")] public bool Installed { get; init; }
    [JsonPropertyName("packagedSourcePath")] public string? PackagedSourcePath { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}

public sealed class ConsoleCaptureOperationResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
    [JsonPropertyName("cancellationRequestedButIgnored")] public bool CancellationRequestedButIgnored { get; init; }
}
