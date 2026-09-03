using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class GuildOwnershipPreviewDto
{
    [JsonPropertyName("canApply")] public bool CanApply { get; init; }
    [JsonPropertyName("previewToken")] public string PreviewToken { get; init; } = string.Empty;
    [JsonPropertyName("operationType")] public string OperationType { get; init; } = string.Empty;
    [JsonPropertyName("guildId")] public string GuildId { get; init; } = string.Empty;
    [JsonPropertyName("guildName")] public string GuildName { get; init; } = string.Empty;
    [JsonPropertyName("playerId")] public string PlayerId { get; init; } = string.Empty;
    [JsonPropertyName("playerName")] public string PlayerName { get; init; } = string.Empty;
    [JsonPropertyName("findings")] public IReadOnlyList<string> Findings { get; init; } = [];
    [JsonPropertyName("expiresUtc")] public DateTimeOffset ExpiresUtc { get; init; }
}

public sealed class GuildOwnershipResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("transactionId")] public string? TransactionId { get; init; }
    [JsonPropertyName("state")] public string State { get; init; } = string.Empty;
    [JsonPropertyName("safetyBackup")] public string? SafetyBackup { get; init; }
    [JsonPropertyName("rolledBack")] public bool RolledBack { get; init; }
    [JsonPropertyName("journalPath")] public string? JournalPath { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}
