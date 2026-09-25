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
    IReadOnlyList<DiscordRoleMapping> RoleMappings,
    // v0.7.95.0: live features. All optional with defaults so a discord-bot.json written by an earlier
    // version still loads unchanged. StatusMessageId is internal bookkeeping (the one message the bot
    // keeps editing); the Desktop form never sets it and a save never wipes it.
    string? StatusChannelId = null,
    string? StatusMessageId = null,
    string? EventsChannelId = null,
    bool ShowPresence = true)
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
    DiscordBotConnectionState ConnectionState,
    string? StatusChannelId = null,
    string? EventsChannelId = null,
    bool ShowPresence = true);

// Discord ids ("snowflakes") are 15-20 digit numbers. Shared by the server (which refuses anything else
// for a channel id) and the Desktop form (which checks before sending), so both agree.
public static class DiscordSnowflake
{
    public static bool TryParse(string? text, out ulong id)
    {
        id = 0;
        var trimmed = (text ?? string.Empty).Trim();
        return trimmed.Length is >= 15 and <= 20 && trimmed.All(char.IsAsciiDigit) && ulong.TryParse(trimmed, out id) && id > 0;
    }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiscordBotConnectionState { NotConfigured, Disconnected, Connecting, Connected, Failed }
