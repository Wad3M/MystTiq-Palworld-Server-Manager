using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class PlayerExplorerItemDto
{
    [JsonPropertyName("playerId")] public string PlayerId { get; init; } = string.Empty;
    [JsonPropertyName("playerName")] public string PlayerName { get; init; } = string.Empty;
    [JsonPropertyName("guildId")] public string GuildId { get; init; } = string.Empty;
    [JsonPropertyName("guildName")] public string GuildName { get; init; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;
    [JsonPropertyName("saveExists")] public bool SaveExists { get; init; }
    [JsonPropertyName("saveSizeBytes")] public long SaveSizeBytes { get; init; }
    [JsonPropertyName("saveLastWriteUtc")] public DateTimeOffset? SaveLastWriteUtc { get; init; }
    [JsonPropertyName("online")] public bool Online { get; init; }
    [JsonPropertyName("platform")] public string Platform { get; init; } = string.Empty;
    [JsonPropertyName("ping")] public string Ping { get; init; } = string.Empty;
    [JsonPropertyName("evidence")] public string Evidence { get; init; } = string.Empty;

    public string SaveStateText => SaveExists ? "Save found" : "Missing save";
    public string OnlineText => Online ? "Online" : "Offline / Unknown";
    public string SaveSizeText => SaveExists
        ? $"{SaveSizeBytes / 1024d:F1} KB"
        : "—";
    public string UpdatedText => SaveLastWriteUtc.HasValue
        ? SaveLastWriteUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
        : "—";
}

public sealed class GuildExplorerItemDto
{
    [JsonPropertyName("guildId")] public string GuildId { get; init; } = string.Empty;
    [JsonPropertyName("guildName")] public string GuildName { get; init; } = string.Empty;
    [JsonPropertyName("leaderPlayerId")] public string LeaderPlayerId { get; init; } = string.Empty;
    [JsonPropertyName("leaderName")] public string LeaderName { get; init; } = string.Empty;
    [JsonPropertyName("memberCount")] public int MemberCount { get; init; }
    [JsonPropertyName("baseCount")] public int BaseCount { get; init; }
    [JsonPropertyName("health")] public string Health { get; init; } = string.Empty;
    [JsonPropertyName("memberPlayerIds")] public IReadOnlyList<string> MemberPlayerIds { get; init; } = [];
    [JsonPropertyName("baseIds")] public IReadOnlyList<string> BaseIds { get; init; } = [];

    public string LeaderDisplay => $"{LeaderName} · {LeaderPlayerId}";
    public string CountText => $"{MemberCount} member(s) · {BaseCount} base reference(s)";
}

public sealed class BaseExplorerItemDto
{
    public string BaseId { get; init; } = string.Empty;
    public string GuildId { get; init; } = string.Empty;
    public string GuildName { get; init; } = string.Empty;
    public string LeaderPlayerId { get; init; } = string.Empty;
    public string LeaderName { get; init; } = string.Empty;
    public string OwnerHealth { get; init; } = string.Empty;
    public string Evidence { get; init; } = string.Empty;
}

public sealed class PlayerGuildSnapshotDto
{
    [JsonPropertyName("available")] public bool Available { get; init; }
    [JsonPropertyName("semanticAvailable")] public bool SemanticAvailable { get; init; }
    [JsonPropertyName("semanticSource")] public string SemanticSource { get; init; } = string.Empty;
    [JsonPropertyName("activeWorldPath")] public string? ActiveWorldPath { get; init; }
    [JsonPropertyName("decodedLevelJsonPath")] public string? DecodedLevelJsonPath { get; init; }
    [JsonPropertyName("players")] public IReadOnlyList<PlayerExplorerItemDto> Players { get; init; } = [];
    [JsonPropertyName("guilds")] public IReadOnlyList<GuildExplorerItemDto> Guilds { get; init; } = [];
    [JsonPropertyName("warnings")] public IReadOnlyList<string> Warnings { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}
