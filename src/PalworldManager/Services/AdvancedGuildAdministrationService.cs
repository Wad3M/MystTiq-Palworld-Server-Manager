using PalworldManager.Models;
namespace PalworldManager.Services;
public sealed class AdvancedGuildAdministrationService
{
    public GuildRepairPlan RemoveMember(GuildWorldSnapshot s,string guildId,string playerUid)=>Plan(s,new GuildRepairOperation{Type=GuildRepairOperationType.RemovePlayerFromGuild,GuildId=guildId,PlayerUid=playerUid,Description="Remove obsolete guild member"});
    public GuildRepairPlan TransferBase(GuildWorldSnapshot s,string sourceGuild,string targetGuild,string baseId)=>Plan(s,new GuildRepairOperation{Type=GuildRepairOperationType.TransferBase,GuildId=sourceGuild,TargetGuildId=targetGuild,BaseId=baseId,Description="Transfer base ownership"});
    public GuildRepairPlan MergeGuilds(GuildWorldSnapshot s,string sourceGuild,string targetGuild)=>Plan(s,new GuildRepairOperation{Type=GuildRepairOperationType.MergeGuilds,GuildId=sourceGuild,TargetGuildId=targetGuild,Description="Merge guilds while preserving target guild ID"});
    private static GuildRepairPlan Plan(GuildWorldSnapshot s,GuildRepairOperation op)=>new(){WorldPath=s.WorldPath,SourceHash=s.SourceHash,Operations=[op]};
}
