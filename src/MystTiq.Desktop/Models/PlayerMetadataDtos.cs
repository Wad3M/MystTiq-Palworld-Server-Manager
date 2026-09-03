using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class PlayerWarningDto
{
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("createdAt")] public DateTimeOffset CreatedAt { get; init; }
    [JsonPropertyName("actor")] public string Actor { get; init; } = string.Empty;

    public string DisplayText => $"{CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm} · {Message}";
}

public sealed class PlayerMetadataDto
{
    [JsonPropertyName("playerId")] public string PlayerId { get; init; } = string.Empty;
    [JsonPropertyName("notes")] public string Notes { get; init; } = string.Empty;
    [JsonPropertyName("warnings")] public IReadOnlyList<PlayerWarningDto> Warnings { get; init; } = [];
    [JsonPropertyName("updatedAt")] public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record PlayerNotesRequestDto([property: JsonPropertyName("notes")] string Notes);
public sealed record PlayerWarningRequestDto([property: JsonPropertyName("message")] string Message);
