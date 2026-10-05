// MystTiq v1.0.1.0: file reviewed for this release (2026-10-05).
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
    [JsonPropertyName("steamId")] public string SteamId { get; init; } = string.Empty;
    [JsonPropertyName("userId")] public string UserId { get; init; } = string.Empty;
    [JsonPropertyName("evidence")] public string Evidence { get; init; } = string.Empty;

    public bool HasSteamId => !string.IsNullOrWhiteSpace(SteamId);
    public string SteamIdVerbatim => HasSteamId ? SteamId : "—";
    public string UserIdVerbatim => string.IsNullOrWhiteSpace(UserId) ? "—" : UserId;
    public string SteamIdentityVerbatim => HasSteamId ? SteamId : (string.IsNullOrWhiteSpace(UserId) ? "—" : UserId);
    public string SteamProfileUrl => HasSteamId ? $"https://steamcommunity.com/profiles/{SteamId.Trim()}" : string.Empty;

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

    // v1.0.0.5: the Guilds page is about people: the roster by name and the bases the guild holds (filled by the Desktop).
    [JsonIgnore] public IReadOnlyList<GuildMemberRow> Roster { get; set; } = [];
    [JsonIgnore] public IReadOnlyList<BaseExplorerItemDto> BaseCards { get; set; } = [];
    public string GuildNameVerbatim => GuildName;
    public string LeaderNameVerbatim => string.IsNullOrWhiteSpace(LeaderName) ? "—" : LeaderName;
    public string ShortIdVerbatim => GuildId.Length > 8 ? GuildId[..8] : GuildId;
    public string MembersBasesText => $"{MemberCount} members · {BaseCount} bases";
    public int OnlineCount => Roster.Count(r => r.Online);
    public string OnlineText => OnlineCount > 0 ? $"{OnlineCount} online" : string.Empty;
}

// v1.0.0.5: one member of a guild, by name, for the Guilds page's roster.
public sealed class GuildMemberRow
{
    public string PlayerId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool IsLeader { get; init; }
    public bool Online { get; init; }
    public DateTimeOffset? LastSeenUtc { get; init; }

    public string NameVerbatim => Name.Length > 0 ? Name : (PlayerId.Length > 8 ? PlayerId[..8] : PlayerId);
    public string RoleText => IsLeader ? "Leader" : "Member";
    public string StatusText => Online ? "Online" : LastSeenUtc is { } seen ? $"Last saved {seen.ToLocalTime():g}" : "Offline";
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

    // v1.0.0.5: the Bases page is about places: where the base is and which Pals work there.
    public double? X { get; init; }
    public double? Y { get; init; }
    public IReadOnlyList<PalLocationDto> Workers { get; init; } = [];
    public string WorkerSummaryVerbatim { get; init; } = string.Empty;
    public bool HasLocation => X.HasValue && Y.HasValue;
    public string LocationVerbatim => HasLocation ? MystTiq.Desktop.Services.PalworldMapCoordinates.Describe(X!.Value, Y!.Value) : "—";
    public string LocationText => HasLocation ? "On the map" : "No location in the save";
    public string ShortIdVerbatim => BaseId.Length > 8 ? BaseId[..8] : BaseId;
    public string GuildNameVerbatim => GuildName;
    public string LeaderNameVerbatim => string.IsNullOrWhiteSpace(LeaderName) ? "—" : LeaderName;
    public string WorkerCountText => Workers.Count == 0 ? "No Pals working" : $"{Workers.Count} Pals working";
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

    // v1.0.0.5: a worker as listed on the Bases page.
    public string WorkerNameVerbatim => MystTiq.Desktop.Services.WorldExplorerCards.PalName(this);
    public string LevelText => $"Lv {Level}";
    public string OwnerNameVerbatim => OwnerName;
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
