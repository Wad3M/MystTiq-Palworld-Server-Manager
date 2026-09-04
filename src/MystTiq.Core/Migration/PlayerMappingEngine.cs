namespace MystTiq.Core.Migration;

// v0.6.3.0 character/account migration. Adapted from the legacy WPF app's
// PlayerMappingEngine.cs/PlayerMappingModels.cs -- the exact-GUID/platform-ID/name/host-candidate
// confidence scoring is ported directly, but the legacy Suggest(imported, destination) API (bulk
// auto-matching many imported players against many existing ones -- built for a "world import"
// scenario) doesn't fit this milestone's actual use case: an admin explicitly picks one source
// player and one already-existing destination player within the SAME live world (e.g. an Xbox
// identity migrating to a freshly-created Steam identity). So this is a single-pair confidence/
// finding evaluator instead of a bulk suggester -- same scoring ideas, adapted shape.
public enum PlayerMappingMethod { ExactGuid, PlatformId, ExactName, HostMigration, Unmatched }

public sealed record PlayerMappingIdentity(string PlayerId, string PlayerName, string PlatformId);

public sealed record PlayerMappingAssessment(PlayerMappingMethod Method, double Confidence, bool Confirmed, string Explanation);

public static class PlayerMappingEngine
{
    // The well-known local/co-op host player GUID -- migrating this identity always needs an
    // explicitly selected destination, never an automatic match.
    public const string HostCandidateGuid = "00000000000000000000000000000001";

    public static PlayerMappingAssessment Assess(PlayerMappingIdentity source, PlayerMappingIdentity destination)
    {
        if (source.PlayerId.Equals(destination.PlayerId, StringComparison.OrdinalIgnoreCase))
            return new(PlayerMappingMethod.ExactGuid, 1.0, true, "Source and destination are the same player identity.");

        if (!string.IsNullOrWhiteSpace(source.PlatformId) &&
            source.PlatformId.Equals(destination.PlatformId, StringComparison.OrdinalIgnoreCase))
            return new(PlayerMappingMethod.PlatformId, 0.98, true, "Source and destination share the same platform identity.");

        if (!string.IsNullOrWhiteSpace(source.PlayerName) &&
            source.PlayerName.Equals(destination.PlayerName, StringComparison.OrdinalIgnoreCase))
            return new(PlayerMappingMethod.ExactName, 0.70, false, "Matched by display name only -- confirm this is the correct destination before applying.");

        if (source.PlayerId.Equals(HostCandidateGuid, StringComparison.OrdinalIgnoreCase))
            return new(PlayerMappingMethod.HostMigration, 0.40, false, "Source is the local/co-op host slot -- confirm the destination identity carefully.");

        return new(PlayerMappingMethod.Unmatched, 0.0, false, "No shared identity signal (GUID, platform ID, or name) was found between source and destination -- confirm this migration manually.");
    }
}
