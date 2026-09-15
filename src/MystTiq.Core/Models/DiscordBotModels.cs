using System.Text.Json.Serialization;
using MystTiq.Core.Security;

namespace MystTiq.Core.Models;

// v0.6.17.0: per-profile config for the real, inbound-capable Discord bot -- distinct from
// HeadlessNotificationRoutingService's outbound-only "Discord" notification channel (a plain
// webhook URL, needs no bot). BotToken is write-only over the REST API, mirroring how
// PalworldSettingsConfigurationService's AdminPassword is never round-tripped to the GUI.
public sealed record DiscordRoleMapping(string DiscordRoleId, MystTiqRole Role);

public sealed record DiscordBotConfiguration(
    bool Enabled,
    string? BotToken,
    string? GuildId,
    string? OwnerDiscordUserId,
    IReadOnlyList<DiscordRoleMapping> RoleMappings)
{
    public static DiscordBotConfiguration Default { get; } = new(false, null, null, null, []);
}

// Returned over the REST API in place of DiscordBotConfiguration -- BotToken is never echoed
// back, only whether one is currently stored (so the Desktop can show "token configured" without
// ever receiving the secret itself).
public sealed record DiscordBotConfigurationView(
    bool Enabled,
    bool TokenConfigured,
    string? GuildId,
    string? OwnerDiscordUserId,
    IReadOnlyList<DiscordRoleMapping> RoleMappings,
    DiscordBotConnectionState ConnectionState);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiscordBotConnectionState { NotConfigured, Disconnected, Connecting, Connected, Failed }
