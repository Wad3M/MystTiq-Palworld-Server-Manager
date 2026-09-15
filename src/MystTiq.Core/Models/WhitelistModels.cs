namespace MystTiq.Core.Models;

// v0.7.10.0: named as an explicit deferred roadmap item in Providers/ProviderModels.cs's own
// comment before this -- the last of this session's verified competitive feature gaps. Opt-in
// per-profile: PlayerId matches HeadlessPlayerSnapshot.PlayerId (whichever of SteamID/UID Palworld
// itself reports for that player), the same identity space Kick/Ban/Unban already operate on.
public sealed record WhitelistEntry(string PlayerId, string Label);

public sealed record WhitelistConfig(bool Enabled, IReadOnlyList<WhitelistEntry> Entries)
{
    public static WhitelistConfig Default { get; } = new(false, []);
}
