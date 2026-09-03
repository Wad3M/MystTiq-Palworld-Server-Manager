using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class ServiceStatusDto
{
    [JsonPropertyName("unitName")] public string? UnitName { get; init; }
    [JsonPropertyName("installed")] public bool Installed { get; init; }
    [JsonPropertyName("enabled")] public bool Enabled { get; init; }
    [JsonPropertyName("activeState")] public string? ActiveState { get; init; }
    [JsonPropertyName("subState")] public string? SubState { get; init; }
    [JsonPropertyName("mainProcessId")] public int? MainProcessId { get; init; }
    [JsonPropertyName("detail")] public string? Detail { get; init; }
}
