// MystTiq v1.0.0.4: file reviewed for this release (2026-10-05).
using System.Text.Json;
using MystTiq.Core.Providers;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.0.2, unique player names (asked 2026-10-04: "is there a way to ensure player names are unique and that duplicates
/// can not be used?"; matching ignores case). Palworld knows players by account and lets two of them use the same name.
/// Each name belongs to the first account seen with it, or to the account the owner reserved it for (or to nobody: a
/// blocked name). A player whose name belongs to another account is recorded, a Warning notification is raised and, by
/// default, they are kicked. Palworld only reports a name once the player is in the world, so their character already
/// exists by then; deleting it on Players lets them make one with another name. On first use the names are taken from
/// the players already known, the earliest first. It runs on the same poll as the whitelist and the identity guard.
/// </summary>
public sealed class HeadlessNameGuardService
{
    private const int MaximumEvents = 200;
    private readonly HeadlessActivityLogService activity;
    private readonly HeadlessNotificationService notifications;
    private readonly PlayerModerationCoordinator playerModeration;
    private readonly string configPath;
    private readonly string eventsPath;
    private readonly object gate = new();
    private readonly HashSet<string> handledThisSession = new(StringComparer.OrdinalIgnoreCase);
    private NameGuardConfig config;

    public HeadlessNameGuardService(IServerPathProfile paths, HeadlessActivityLogService activity, HeadlessNotificationService notifications,
        PlayerModerationCoordinator playerModeration, HeadlessPlayerRegistryService registry)
    {
        this.activity = activity;
        this.notifications = notifications;
        this.playerModeration = playerModeration;
        var root = Path.Combine(paths.ManagerRuntimeRoot, "players");
        Directory.CreateDirectory(root);
        configPath = Path.Combine(root, "name-guard.json");
        eventsPath = Path.Combine(root, "name-guard-events.json");
        config = Load(registry);
    }

    public NameGuardConfig GetConfig() { lock (gate) return config; }

    public NameGuardConfig SaveConfig(NameGuardConfig updated)
    {
        lock (gate)
        {
            config = updated with { Claims = NameGuard.Normalize(updated.Claims ?? [], DateTimeOffset.UtcNow) };
            Persist();
            handledThisSession.Clear();
            return config;
        }
    }

    public IReadOnlyList<NameGuardEvent> RecentEvents()
    {
        try { return File.Exists(eventsPath) ? JsonSerializer.Deserialize<List<NameGuardEvent>>(File.ReadAllText(eventsPath)) ?? [] : []; }
        catch { return []; }
    }

    public async Task EnforceAsync(HeadlessPlayersSnapshot players, CancellationToken cancellationToken)
    {
        if (!players.Available) return;
        NameGuardConfig snapshot;
        lock (gate) snapshot = config;
        if (!snapshot.Enabled)
        {
            lock (gate) handledThisSession.Clear();
            return;
        }

        var online = players.Players
            .Select(p => new NameGuardPlayer(NameGuard.AccountOf(p.UserId, p.PlayerId), (p.Name ?? string.Empty).Trim(), (p.PlayerId ?? string.Empty).Trim()))
            .Where(p => p.Account.Length > 0)
            .ToArray();
        var outcome = NameGuard.Judge(snapshot.Claims, online, DateTimeOffset.UtcNow);

        if (outcome.NewClaims.Count > 0)
        {
            lock (gate)
            {
                // The config may have been saved since the snapshot: add to the current one, never over a claim made meanwhile.
                config = config with { Claims = NameGuard.Normalize([.. config.Claims, .. outcome.NewClaims], DateTimeOffset.UtcNow) };
                Persist();
            }
        }

        foreach (var clash in outcome.Duplicates)
        {
            lock (gate) if (!handledThisSession.Add(clash.Player.Account)) continue;
            var kicked = false;
            var kickNote = string.Empty;
            if (snapshot.KickDuplicates)
            {
                var result = await playerModeration.ExecuteAsync("kick", clash.Player.Account,
                    $"MystTiq: the name \"{clash.Player.Name}\" is already taken on this server. Ask the administrator to remove your character, then make one with another name.", cancellationToken);
                kicked = result.Success;
                kickNote = result.Success ? " They were kicked." : $" The kick failed: {result.Message}.";
            }

            var owner = NameGuard.DescribeOwner(clash.Owner);
            var character = clash.Player.PlayerId.Length >= 8 ? $" Their character ({clash.Player.PlayerId[..8]}) already has that name; delete it on Players so they can make one with another name." : string.Empty;
            var message = $"{clash.Player.Account} joined as \"{clash.Player.Name}\", a name that belongs to {owner}.{kickNote}{character}";
            activity.Record("Warning", "Players", "Unique names", message);
            notifications.Create("Warning", "Player name already taken", message, pinned: false);
            Append(new NameGuardEvent(DateTimeOffset.UtcNow, clash.Player.Name, clash.Player.Account, clash.Player.PlayerId, clash.Owner.OwnerId, clash.Owner.OwnerName, kicked, message));
        }

        var onlineAccounts = new HashSet<string>(online.Select(p => p.Account), StringComparer.OrdinalIgnoreCase);
        lock (gate) handledThisSession.RemoveWhere(a => !onlineAccounts.Contains(a));
    }

    private void Append(NameGuardEvent entry)
    {
        lock (gate)
        {
            var list = RecentEvents().ToList();
            list.Add(entry);
            if (list.Count > MaximumEvents) list.RemoveRange(0, list.Count - MaximumEvents);
            try { File.WriteAllText(eventsPath, JsonSerializer.Serialize(list)); } catch { }
        }
    }

    private void Persist()
    {
        try { File.WriteAllText(configPath, JsonSerializer.Serialize(config)); } catch { }
    }

    // First use: the players already known own their names (the earliest first), so nobody new can take one before its
    // owner next joins. Names two known players share go to the earlier; the activity log says which.
    private NameGuardConfig Load(HeadlessPlayerRegistryService registry)
    {
        try
        {
            if (File.Exists(configPath))
            {
                var loaded = JsonSerializer.Deserialize<NameGuardConfig>(File.ReadAllText(configPath)) ?? NameGuardConfig.Default;
                return loaded with { Claims = NameGuard.Normalize(loaded.Claims ?? [], DateTimeOffset.UtcNow) };
            }
        }
        catch { return NameGuardConfig.Default; }

        var seed = NameGuard.Seed(registry.Snapshot().Select(r => new NameGuardKnownPlayer(NameGuard.AccountOf(r.UserId, r.PlayerId), r.LastKnownName ?? string.Empty, r.FirstSeenUtc)));
        config = NameGuardConfig.Default with { Claims = seed.Claims };
        Persist();
        foreach (var conflict in seed.Conflicts)
            activity.Record("Info", "Players", "Unique names",
                $"\"{conflict.Name}\" is used by {conflict.Owner} and {conflict.Other}. It belongs to {conflict.Owner}, seen first; {conflict.Other} is turned away under that name.");
        return config;
    }
}

public sealed record NameClaim(string Name, string OwnerId, string OwnerName, bool Reserved, DateTimeOffset ClaimedAt);

public sealed record NameGuardConfig(bool Enabled, bool KickDuplicates, IReadOnlyList<NameClaim> Claims)
{
    public static NameGuardConfig Default { get; } = new(true, true, []);
}

public sealed record NameGuardEvent(DateTimeOffset At, string Name, string UserId, string PlayerId, string OwnerId, string OwnerName, bool Kicked, string Message);

public sealed record NameGuardPlayer(string Account, string Name, string PlayerId);

public sealed record NameGuardKnownPlayer(string Account, string Name, DateTimeOffset FirstSeen);

public sealed record NameGuardClash(NameGuardPlayer Player, NameClaim Owner);

public sealed record NameGuardOutcome(IReadOnlyList<NameClaim> NewClaims, IReadOnlyList<NameGuardClash> Duplicates);

public sealed record NameGuardConflict(string Name, string Owner, string Other);

public sealed record NameGuardSeed(IReadOnlyList<NameClaim> Claims, IReadOnlyList<NameGuardConflict> Conflicts);

/// <summary>v1.0.0.2: the unique-names rules, pure so the logic harness covers them.</summary>
public static class NameGuard
{
    /// <summary>The form two names are compared in: case ignored, surrounding spaces dropped and inner runs of spaces as one.</summary>
    public static string Key(string? name) =>
        string.Join(' ', (name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    /// <summary>The account a player is known by: the user ID (steam_…), else the player ID. A bare SteamID64 gains steam_.</summary>
    public static string AccountOf(string? userId, string? playerId)
    {
        var id = (userId ?? string.Empty).Trim();
        if (id.Length == 0) id = (playerId ?? string.Empty).Trim();
        return id.Length == 17 && id.All(char.IsAsciiDigit) ? "steam_" + id : id;
    }

    public static string DescribeOwner(NameClaim claim) =>
        claim.OwnerId.Length == 0 ? "nobody (a blocked name)"
        : claim.OwnerName.Length > 0 && Key(claim.OwnerName) != Key(claim.OwnerId) ? $"{claim.OwnerName} ({claim.OwnerId})"
        : claim.OwnerId;

    /// <summary>
    /// Online players against the claims: a player using a name another account owns (or a blocked name) is a duplicate;
    /// an unclaimed name becomes the player's. Two players arriving together with one unclaimed name: the first listed gets it.
    /// </summary>
    public static NameGuardOutcome Judge(IReadOnlyList<NameClaim> claims, IEnumerable<NameGuardPlayer> online, DateTimeOffset now)
    {
        var byKey = new Dictionary<string, NameClaim>(StringComparer.Ordinal);
        foreach (var claim in claims) byKey.TryAdd(Key(claim.Name), claim);
        var added = new List<NameClaim>();
        var duplicates = new List<NameGuardClash>();
        foreach (var player in online)
        {
            var key = Key(player.Name);
            if (key.Length == 0 || player.Account.Length == 0) continue;  // no name yet (the new-character screen)
            if (byKey.TryGetValue(key, out var owner))
            {
                // A claim made from a record without a user ID names the character instead; that is the same player.
                var own = string.Equals(owner.OwnerId, player.Account, StringComparison.OrdinalIgnoreCase) ||
                          (player.PlayerId.Length > 0 && string.Equals(owner.OwnerId, player.PlayerId, StringComparison.OrdinalIgnoreCase));
                if (!own) duplicates.Add(new NameGuardClash(player, owner));
                continue;
            }
            var claim = new NameClaim(player.Name, player.Account, player.Name, false, now);
            byKey[key] = claim;
            added.Add(claim);
        }
        return new NameGuardOutcome(added, duplicates);
    }

    /// <summary>The names the known players already use, each to the one seen first; a name shared by two is reported.</summary>
    public static NameGuardSeed Seed(IEnumerable<NameGuardKnownPlayer> known)
    {
        var byKey = new Dictionary<string, NameClaim>(StringComparer.Ordinal);
        var conflicts = new List<NameGuardConflict>();
        foreach (var player in known.Where(p => p.Account.Length > 0).OrderBy(p => p.FirstSeen))
        {
            var name = player.Name.Trim();
            var key = Key(name);
            if (key.Length == 0) continue;
            if (byKey.TryGetValue(key, out var owner))
            {
                if (!string.Equals(owner.OwnerId, player.Account, StringComparison.OrdinalIgnoreCase))
                    conflicts.Add(new NameGuardConflict(owner.Name, owner.OwnerId, player.Account));
                continue;
            }
            byKey[key] = new NameClaim(name, player.Account, name, false, player.FirstSeen);
        }
        return new NameGuardSeed([.. byKey.Values], conflicts);
    }

    /// <summary>A saved list made consistent: names trimmed, empty ones dropped, one claim per name (the first), owner IDs as accounts.</summary>
    public static IReadOnlyList<NameClaim> Normalize(IEnumerable<NameClaim> claims, DateTimeOffset now)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<NameClaim>();
        foreach (var claim in claims)
        {
            if (claim is null) continue;
            var name = string.Join(' ', (claim.Name ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (name.Length == 0 || !seen.Add(Key(name))) continue;
            var owner = AccountOf(claim.OwnerId, null);
            result.Add(new NameClaim(name, owner, (claim.OwnerName ?? string.Empty).Trim(), claim.Reserved || owner.Length == 0,
                claim.ClaimedAt == default ? now : claim.ClaimedAt));
        }
        return result;
    }
}
