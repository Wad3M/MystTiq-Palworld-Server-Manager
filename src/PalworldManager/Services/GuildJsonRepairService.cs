using System.Text.Json.Nodes;
using PalworldManager.Models;
namespace PalworldManager.Services;
public sealed class GuildJsonRepairService
{
    public void Apply(string jsonPath,GuildRepairPlan plan)
    {
        var root=JsonNode.Parse(File.ReadAllText(jsonPath))??throw new InvalidOperationException("Decoded save JSON is empty.");
        foreach(var op in plan.Operations){var guild=FindGuild(root,op.GuildId)??throw new InvalidOperationException($"Guild {op.GuildId} was not found in decoded save data.");switch(op.Type){case GuildRepairOperationType.ClaimOrphanedGuild:case GuildRepairOperationType.AddPlayerToGuild:AddMember(guild,op.PlayerUid);if(op.Type==GuildRepairOperationType.ClaimOrphanedGuild)SetScalar(guild,"admin_player_uid",op.PlayerUid);break;case GuildRepairOperationType.TransferLeadership:SetScalar(guild,"admin_player_uid",op.PlayerUid);AddMember(guild,op.PlayerUid);break;case GuildRepairOperationType.RepairOwnershipMappings:NormalizeMembers(guild);break;}}
        File.WriteAllText(jsonPath,root.ToJsonString(new System.Text.Json.JsonSerializerOptions{WriteIndented=false}));
    }
    private static JsonObject? FindGuild(JsonNode node,string id){if(node is JsonObject o){var gid=Read(o,"group_id","guild_id","GuildId","id");if(!string.IsNullOrWhiteSpace(gid)&&gid.Equals(id,StringComparison.OrdinalIgnoreCase))return o;foreach(var kv in o){if(kv.Value is null)continue;var r=FindGuild(kv.Value,id);if(r!=null)return r;}}else if(node is JsonArray a)foreach(var n in a){if(n is null)continue;var r=FindGuild(n,id);if(r!=null)return r;}return null;}
    private static string Read(JsonObject o,params string[] names){foreach(var n in names)if(o.TryGetPropertyValue(n,out var v)&&v!=null){if(v is JsonValue)return v.ToString();if(v is JsonObject vo&&vo.TryGetPropertyValue("value",out var x)&&x!=null)return x.ToString();}return"";}
    private static void SetScalar(JsonObject o,string name,string value){if(o.TryGetPropertyValue(name,out var n)&&n is JsonObject vo&&vo.ContainsKey("value"))vo["value"]=value;else o[name]=value;}
    private static JsonArray GetMembers(JsonObject g){foreach(var n in new[]{"players","members","Members"})if(g.TryGetPropertyValue(n,out var v)){if(v is JsonArray a)return a;if(v is JsonObject o&&o["value"] is JsonArray av)return av;}var created=new JsonArray();g["players"]=created;return created;}
    private static void AddMember(JsonObject g,string uid){var a=GetMembers(g);if(a.OfType<JsonObject>().Any(m=>Read(m,"player_uid","PlayerUid","uid").Equals(uid,StringComparison.OrdinalIgnoreCase)))return;a.Add(new JsonObject{{"player_uid",uid},{"player_name",uid}});}
    private static void NormalizeMembers(JsonObject g){var a=GetMembers(g);var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);for(var i=a.Count-1;i>=0;i--){if(a[i] is not JsonObject m){continue;}var uid=Read(m,"player_uid","PlayerUid","uid");if(string.IsNullOrWhiteSpace(uid)||!seen.Add(uid))a.RemoveAt(i);}}
}
