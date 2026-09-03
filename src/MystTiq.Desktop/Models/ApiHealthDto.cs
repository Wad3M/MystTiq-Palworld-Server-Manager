using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class ApiHealthDto
{
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("component")] public string Component { get; init; } = string.Empty;
    [JsonPropertyName("api")] public string Api { get; init; } = string.Empty;
    [JsonPropertyName("apiVersion")] public int ApiVersion { get; init; }
    [JsonPropertyName("version")] public string Version { get; init; } = string.Empty;
    [JsonPropertyName("platform")] public string Platform { get; init; } = string.Empty;
    [JsonPropertyName("authentication")] public bool Authentication { get; init; }
    [JsonPropertyName("tls")] public bool Tls { get; init; }
}
