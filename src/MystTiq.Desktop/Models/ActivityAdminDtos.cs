using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

public sealed class ActivityLogSnapshotDto
{
    [JsonPropertyName("available")] public bool Available { get; init; }
    [JsonPropertyName("fileName")] public string FileName { get; init; } = string.Empty;
    [JsonPropertyName("lines")] public IReadOnlyList<string> Lines { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}

public sealed class PlayerAdminActionRequestDto
{
    [JsonPropertyName("action")] public string Action { get; init; } = string.Empty;
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("item")] public string? Item { get; init; }
}

public sealed class PlayerAdminActionResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("supported")] public bool Supported { get; init; }
    [JsonPropertyName("action")] public string Action { get; init; } = string.Empty;
    [JsonPropertyName("playerId")] public string PlayerId { get; init; } = string.Empty;
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
}

// v0.6.6.0 Player Registry: persistent per-player identity/session history, sampled on the
// existing /status/poll cadence rather than a new background timer.
public sealed class PlayerRegistryRecordDto
{
    [JsonPropertyName("playerId")] public string PlayerId { get; init; } = string.Empty;
    [JsonPropertyName("lastKnownName")] public string LastKnownName { get; init; } = string.Empty;
    [JsonPropertyName("steamId")] public string SteamId { get; init; } = string.Empty;
    [JsonPropertyName("userId")] public string UserId { get; init; } = string.Empty;
    [JsonPropertyName("firstSeenUtc")] public DateTimeOffset FirstSeenUtc { get; init; }
    [JsonPropertyName("lastSeenUtc")] public DateTimeOffset LastSeenUtc { get; init; }
    [JsonPropertyName("totalSessions")] public int TotalSessions { get; init; }
    [JsonPropertyName("totalPlaytimeMinutes")] public double TotalPlaytimeMinutes { get; init; }
    [JsonPropertyName("currentlyOnline")] public bool CurrentlyOnline { get; init; }
    [JsonPropertyName("currentSessionStartUtc")] public DateTimeOffset? CurrentSessionStartUtc { get; init; }
}

// v0.6.5.0 Provider Framework: health of each player-moderation (kick/ban) provider MystTiq can
// route through -- shown on the Players page so an admin can see, before an action fails, whether
// REST and/or RCON are actually configured on the server.
public sealed class ProviderDescriptorDto
{
    [JsonPropertyName("providerId")] public string ProviderId { get; init; } = string.Empty;
    [JsonPropertyName("displayName")] public string DisplayName { get; init; } = string.Empty;
    [JsonPropertyName("health")] public string Health { get; init; } = string.Empty;
    [JsonPropertyName("detail")] public string Detail { get; init; } = string.Empty;
}
