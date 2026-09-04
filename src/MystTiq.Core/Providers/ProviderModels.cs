namespace MystTiq.Core.Providers;

// v0.6.5.0 "Provider Framework & Configuration Intelligence": decouples player-moderation actions
// (kick/ban/etc.) from a single hardcoded interface (vanilla REST). Real, working slice: one
// capability (player moderation) with two real providers (REST, RCON) and health-based fallback --
// scoped down from the roadmap's full 7-capability/6-provider-type list (presence, world data,
// chat, whitelist, teleport, commands x REST/GameData/RCON/save/PalDefender/MOD), which stays an
// explicit deferred list rather than being built decoratively across every named capability.
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ProviderHealth { Unknown, Healthy, Degraded, Unavailable, Misconfigured, Unsupported }

public sealed record ProviderDescriptor(string ProviderId, string DisplayName, ProviderHealth Health, string Detail);

public sealed record PlayerModerationResult(bool Success, bool Supported, string ProviderId, string Action, string PlayerId, string Message);

/// <summary>
/// One real interface a player-moderation action (kick/ban today) can be executed through.
/// Vanilla Palworld exposes two independent admin surfaces (REST, RCON) with different
/// enable/config requirements; a server that only enables one of them should not lose kick/ban
/// just because the other happens to be MystTiq's historical default.
/// </summary>
public interface IPlayerModerationProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    Task<ProviderDescriptor> GetHealthAsync(CancellationToken cancellationToken);
    bool SupportsAction(string action);
    Task<PlayerModerationResult> ExecuteAsync(string action, string playerId, string? message, CancellationToken cancellationToken);
}
