using PalworldManager.Models;
namespace PalworldManager.Services;
public sealed class GuildRepairPlanner
{
    public GuildRepairPlan ClaimGuild(GuildWorldSnapshot snapshot,string guildId,string playerUid)
    {
        var guild=snapshot.Guilds.SingleOrDefault(g=>g.GuildId.Equals(guildId,StringComparison.OrdinalIgnoreCase))??throw new InvalidOperationException("Guild not found.");
        var player=new GuildPlayerIdentityService().ResolveRequired(snapshot,playerUid);
        return new GuildRepairPlan{WorldPath=snapshot.WorldPath,SourceHash=snapshot.SourceHash,Operations=[new GuildRepairOperation{Type=GuildRepairOperationType.ClaimOrphanedGuild,GuildId=guild.GuildId,PlayerUid=player.PlayerUid,Description=$"Add {player.PlayerName}, set leader, preserve guild ID and {guild.BaseCount} base(s)."}]};
    }
}
