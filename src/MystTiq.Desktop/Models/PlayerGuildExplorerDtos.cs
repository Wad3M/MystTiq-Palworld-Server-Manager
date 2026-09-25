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

// v0.7.92.0: a base's decoded world coordinates (Level.sav.json BaseCampSaveData
// spawn_transform.translation), attributed to its owning guild when one claims it.
public sealed class BaseLocationDto
{
    [JsonPropertyName("baseId")] public string BaseId { get; init; } = string.Empty;
    [JsonPropertyName("guildId")] public string GuildId { get; init; } = string.Empty;
    [JsonPropertyName("guildName")] public string GuildName { get; init; } = string.Empty;
    [JsonPropertyName("x")] public double X { get; init; }
    [JsonPropertyName("y")] public double Y { get; init; }
}

// v0.7.100.0: where a player character last was, from the decoded save. Drawn on the map for players
// who are not online; online players use their live position instead.
public sealed class PlayerLocationDto
{
    [JsonPropertyName("playerId")] public string PlayerId { get; init; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("x")] public double X { get; init; }
    [JsonPropertyName("y")] public double Y { get; init; }
    [JsonPropertyName("z")] public double Z { get; init; }
}

// v0.8.11.0: an owned Pal out in the world (working at a base, or in its owner's party), at the last position the
// save recorded. Palbox Pals are only counted (PalSummaryDto), since their saved spot is not where they are.
public sealed class PalLocationDto
{
    [JsonPropertyName("instanceId")] public string InstanceId { get; init; } = string.Empty;
    [JsonPropertyName("species")] public string Species { get; init; } = string.Empty;
    [JsonPropertyName("isAlpha")] public bool IsAlpha { get; init; }
    [JsonPropertyName("level")] public int Level { get; init; }
    [JsonPropertyName("nickName")] public string NickName { get; init; } = string.Empty;
    [JsonPropertyName("ownerPlayerId")] public string OwnerPlayerId { get; init; } = string.Empty;
    [JsonPropertyName("ownerName")] public string OwnerName { get; init; } = string.Empty;
    [JsonPropertyName("placement")] public string Placement { get; init; } = string.Empty;
    [JsonPropertyName("baseId")] public string BaseId { get; init; } = string.Empty;
    [JsonPropertyName("guildName")] public string GuildName { get; init; } = string.Empty;
    [JsonPropertyName("x")] public double X { get; init; }
    [JsonPropertyName("y")] public double Y { get; init; }
    // v0.8.13.0: the species' display name ("Cattiva") when the game's names are available, else empty.
    [JsonPropertyName("speciesName")] public string SpeciesName { get; init; } = string.Empty;
}

public sealed class PalSummaryDto
{
    [JsonPropertyName("totalPals")] public int TotalPals { get; init; }
    [JsonPropertyName("onMap")] public int OnMap { get; init; }
    [JsonPropertyName("inPalbox")] public int InPalbox { get; init; }
    [JsonPropertyName("withoutPosition")] public int WithoutPosition { get; init; }
    [JsonPropertyName("unplaced")] public int Unplaced { get; init; }
}

public sealed class PlayerGuildSnapshotDto
{
    [JsonPropertyName("palLocations")] public IReadOnlyList<PalLocationDto> PalLocations { get; init; } = [];
    [JsonPropertyName("palSummary")] public PalSummaryDto? PalSummary { get; init; }
    [JsonPropertyName("playerLocations")] public IReadOnlyList<PlayerLocationDto> PlayerLocations { get; init; } = [];
    [JsonPropertyName("baseLocations")] public IReadOnlyList<BaseLocationDto> BaseLocations { get; init; } = [];
    [JsonPropertyName("available")] public bool Available { get; init; }
    [JsonPropertyName("semanticAvailable")] public bool SemanticAvailable { get; init; }
    [JsonPropertyName("semanticSource")] public string SemanticSource { get; init; } = string.Empty;
    [JsonPropertyName("activeWorldPath")] public string? ActiveWorldPath { get; init; }
    [JsonPropertyName("decodedLevelJsonPath")] public string? DecodedLevelJsonPath { get; init; }
    [JsonPropertyName("players")] public IReadOnlyList<PlayerExplorerItemDto> Players { get; init; } = [];
    [JsonPropertyName("guilds")] public IReadOnlyList<GuildExplorerItemDto> Guilds { get; init; } = [];
    [JsonPropertyName("abandonedBaseIds")] public IReadOnlyList<string> AbandonedBaseIds { get; init; } = [];
    [JsonPropertyName("warnings")] public IReadOnlyList<string> Warnings { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}
