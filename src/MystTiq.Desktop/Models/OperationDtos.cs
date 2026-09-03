using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class OperationStageDto
{
    [JsonPropertyName("state")] public string State { get; init; } = string.Empty;
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
    [JsonPropertyName("timestampUtc")] public DateTimeOffset TimestampUtc { get; init; }
}

public sealed class OperationRecordDto
{
    [JsonPropertyName("operationId")] public string OperationId { get; init; } = string.Empty;
    [JsonPropertyName("serverProfileId")] public string ServerProfileId { get; init; } = string.Empty;
    [JsonPropertyName("kind")] public string Kind { get; init; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; init; } = string.Empty;
    [JsonPropertyName("resourceKeys")] public IReadOnlyList<string> ResourceKeys { get; init; } = [];
    [JsonPropertyName("phase")] public string Phase { get; init; } = string.Empty;
    [JsonPropertyName("transactionState")] public string TransactionState { get; init; } = string.Empty;
    [JsonPropertyName("createdUtc")] public DateTimeOffset CreatedUtc { get; init; }
    [JsonPropertyName("updatedUtc")] public DateTimeOffset UpdatedUtc { get; init; }
    [JsonPropertyName("safetyBackup")] public string? SafetyBackup { get; init; }
    [JsonPropertyName("rolledBack")] public bool RolledBack { get; init; }
    [JsonPropertyName("stages")] public IReadOnlyList<OperationStageDto> Stages { get; init; } = [];
    [JsonPropertyName("journalPath")] public string JournalPath { get; init; } = string.Empty;
}
