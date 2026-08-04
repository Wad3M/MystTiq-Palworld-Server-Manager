using PalworldManager.Models;
namespace PalworldManager.Services;
public sealed class GuildDiagnosticsService
{
    public IReadOnlyList<string> Analyze(GuildWorldSnapshot s)
    {
        var r=new List<string>();if(s.Guilds.Count==0)r.Add("No guilds were decoded.");foreach(var g in s.Guilds){if(string.IsNullOrWhiteSpace(g.GuildId))r.Add($"{g.Name}: missing guild ID.");if(g.IsOrphaned)r.Add($"{g.Name}: orphaned leader mapping.");foreach(var m in g.Members.Where(x=>!x.PlayerSaveExists))r.Add($"{g.Name}: player save missing for {m.PlayerName} ({m.PlayerUid}).");}foreach(var p in s.Players.GroupBy(x=>x.PlayerUid.Replace("-",""),StringComparer.OrdinalIgnoreCase).Where(x=>x.Select(y=>y.GuildName).Distinct(StringComparer.OrdinalIgnoreCase).Count()>1))r.Add($"Player {p.Key} appears in multiple guilds.");return r;
    }
}
