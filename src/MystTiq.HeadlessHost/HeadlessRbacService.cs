using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MystTiq.Core.Security;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// Additive on top of the existing single shared bearer token (see MystTiqPrincipal.LegacyOwner) --
// a request that matches the legacy token never touches this store. Tokens are hashed (SHA-256)
// before persistence; the plaintext is returned exactly once, at creation, the same convention
// the existing `api-token-create` CLI command already uses.
//
// v0.6.2.0: fleet-level singleton (one principal store for the whole app, not one per server
// profile) -- a principal's per-server restriction is an attribute on the grant
// (ScopedServerProfileId), enforced by RequireRole at the route layer, not a separate store per
// server.
public sealed class HeadlessRbacService
{
    private readonly object gate = new();
    private readonly string statePath;
    private readonly Dictionary<string, HeadlessPrincipalRecord> principals;

    public HeadlessRbacService(IServerPathProfile paths)
    {
        var root = Path.Combine(paths.ManagerRuntimeRoot, "security");
        Directory.CreateDirectory(root);
        statePath = Path.Combine(root, "principals.json");
        principals = Load();
    }

    public (MystTiqPrincipal Principal, string PlaintextToken) CreatePrincipal(string name, MystTiqRole role, DateTimeOffset? expiresUtc, string? scopedServerProfileId = null)
    {
        var id = Guid.NewGuid().ToString("N");
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var record = new HeadlessPrincipalRecord(
            id,
            string.IsNullOrWhiteSpace(name) ? "Unnamed principal" : name.Trim(),
            role,
            Hash(token),
            DateTimeOffset.UtcNow,
            expiresUtc,
            true,
            string.IsNullOrWhiteSpace(scopedServerProfileId) ? null : scopedServerProfileId.Trim());
        lock (gate) { principals[id] = record; Persist(); }
        return (ToPrincipal(record), token);
    }

    public bool RevokePrincipal(string id)
    {
        lock (gate)
        {
            if (!principals.TryGetValue(id, out var record)) return false;
            principals[id] = record with { Enabled = false };
            Persist();
            return true;
        }
    }

    public IReadOnlyList<MystTiqPrincipal> ListPrincipals()
    {
        lock (gate) return principals.Values.Select(ToPrincipal).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public MystTiqPrincipal? Authenticate(string suppliedToken)
    {
        var hash = Hash(suppliedToken);
        lock (gate)
        {
            var record = principals.Values.FirstOrDefault(p => p.Enabled && HeadlessSecretFileService.FixedTimeEquals(p.TokenSha256, hash));
            if (record is null) return null;
            var principal = ToPrincipal(record);
            return principal.IsExpired ? null : principal;
        }
    }

    private static MystTiqPrincipal ToPrincipal(HeadlessPrincipalRecord record) =>
        new() { Id = record.Id, Name = record.Name, Role = record.Role, ExpiresUtc = record.ExpiresUtc, ScopedServerProfileId = record.ScopedServerProfileId };

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private Dictionary<string, HeadlessPrincipalRecord> Load()
    {
        try
        {
            if (!File.Exists(statePath)) return new(StringComparer.Ordinal);
            var list = JsonSerializer.Deserialize<List<HeadlessPrincipalRecord>>(File.ReadAllText(statePath)) ?? [];
            return list.ToDictionary(p => p.Id, StringComparer.Ordinal);
        }
        catch { return new(StringComparer.Ordinal); }
    }

    // Caller must already hold gate.
    private void Persist()
    {
        var partial = statePath + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(principals.Values.ToList(), new JsonSerializerOptions { WriteIndented = true }));
        File.Move(partial, statePath, true);
    }
}

public sealed record HeadlessPrincipalRecord(string Id, string Name, MystTiqRole Role, string TokenSha256, DateTimeOffset CreatedUtc, DateTimeOffset? ExpiresUtc, bool Enabled, string? ScopedServerProfileId = null);
public sealed record HeadlessCreatePrincipalRequest(string Name, MystTiqRole Role, DateTimeOffset? ExpiresUtc, string? ScopedServerProfileId = null);
public sealed record HeadlessCreatePrincipalResult(MystTiqPrincipalDto Principal, string PlaintextToken);
public sealed record MystTiqPrincipalDto(string Id, string Name, MystTiqRole Role, DateTimeOffset? ExpiresUtc, bool IsExpired, string? ScopedServerProfileId = null);
