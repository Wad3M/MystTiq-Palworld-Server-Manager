using System.Text.Json.Serialization;

namespace MystTiq.Core.Security;

// Ordered so `<`/`>=` comparisons work directly for RequireRole gating.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MystTiqRole { Viewer = 0, Operator = 1, Admin = 2, Owner = 3 }

public sealed class MystTiqPrincipal
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required MystTiqRole Role { get; init; }

    // Non-null => a temporary scoped guest grant.
    public DateTimeOffset? ExpiresUtc { get; init; }
    public bool IsExpired => ExpiresUtc.HasValue && ExpiresUtc.Value < DateTimeOffset.UtcNow;

    // v0.6.2.0: non-null => this principal's grant is restricted to one server profile in the
    // fleet; null (the default, and always true for LegacyOwner) => full-fleet access. Checked by
    // RequireRole against the resolved route's server profile, not enforced by the RBAC store
    // itself -- a principal scoped to "shard-2" is simply denied (403) on "shard-1" routes.
    public string? ScopedServerProfileId { get; init; }

    // The existing single shared bearer token (Api.Authentication.TokenFile) resolves to this
    // singleton, full-access, before the RBAC principal store is even checked -- existing
    // zero-config deployments are completely unaffected by RBAC's addition.
    public static MystTiqPrincipal LegacyOwner { get; } =
        new() { Id = "legacy-token", Name = "Legacy shared token", Role = MystTiqRole.Owner };
}
