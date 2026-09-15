namespace MystTiq.Core.Models;

// v0.7.15.0: backlog item -- a ban that auto-lifts after a set duration instead of staying
// permanent until someone remembers to Unban. PlayerId matches HeadlessPlayerSnapshot.PlayerId,
// the same identity space Kick/Ban/Unban/Whitelist already operate on. The underlying ban/unban
// RCON commands are unchanged (RconPlayerModerationProvider already supports both) -- this only
// adds a persisted expiry timestamp and a poll-driven sweep that calls the existing unban path
// once it elapses, the same "reuse the existing moderation route, add a timer" shape as
// HeadlessWhitelistService.
public sealed record TemporaryBanEntry(string PlayerId, string PlayerName, string Reason, DateTimeOffset ExpiresAtUtc);

public sealed record TemporaryBanConfig(IReadOnlyList<TemporaryBanEntry> Entries)
{
    public static TemporaryBanConfig Default { get; } = new([]);
}
