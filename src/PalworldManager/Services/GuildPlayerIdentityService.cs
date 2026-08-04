using PalworldManager.Models;
namespace PalworldManager.Services;
public sealed class GuildPlayerIdentityService
{
    public IReadOnlyList<string> Validate(GuildWorldSnapshot snapshot)
    {
        var issues=new List<string>();
        foreach(var p in snapshot.Players){if(string.IsNullOrWhiteSpace(p.PlayerUid))issues.Add("A player record has no UID.");if(p.PlayerSaveExists&&string.IsNullOrWhiteSpace(p.SavePath))issues.Add($"Player {p.PlayerUid} is marked present without a save path.");}
        foreach(var duplicate in snapshot.Players.Where(p=>!string.IsNullOrWhiteSpace(p.PlayerUid)).GroupBy(p=>p.PlayerUid.Replace("-",""),StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1)) issues.Add($"Player {duplicate.Key} appears in multiple identity records.");
        return issues;
    }
    public GuildWorldPlayerRow ResolveRequired(GuildWorldSnapshot snapshot,string uid)
    {var key=uid.Replace("-","");return snapshot.Players.FirstOrDefault(p=>p.PlayerUid.Replace("-","").Equals(key,StringComparison.OrdinalIgnoreCase))??throw new InvalidOperationException($"Player {uid} was not found in Level.sav or Players.");}
}
