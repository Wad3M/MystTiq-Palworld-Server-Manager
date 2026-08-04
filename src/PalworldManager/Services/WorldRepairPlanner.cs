using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class WorldRepairPlanner
{
    public WorldRepairPlan Create(WorldSnapshot snapshot, IEnumerable<PlayerMappingRecord> mappings)
    {
        var plan = new WorldRepairPlan();
        foreach (var map in mappings.Where(x => x.Confirmed && !x.SourcePlayerGuid.Equals(x.DestinationPlayerGuid, StringComparison.OrdinalIgnoreCase)))
            plan.Operations.Add(new WorldRepairOperation { Kind=WorldRepairKind.RebindPlayer, EntityId=map.SourcePlayerGuid, SourceValue=map.SourcePlayerGuid, DestinationValue=map.DestinationPlayerGuid, Description=$"Rebind player {map.SourcePlayerGuid} to {map.DestinationPlayerGuid}.", Confirmed=true });
        foreach (var guild in snapshot.Guilds.Where(g => string.IsNullOrWhiteSpace(g.LeaderPlayerGuid) || !snapshot.Players.Any(p => p.PlayerGuid.Equals(g.LeaderPlayerGuid, StringComparison.OrdinalIgnoreCase))))
            plan.RemainingIssues.Add(new WorldIssue { Code="GUILD_LEADER_REQUIRED", EntityId=guild.GuildId, Message=$"Select a valid leader for {guild.GuildName}.", BlocksActivation=true });
        foreach (var worldBase in snapshot.Bases.Where(b => string.IsNullOrWhiteSpace(b.GuildId) || !snapshot.Guilds.Any(g => g.GuildId.Equals(b.GuildId, StringComparison.OrdinalIgnoreCase))))
            plan.RemainingIssues.Add(new WorldIssue { Code="BASE_GUILD_REQUIRED", EntityId=worldBase.BaseId, Message=$"Assign {worldBase.DisplayName} to a valid guild.", BlocksActivation=true });
        return plan;
    }
}
