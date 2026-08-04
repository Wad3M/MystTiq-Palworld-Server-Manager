using PalworldManager.Models;
namespace PalworldManager.Services;

public sealed class PlayerMappingEngine
{
    public IReadOnlyList<PlayerMappingRecord> Suggest(IEnumerable<WorldPlayerRecord> imported, IEnumerable<WorldPlayerRecord> destination)
    {
        var targets = destination.ToList(); var result = new List<PlayerMappingRecord>();
        foreach (var source in imported)
        {
            var exact = targets.FirstOrDefault(x => x.PlayerGuid.Equals(source.PlayerGuid, StringComparison.OrdinalIgnoreCase));
            if (exact != null) { result.Add(Map(source, exact, PlayerMappingMethod.ExactGuid, 1.0, true, "Exact player GUID match.")); continue; }
            var platform = !string.IsNullOrWhiteSpace(source.PlatformId) ? targets.FirstOrDefault(x => x.PlatformId.Equals(source.PlatformId, StringComparison.OrdinalIgnoreCase)) : null;
            if (platform != null) { result.Add(Map(source, platform, PlayerMappingMethod.PlatformId, .98, true, "Exact platform identity match.")); continue; }
            var name = !string.IsNullOrWhiteSpace(source.PlayerName) ? targets.Where(x => x.PlayerName.Equals(source.PlayerName, StringComparison.OrdinalIgnoreCase)).ToList() : [];
            if (name.Count == 1) { result.Add(Map(source, name[0], PlayerMappingMethod.ExactName, .70, false, "Name-only matches require confirmation.")); continue; }
            result.Add(new PlayerMappingRecord { SourcePlayerGuid = source.PlayerGuid, Method = source.IsHostCandidate ? PlayerMappingMethod.HostMigration : PlayerMappingMethod.Unmatched, Confidence = source.IsHostCandidate ? .40 : 0, Confirmed = false, Explanation = source.IsHostCandidate ? "Local/co-op host requires a selected destination identity." : "No reliable match was found." });
        }
        return result;
    }

    public IReadOnlyList<WorldIssue> Validate(IEnumerable<PlayerMappingRecord> mappings)
    {
        var rows=mappings.ToList(); var issues=new List<WorldIssue>();
        foreach(var row in rows.Where(x=>!x.Confirmed || string.IsNullOrWhiteSpace(x.DestinationPlayerGuid))) issues.Add(new WorldIssue { Code="PLAYER_MAPPING_UNRESOLVED", Message=$"Player {row.SourcePlayerGuid} is not confirmed.", EntityId=row.SourcePlayerGuid, BlocksActivation=true });
        foreach(var duplicate in rows.Where(x=>!string.IsNullOrWhiteSpace(x.DestinationPlayerGuid)).GroupBy(x=>x.DestinationPlayerGuid,StringComparer.OrdinalIgnoreCase).Where(g=>g.Count()>1)) issues.Add(new WorldIssue { Code="PLAYER_MAPPING_DUPLICATE", Message=$"Multiple imported players map to {duplicate.Key}.", EntityId=duplicate.Key, BlocksActivation=true });
        return issues;
    }

    private static PlayerMappingRecord Map(WorldPlayerRecord s, WorldPlayerRecord d, PlayerMappingMethod method, double confidence, bool confirmed, string explanation) => new() { SourcePlayerGuid=s.PlayerGuid, DestinationPlayerGuid=d.PlayerGuid, Method=method, Confidence=confidence, Confirmed=confirmed, Explanation=explanation };
}
