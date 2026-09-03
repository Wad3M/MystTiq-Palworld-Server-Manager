using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class BaseOwnershipPreviewDto
{
    [JsonPropertyName("canApply")] public bool CanApply { get; init; }
    [JsonPropertyName("previewToken")] public string PreviewToken { get; init; } = string.Empty;
    [JsonPropertyName("baseId")] public string BaseId { get; init; } = string.Empty;
    [JsonPropertyName("sourceGuildId")] public string SourceGuildId { get; init; } = string.Empty;
    [JsonPropertyName("sourceGuildName")] public string SourceGuildName { get; init; } = string.Empty;
    [JsonPropertyName("targetGuildId")] public string TargetGuildId { get; init; } = string.Empty;
    [JsonPropertyName("targetGuildName")] public string TargetGuildName { get; init; } = string.Empty;
    [JsonPropertyName("findings")] public IReadOnlyList<string> Findings { get; init; } = [];
    [JsonPropertyName("expiresUtc")] public DateTimeOffset ExpiresUtc { get; init; }
}

public sealed class BaseOwnershipResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("transactionId")] public string? TransactionId { get; init; }
    [JsonPropertyName("state")] public string State { get; init; } = string.Empty;
    [JsonPropertyName("safetyBackup")] public string? SafetyBackup { get; init; }
    [JsonPropertyName("rolledBack")] public bool RolledBack { get; init; }
    [JsonPropertyName("journalPath")] public string? JournalPath { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

public sealed class BaseRecoveryPreviewDto
{
    [JsonPropertyName("canApply")] public bool CanApply { get; init; }
    [JsonPropertyName("previewToken")] public string PreviewToken { get; init; } = string.Empty;
    [JsonPropertyName("baseId")] public string BaseId { get; init; } = string.Empty;
    [JsonPropertyName("guildId")] public string GuildId { get; init; } = string.Empty;
    [JsonPropertyName("guildName")] public string GuildName { get; init; } = string.Empty;
    [JsonPropertyName("objectCount")] public int? ObjectCount { get; init; }
    [JsonPropertyName("findings")] public IReadOnlyList<string> Findings { get; init; } = [];
    [JsonPropertyName("expiresUtc")] public DateTimeOffset ExpiresUtc { get; init; }
}
