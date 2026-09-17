using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class PlayerDeletionPreviewDto
{
    [JsonPropertyName("canApply")] public bool CanApply { get; init; }
    [JsonPropertyName("previewToken")] public string PreviewToken { get; init; } = string.Empty;
    [JsonPropertyName("playerId")] public string PlayerId { get; init; } = string.Empty;
    [JsonPropertyName("playerName")] public string PlayerName { get; init; } = string.Empty;
    [JsonPropertyName("findings")] public IReadOnlyList<string> Findings { get; init; } = [];
    [JsonPropertyName("expiresUtc")] public DateTimeOffset ExpiresUtc { get; init; }
}

public sealed class PlayerDeletionResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("transactionId")] public string? TransactionId { get; init; }
    [JsonPropertyName("state")] public string State { get; init; } = string.Empty;
    [JsonPropertyName("safetyBackup")] public string? SafetyBackup { get; init; }
    [JsonPropertyName("journalPath")] public string? JournalPath { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

public sealed class PlayerCopyPreviewDto
{
    [JsonPropertyName("canApply")] public bool CanApply { get; init; }
    [JsonPropertyName("previewToken")] public string PreviewToken { get; init; } = string.Empty;
    [JsonPropertyName("sourcePlayerId")] public string SourcePlayerId { get; init; } = string.Empty;
    [JsonPropertyName("sourcePlayerName")] public string SourcePlayerName { get; init; } = string.Empty;
    [JsonPropertyName("destinationPlayerId")] public string DestinationPlayerId { get; init; } = string.Empty;
    [JsonPropertyName("destinationPlayerName")] public string DestinationPlayerName { get; init; } = string.Empty;
    [JsonPropertyName("findings")] public IReadOnlyList<string> Findings { get; init; } = [];
    [JsonPropertyName("expiresUtc")] public DateTimeOffset ExpiresUtc { get; init; }
}

public sealed class PlayerCopyResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("transactionId")] public string? TransactionId { get; init; }
    [JsonPropertyName("state")] public string State { get; init; } = string.Empty;
    [JsonPropertyName("safetyBackup")] public string? SafetyBackup { get; init; }
    [JsonPropertyName("rolledBack")] public bool RolledBack { get; init; }
    [JsonPropertyName("journalPath")] public string? JournalPath { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}
