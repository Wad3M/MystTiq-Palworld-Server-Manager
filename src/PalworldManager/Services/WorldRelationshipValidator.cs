using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class WorldRelationshipValidator
{
    public IReadOnlyList<WorldIssue> Validate(WorldSnapshot snapshot)
    {
        var issues = new List<WorldIssue>();
        var players = snapshot.Players.Where(x => !string.IsNullOrWhiteSpace(x.PlayerGuid)).ToDictionary(x => x.PlayerGuid, StringComparer.OrdinalIgnoreCase);
        var guilds = snapshot.Guilds.Where(x => !string.IsNullOrWhiteSpace(x.GuildId)).ToDictionary(x => x.GuildId, StringComparer.OrdinalIgnoreCase);
        foreach (var guild in snapshot.Guilds)
        {
            if (string.IsNullOrWhiteSpace(guild.LeaderPlayerGuid) || !players.ContainsKey(guild.LeaderPlayerGuid))
                issues.Add(new WorldIssue { Code = "GUILD_MISSING_LEADER", Message = $"Guild {guild.GuildName} has no valid leader.", EntityId = guild.GuildId, BlocksActivation = true });
            foreach (var member in guild.MemberPlayerGuids.Distinct(StringComparer.OrdinalIgnoreCase))
                if (!players.ContainsKey(member)) issues.Add(new WorldIssue { Code = "GUILD_MISSING_MEMBER", Message = $"Guild {guild.GuildName} references missing player {member}.", EntityId = guild.GuildId });
        }
        foreach (var worldBase in snapshot.Bases)
            if (string.IsNullOrWhiteSpace(worldBase.GuildId) || !guilds.ContainsKey(worldBase.GuildId))
                issues.Add(new WorldIssue { Code = "BASE_ORPHANED", Message = $"Base {worldBase.DisplayName} has no valid guild.", EntityId = worldBase.BaseId, BlocksActivation = true });
        snapshot.Issues = issues;
        return issues;
    }
}
