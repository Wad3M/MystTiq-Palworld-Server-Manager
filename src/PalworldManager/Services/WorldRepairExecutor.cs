using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class WorldRepairExecutor
{
    public void ApplyToSnapshot(WorldSnapshot snapshot, IEnumerable<WorldRepairOperation> operations)
    {
        foreach (var op in operations.Where(x => x.Confirmed))
        {
            switch (op.Kind)
            {
                case WorldRepairKind.RebindPlayer: Rebind(snapshot, op.SourceValue, op.DestinationValue); break;
                case WorldRepairKind.TransferGuildLeadership: snapshot.Guilds.Single(x=>x.GuildId.Equals(op.EntityId,StringComparison.OrdinalIgnoreCase)).LeaderPlayerGuid=op.DestinationValue; break;
                case WorldRepairKind.AssignBaseToGuild: snapshot.Bases.Single(x=>x.BaseId.Equals(op.EntityId,StringComparison.OrdinalIgnoreCase)).GuildId=op.DestinationValue; break;
                case WorldRepairKind.AddPlayerToGuild:
                    var guild=snapshot.Guilds.Single(x=>x.GuildId.Equals(op.EntityId,StringComparison.OrdinalIgnoreCase));
                    if(!guild.MemberPlayerGuids.Contains(op.DestinationValue,StringComparer.OrdinalIgnoreCase)) guild.MemberPlayerGuids.Add(op.DestinationValue);
                    break;
                case WorldRepairKind.RemovePlayerFromGuild: snapshot.Guilds.Single(x=>x.GuildId.Equals(op.EntityId,StringComparison.OrdinalIgnoreCase)).MemberPlayerGuids.RemoveAll(x=>x.Equals(op.SourceValue,StringComparison.OrdinalIgnoreCase)); break;
            }
        }
    }

    private static void Rebind(WorldSnapshot snapshot,string oldId,string newId)
    {
        var player=snapshot.Players.Single(x=>x.PlayerGuid.Equals(oldId,StringComparison.OrdinalIgnoreCase)); player.PlayerGuid=newId;
        foreach(var guild in snapshot.Guilds)
        {
            if(guild.LeaderPlayerGuid.Equals(oldId,StringComparison.OrdinalIgnoreCase)) guild.LeaderPlayerGuid=newId;
            for(var i=0;i<guild.MemberPlayerGuids.Count;i++) if(guild.MemberPlayerGuids[i].Equals(oldId,StringComparison.OrdinalIgnoreCase)) guild.MemberPlayerGuids[i]=newId;
        }
    }
}
