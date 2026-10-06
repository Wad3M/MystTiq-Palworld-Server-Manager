// MystTiq v1.0.3.0: file reviewed for this release (2026-10-05).
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v1.0.0.5 (asked 2026-10-05: "the bases and guilds sections look too similar and have redundant data"). Bases and
/// Guilds showed the same header, count cards and evidence text, and both lists repeated the guild's name, ID and leader.
/// Now each page shows what belongs to it: a base is a place (where it is on the map, which Pals work there, who owns it);
/// a guild is people (its roster by name, who leads it, who is online) and the bases it holds. Pure, so the harness checks it.
/// </summary>
public static class WorldExplorerCards
{
    /// <summary>One card per base a guild holds, with its decoded location and the Pals working there.</summary>
    public static List<BaseExplorerItemDto> Bases(IEnumerable<GuildExplorerItemDto> guilds, IReadOnlyList<BaseLocationDto> locations, IReadOnlyList<PalLocationDto> pals)
    {
        var cards = new List<BaseExplorerItemDto>();
        foreach (var guild in guilds)
        {
            foreach (var baseId in guild.BaseIds)
            {
                var location = locations.FirstOrDefault(l => string.Equals(l.BaseId, baseId, StringComparison.OrdinalIgnoreCase));
                var workers = pals.Where(p => p.Placement == PalMapLayout.BaseWorker && string.Equals(p.BaseId, baseId, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(p => p.Level).ThenBy(p => PalName(p), StringComparer.CurrentCultureIgnoreCase).ToList();
                cards.Add(new BaseExplorerItemDto
                {
                    BaseId = baseId,
                    GuildId = guild.GuildId,
                    GuildName = guild.GuildName,
                    LeaderPlayerId = guild.LeaderPlayerId,
                    LeaderName = guild.LeaderName,
                    OwnerHealth = guild.Health,
                    Evidence = $"Decoded GroupSaveDataMap guild base_ids ownership reference for {guild.GuildName} ({guild.GuildId}).",
                    X = location?.X,
                    Y = location?.Y,
                    Workers = workers,
                    WorkerSummaryVerbatim = WorkerSummary(workers),
                });
            }
        }
        return cards;
    }

    /// <summary>A guild's members by name, the leader first, then who is online, then by name.</summary>
    public static List<GuildMemberRow> Roster(GuildExplorerItemDto guild, IReadOnlyList<PlayerExplorerItemDto> players)
    {
        var rows = new List<GuildMemberRow>();
        foreach (var memberId in guild.MemberPlayerIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var player = players.FirstOrDefault(p => string.Equals(p.PlayerId, memberId, StringComparison.OrdinalIgnoreCase));
            rows.Add(new GuildMemberRow
            {
                PlayerId = memberId,
                Name = string.IsNullOrWhiteSpace(player?.PlayerName) ? string.Empty : player!.PlayerName,
                IsLeader = string.Equals(memberId, guild.LeaderPlayerId, StringComparison.OrdinalIgnoreCase),
                Online = player?.Online == true,
                LastSeenUtc = player?.SaveLastWriteUtc,
            });
        }
        return rows.OrderByDescending(r => r.IsLeader).ThenByDescending(r => r.Online)
            .ThenBy(r => r.Name.Length == 0 ? 1 : 0).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>The kinds of Pal at work, most common first: "Lamball ×3 · Cattiva ×2 · Depresso", and "+N more" past three.</summary>
    public static string WorkerSummary(IReadOnlyList<PalLocationDto> workers, int shown = 3)
    {
        if (workers.Count == 0) return string.Empty;
        var groups = workers.GroupBy(SpeciesLabel, StringComparer.CurrentCultureIgnoreCase)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase).ToList();
        var parts = groups.Take(shown).Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key).ToList();
        if (groups.Count > shown) parts.Add($"+{groups.Count - shown}");
        return string.Join(" · ", parts);
    }

    /// <summary>A Pal as listed at its base: its nickname when it has one, else its species.</summary>
    public static string PalName(PalLocationDto pal) => !string.IsNullOrWhiteSpace(pal.NickName) ? pal.NickName : SpeciesLabel(pal);

    /// <summary>The species' display name when the game's names are available, else its internal name.</summary>
    public static string SpeciesLabel(PalLocationDto pal) => !string.IsNullOrWhiteSpace(pal.SpeciesName) ? pal.SpeciesName : pal.Species;
}
