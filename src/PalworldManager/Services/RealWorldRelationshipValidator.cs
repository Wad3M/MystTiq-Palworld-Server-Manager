using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class RealWorldRelationshipValidator
{
    public WorldValidationReport Validate(WorldSnapshot snapshot)
    {
        var report=new WorldValidationReport(); var players=snapshot.Players.ToDictionary(x=>x.PlayerGuid,StringComparer.OrdinalIgnoreCase);var guilds=snapshot.Guilds.ToDictionary(x=>x.GuildId,StringComparer.OrdinalIgnoreCase);
        AddDuplicates(snapshot.Players.Select(x=>x.PlayerGuid),"DUPLICATE_PLAYER","Duplicate player GUID",report);
        AddDuplicates(snapshot.Guilds.Select(x=>x.GuildId),"DUPLICATE_GUILD","Duplicate guild ID",report);
        AddDuplicates(snapshot.Bases.Select(x=>x.BaseId),"DUPLICATE_BASE","Duplicate base ID",report);
        foreach(var player in snapshot.Players){player.Health=EntityHealth.Healthy;if(string.IsNullOrWhiteSpace(player.SaveFilePath)){player.Health=EntityHealth.Warning;report.Warnings.Add(Issue("PLAYER_SAVE_MISSING",$"Player {Label(player.PlayerName,player.PlayerGuid)} has no matching player save.",player.PlayerGuid,false));}}
        foreach(var guild in snapshot.Guilds)
        {
            guild.Health=EntityHealth.Healthy;
            if(guild.MemberPlayerGuids.Count==0){guild.Health=EntityHealth.Orphaned;report.Errors.Add(Issue("GUILD_EMPTY",$"Guild {Label(guild.GuildName,guild.GuildId)} has no valid members.",guild.GuildId,true));}
            foreach(var member in guild.MemberPlayerGuids.Where(x=>!players.ContainsKey(x))){guild.Health=EntityHealth.Broken;report.Errors.Add(Issue("GUILD_MEMBER_MISSING",$"Guild references missing player {member}.",guild.GuildId,true));}
            if(string.IsNullOrWhiteSpace(guild.LeaderPlayerGuid)||!players.ContainsKey(guild.LeaderPlayerGuid)){guild.Health=EntityHealth.Warning;report.Warnings.Add(Issue("GUILD_LEADER_MISSING",$"Guild {Label(guild.GuildName,guild.GuildId)} has no valid leader.",guild.GuildId,false));}
        }
        foreach(var b in snapshot.Bases){b.Health=EntityHealth.Healthy;if(string.IsNullOrWhiteSpace(b.GuildId)||!guilds.ContainsKey(b.GuildId)){b.Health=EntityHealth.Orphaned;report.Errors.Add(Issue("BASE_ORPHANED",$"Base {Label(b.DisplayName,b.BaseId)} does not reference a valid guild.",b.BaseId,true));}}
        snapshot.Issues=report.Errors.Concat(report.Warnings).ToList(); return report;
    }
    private static void AddDuplicates(IEnumerable<string> ids,string code,string message,WorldValidationReport r){foreach(var g in ids.Where(x=>!string.IsNullOrWhiteSpace(x)).GroupBy(x=>x,StringComparer.OrdinalIgnoreCase).Where(x=>x.Count()>1))r.Errors.Add(Issue(code,message+": "+g.Key,g.Key,true));}
    private static WorldIssue Issue(string c,string m,string id,bool block)=>new(){Code=c,Message=m,EntityId=id,BlocksActivation=block};
    private static string Label(string name,string id)=>string.IsNullOrWhiteSpace(name)?id:name;
}
