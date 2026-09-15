using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class PalInstanceDto
{
    [JsonPropertyName("instanceId")] public string InstanceId { get; init; } = string.Empty;
    [JsonPropertyName("characterId")] public string CharacterId { get; init; } = string.Empty;
    [JsonPropertyName("isBoss")] public bool IsBoss { get; init; }
    [JsonPropertyName("nickName")] public string NickName { get; init; } = string.Empty;
    [JsonPropertyName("level")] public int Level { get; init; }
    [JsonPropertyName("rank")] public int Rank { get; init; }
    [JsonPropertyName("talentHp")] public int TalentHp { get; init; }
    [JsonPropertyName("talentShot")] public int TalentShot { get; init; }
    [JsonPropertyName("talentDefense")] public int TalentDefense { get; init; }
    [JsonPropertyName("gender")] public string Gender { get; init; } = string.Empty;
    [JsonPropertyName("isRarePal")] public bool IsRarePal { get; init; }
    [JsonPropertyName("ownerPlayerId")] public string? OwnerPlayerId { get; init; }
    [JsonPropertyName("ownerPlayerName")] public string? OwnerPlayerName { get; init; }
    public string DisplayName => string.IsNullOrWhiteSpace(NickName) ? CharacterId : $"{NickName} ({CharacterId})";
    public string OwnerDisplay => OwnerPlayerName ?? "Wild / unowned";
}

public sealed class PalEditPreviewDto
{
    [JsonPropertyName("canApply")] public bool CanApply { get; init; }
    [JsonPropertyName("previewToken")] public string PreviewToken { get; init; } = string.Empty;
    [JsonPropertyName("instanceId")] public string InstanceId { get; init; } = string.Empty;
    [JsonPropertyName("palLabel")] public string PalLabel { get; init; } = string.Empty;
    [JsonPropertyName("findings")] public IReadOnlyList<string> Findings { get; init; } = [];
    [JsonPropertyName("expiresUtc")] public DateTimeOffset ExpiresUtc { get; init; }
}

public sealed record PalEditFieldChangesDto(string? NickName, int? Level, int? Rank, int? TalentHp, int? TalentShot, int? TalentDefense, string? Gender, bool? IsRarePal);

public sealed class PalEditResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("transactionId")] public string? TransactionId { get; init; }
    [JsonPropertyName("state")] public string State { get; init; } = string.Empty;
    [JsonPropertyName("safetyBackup")] public string? SafetyBackup { get; init; }
    [JsonPropertyName("rolledBack")] public bool RolledBack { get; init; }
    [JsonPropertyName("journalPath")] public string? JournalPath { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}
