using System.Text.Json;
using PalworldManager.Models;
namespace PalworldManager.Services;
public sealed class GuildJsonMapper
{
    public GuildWorldSnapshot Read(string jsonPath,string worldPath,string levelSavePath)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(jsonPath));
        var snap=new GuildWorldSnapshot { SourcePath=jsonPath,WorldPath=worldPath,LevelSavePath=levelSavePath,DecodedJsonPath=jsonPath,Mode=File.Exists(levelSavePath)?GuildSnapshotMode.DirectLevelSave:GuildSnapshotMode.JsonExport,IsReadOnly=true };
        var discovery = new GuildDiscoveryEngine().DiscoverWithDiagnostics(jsonPath);
        foreach (var record in discovery.Records)
        {
            var guild = new GuildRow
            {
                GuildId = record.GuildId,
                Name = string.IsNullOrWhiteSpace(record.Name) ? "Unnamed Guild" : record.Name,
                LeaderUid = record.LeaderGuid,
                GroupType = record.GuildId.StartsWith("PARTIAL-", StringComparison.OrdinalIgnoreCase) ? "Partial Guild" : "Guild"
            };
            foreach (var memberId in record.MemberGuids.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                guild.Members.Add(new GuildMemberRow
                {
                    PlayerUid = memberId,
                    PlayerName = record.MemberNames.TryGetValue(memberId, out var decodedName) && !string.IsNullOrWhiteSpace(decodedName) ? decodedName : memberId,
                    IsLeader = memberId.Equals(record.LeaderGuid, StringComparison.OrdinalIgnoreCase)
                });
            }
            guild.LeaderName = guild.Members.FirstOrDefault(x => x.IsLeader)?.PlayerName ??
                               (string.IsNullOrWhiteSpace(record.LeaderGuid) ? "Unknown" : record.LeaderGuid);
            foreach (var baseId in record.BaseIds.Distinct(StringComparer.OrdinalIgnoreCase))
                guild.Bases.Add(new GuildBaseRow
                {
                    BaseId = baseId,
                    Name = "Base " + baseId[..Math.Min(8, baseId.Length)],
                    OwnerGuildId = record.GuildId
                });
            snap.Guilds.Add(guild);
        }
        snap.Warnings.AddRange(discovery.Diagnostics.Where(x => x.Contains("partial", StringComparison.OrdinalIgnoreCase)));
        foreach (var rejection in discovery.Rejections.Take(25)) snap.Warnings.Add("Guild candidate rejected: " + rejection);
        // Bases are decoded independently, then merged by guild ID. Records with
        // no resolvable guild are retained as orphan bases instead of discarded.
        try
        {
            var bases = new BaseDiscoveryEngine().Discover(jsonPath);
            foreach (var record in bases)
            {
                var row = new GuildBaseRow
                {
                    BaseId = record.BaseId,
                    Name = string.IsNullOrWhiteSpace(record.Name) ? "Base " + record.BaseId[..Math.Min(8, record.BaseId.Length)] : record.Name,
                    Location = $"{record.X:0.##}, {record.Y:0.##}, {record.Z:0.##}",
                    OwnerGuildId = record.GuildId
                };
                var owner = snap.Guilds.FirstOrDefault(g => Normalize(g.GuildId) == Normalize(record.GuildId));
                owner ??= snap.Guilds.FirstOrDefault(g => g.Bases.Any(b => Normalize(b.BaseId) == Normalize(record.BaseId)));
                if (owner is null)
                {
                    snap.Warnings.Add($"Unmatched base retained by Bases discovery only: {row.BaseId} (owner guild {record.GuildId})");
                    continue;
                }
                var existingBase = owner.Bases.FirstOrDefault(b => Normalize(b.BaseId) == Normalize(row.BaseId));
                if (existingBase is null) owner.Bases.Add(row);
                else
                {
                    existingBase.Name = row.Name;
                    existingBase.Location = row.Location;
                    existingBase.OwnerGuildId = string.IsNullOrWhiteSpace(row.OwnerGuildId) ? owner.GuildId : row.OwnerGuildId;
                }
            }
        }
        catch (Exception ex) { snap.Warnings.Add("Base scan was partial: " + ex.Message); }
        ResolvePlayerSaves(snap);
        if (snap.Guilds.Count == 0) snap.Warnings.Add("No complete guild records decoded. Partial player and base records remain visible where available.");
        return snap;
    }
    private static void ResolvePlayerSaves(GuildWorldSnapshot snap)
    {
        var dir=Path.Combine(snap.WorldPath,"Players");
        var files=new PlayerSaveDiscoveryService().DiscoverFromPlayersDirectory(dir).Accepted
            .ToDictionary(x=>x.PlayerId,x=>x.Path,StringComparer.OrdinalIgnoreCase);
        foreach(var g in snap.Guilds) foreach(var m in g.Members){ var compact=m.PlayerUid.Replace("-",""); m.PlayerSaveExists=files.ContainsKey(m.PlayerUid)||files.ContainsKey(compact); snap.Players.Add(new GuildWorldPlayerRow{PlayerUid=m.PlayerUid,PlayerName=m.PlayerName,GuildName=g.Name,Role=m.Role,PlayerSaveExists=m.PlayerSaveExists,SavePath=files.GetValueOrDefault(m.PlayerUid,files.GetValueOrDefault(compact,"")),Source="Level.sav guild"}); }
        foreach(var kv in files) if(!snap.Players.Any(p=>p.PlayerUid.Replace("-","").Equals(kv.Key,StringComparison.OrdinalIgnoreCase))) snap.Players.Add(new GuildWorldPlayerRow{PlayerUid=kv.Key,PlayerName="Unknown Player",GuildName="Unassigned",Role="Unassigned",PlayerSaveExists=true,SavePath=kv.Value,Source="Player save"});
    }
    private static string Normalize(string? value) => new((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    private static IEnumerable<JsonElement> Walk(JsonElement e){yield return e;if(e.ValueKind==JsonValueKind.Object)foreach(var p in e.EnumerateObject())foreach(var n in Walk(p.Value))yield return n;else if(e.ValueKind==JsonValueKind.Array)foreach(var a in e.EnumerateArray())foreach(var n in Walk(a))yield return n;}
    private static string Text(JsonElement e,params string[] names){foreach(var n in names)if(e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(n,out var p)){if(p.ValueKind==JsonValueKind.String)return p.GetString()??"";if(p.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)return p.ToString();if(p.ValueKind==JsonValueKind.Object&&p.TryGetProperty("value",out var v))return v.ValueKind==JsonValueKind.String?v.GetString()??"":v.ToString();}return "";}
    private static bool HasArray(JsonElement e,params string[] names)=>Array(e,names).HasValue;
    private static JsonElement? Array(JsonElement e,params string[] names){foreach(var n in names)if(e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(n,out var p)){if(p.ValueKind==JsonValueKind.Array)return p;if(p.ValueKind==JsonValueKind.Object&&p.TryGetProperty("value",out var v)&&v.ValueKind==JsonValueKind.Array)return v;}return null;}
}
