using System.Text.Json;
using System.Text.RegularExpressions;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.7.94.0 "Starter Kits" -- a named loadout of items and Pals an admin can give to a player by hand,
// or have MystTiq give automatically to players it first sees after auto-gift is switched on.
//
// Delivery is the honest constraint: vanilla Palworld has no give command at all (which is why the
// Players page's Give Item was disabled until v0.7.112.0 wired it to this same provider through
// GiveEntriesAsync). PalDefender, a server mod, adds `giveitems` and
// `givepal` (documented at ultimeit.github.io/PalDefender/Commands, runnable over RCON). So this only
// delivers when PalDefender is installed and RCON is usable, says so plainly when it is not, and never
// pretends. The delivery mechanism sits behind IKitCommandRunner so a future provider (the v0.8 give-
// item work) can slot in without touching kit storage, claim tracking or auto-gift.
//
// Enforcement runs from the same /status/poll cadence as the player registry and whitelist. The
// registry's Observe runs first in that poll, so a brand-new player's FirstSeenUtc is "now".
public sealed record KitEntry(string Type, string Id, int Amount);

public sealed record KitDefinition(string Id, string Name, IReadOnlyList<KitEntry> Entries);

public sealed record KitConfig(
    bool AutoGiftEnabled,
    string AutoGiftKitId,
    DateTimeOffset? AutoGiftEnabledAtUtc,
    IReadOnlyList<KitDefinition> Kits)
{
    public static KitConfig Default => new(false, string.Empty, null, []);
}

public sealed record KitClaim(string PlayerId, string PlayerName, string KitId, DateTimeOffset ClaimedAtUtc, string Trigger);

public sealed record KitProviderStatus(bool CanDeliver, string Provider, string Detail);

public sealed record KitSnapshot(KitConfig Config, KitProviderStatus Provider, IReadOnlyList<KitClaim> Claims);

public sealed record KitSaveResult(bool Success, string Message, KitConfig Config, IReadOnlyList<string> Errors);

public sealed record KitGiveResult(bool Success, string Message, IReadOnlyList<string> Commands, string Response);

public sealed record KitCommandResult(bool Success, string Response, string Message);

public interface IKitCommandRunner
{
    KitProviderStatus GetStatus();
    Task<KitCommandResult> RunAsync(string command, CancellationToken cancellationToken);
}

// Production runner: PalDefender's give commands over the server's own RCON connection.
public sealed class RconKitCommandRunner : IKitCommandRunner
{
    private readonly IServerPathProfile paths;
    private readonly PalworldRconService rcon;

    public RconKitCommandRunner(IServerPathProfile paths, PalworldRconService rcon)
    {
        this.paths = paths;
        this.rcon = rcon;
    }

    public KitProviderStatus GetStatus()
    {
        var installed = Directory.Exists(Path.Combine(paths.RuntimeBinaryRoot, "PalDefender"));
        var status = rcon.GetStatus();
        if (!installed)
            return new(false, "PalDefender (RCON)", "PalDefender was not found in this server's binaries folder. Vanilla Palworld has no give-item command, so kits need PalDefender to deliver.");
        if (!status.Enabled || !status.PasswordConfigured)
            return new(false, "PalDefender (RCON)", "PalDefender is installed, but RCON is not enabled with an AdminPassword in PalWorldSettings.ini, which kit delivery uses.");
        return new(true, "PalDefender (RCON)", "PalDefender is installed and RCON is enabled. Use Test to confirm it answers while the server is running.");
    }

    public async Task<KitCommandResult> RunAsync(string command, CancellationToken cancellationToken)
    {
        var result = await rcon.ExecuteAsync(command, cancellationToken);
        return new(result.Success, result.Response ?? string.Empty, result.Message);
    }
}

public sealed partial class HeadlessKitService
{
    public const int MaximumKits = 50;
    public const int MaximumEntriesPerKit = 100;
    public const int MaximumDeliveryAttempts = 3;
    private const int ItemsPerCommand = 20;
    private const int MaximumClaims = 5000;
    public const int MaximumGivenHistory = 200;

    [GeneratedRegex("^[A-Za-z0-9_]{1,64}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex("^[A-Za-z0-9_\\-]{3,64}$")]
    private static partial Regex UserIdPattern();

    [GeneratedRegex("\\b(unknown|invalid|not found|no such|failed|failure|error|cannot|can't|denied|not allowed)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex FailurePattern();

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string configPath;
    private readonly string claimsPath;
    // v0.8.3.0: ids a manual Give delivered without an error reply, newest first, for the item picker.
    private readonly string givenPath;
    private readonly HeadlessActivityLogService activity;
    private readonly HeadlessPlayerRegistryService registry;
    private readonly IKitCommandRunner runner;
    private readonly object gate = new();
    private readonly SemaphoreSlim enforceGate = new(1, 1);
    private readonly Dictionary<string, int> attempts = new(StringComparer.OrdinalIgnoreCase);
    private KitConfig config;
    private List<KitClaim> claims;
    private List<KitEntry> given;

    public HeadlessKitService(IServerPathProfile paths, HeadlessActivityLogService activity, HeadlessPlayerRegistryService registry, IKitCommandRunner runner)
    {
        this.activity = activity;
        this.registry = registry;
        this.runner = runner;
        var root = Path.Combine(paths.ManagerRuntimeRoot, "players");
        Directory.CreateDirectory(root);
        configPath = Path.Combine(root, "kits.json");
        claimsPath = Path.Combine(root, "kit-claims.json");
        givenPath = Path.Combine(root, "give-history.json");
        config = LoadConfig();
        claims = LoadClaims();
        given = LoadList<KitEntry>(givenPath);
    }

    public KitSnapshot GetSnapshot()
    {
        lock (gate)
            return new(config, runner.GetStatus(), claims.OrderByDescending(c => c.ClaimedAtUtc).Take(200).ToArray());
    }

    // ---- Pure logic (unit-tested in the logic harness) --------------------------------------------

    public static IReadOnlyList<string> Validate(KitConfig candidate)
    {
        var errors = new List<string>();
        if (candidate.Kits.Count > MaximumKits) errors.Add($"At most {MaximumKits} kits are allowed.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kit in candidate.Kits)
        {
            var label = string.IsNullOrWhiteSpace(kit.Name) ? "(unnamed kit)" : kit.Name;
            if (string.IsNullOrWhiteSpace(kit.Name) || kit.Name.Trim().Length > 64) errors.Add($"Kit '{label}': give it a name of 1 to 64 characters.");
            if (!ids.Add(kit.Id)) errors.Add($"Kit '{label}': duplicate kit id.");
            if (kit.Entries.Count == 0) errors.Add($"Kit '{label}': add at least one item or Pal.");
            if (kit.Entries.Count > MaximumEntriesPerKit) errors.Add($"Kit '{label}': at most {MaximumEntriesPerKit} entries.");
            foreach (var entry in kit.Entries)
            {
                var isItem = entry.Type.Equals("Item", StringComparison.OrdinalIgnoreCase);
                var isPal = entry.Type.Equals("Pal", StringComparison.OrdinalIgnoreCase);
                if (!isItem && !isPal) { errors.Add($"Kit '{label}': entry type must be Item or Pal, not '{entry.Type}'."); continue; }
                if (!IdPattern().IsMatch(entry.Id ?? string.Empty)) errors.Add($"Kit '{label}': '{entry.Id}' is not a valid id (letters, digits and underscore only).");
                if (isItem && entry.Amount is < 1 or > 1_000_000) errors.Add($"Kit '{label}': item '{entry.Id}' amount must be 1 to 1,000,000.");
                if (isPal && entry.Amount is < 1 or > 100) errors.Add($"Kit '{label}': Pal '{entry.Id}' level must be 1 to 100.");
            }
        }

        if (candidate.AutoGiftEnabled && !candidate.Kits.Any(k => k.Id.Equals(candidate.AutoGiftKitId, StringComparison.OrdinalIgnoreCase)))
            errors.Add("Auto-gift is on but no existing kit is chosen for it.");
        return errors;
    }

    // items are grouped into `giveitems <UserId> Id:Amount ...` commands; each Pal is one `givepal` command.
    public static IReadOnlyList<string> BuildCommands(KitDefinition kit, string userId)
    {
        if (!UserIdPattern().IsMatch(userId ?? string.Empty)) return [];
        var commands = new List<string>();
        var items = kit.Entries.Where(e => e.Type.Equals("Item", StringComparison.OrdinalIgnoreCase)).ToList();
        for (var offset = 0; offset < items.Count; offset += ItemsPerCommand)
            commands.Add($"giveitems {userId} " + string.Join(' ', items.Skip(offset).Take(ItemsPerCommand).Select(e => $"{e.Id}:{e.Amount}")));
        foreach (var pal in kit.Entries.Where(e => e.Type.Equals("Pal", StringComparison.OrdinalIgnoreCase)))
            commands.Add($"givepal {userId} {pal.Id} {pal.Amount}");
        return commands;
    }

    // The server's reply text for these commands is not formally specified, so a reply that reads as
    // an error is treated as a failed delivery (and not marked claimed) rather than trusted blindly.
    public static bool LooksLikeFailure(string? response) => !string.IsNullOrWhiteSpace(response) && FailurePattern().IsMatch(response);

    // ---- Config -----------------------------------------------------------------------------------

    public KitSaveResult SaveConfig(KitConfig updated)
    {
        var normalized = updated with
        {
            Kits = updated.Kits.Select(k => k with
            {
                Id = string.IsNullOrWhiteSpace(k.Id) ? NewKitId() : k.Id.Trim(),
                Name = (k.Name ?? string.Empty).Trim(),
                Entries = k.Entries.Select(e => e with { Type = e.Type.Trim(), Id = (e.Id ?? string.Empty).Trim() }).ToArray()
            }).ToArray(),
            AutoGiftKitId = (updated.AutoGiftKitId ?? string.Empty).Trim()
        };

        var errors = Validate(normalized);
        if (errors.Count > 0)
            return new(false, errors[0], GetSnapshot().Config, errors);

        lock (gate)
        {
            // The moment auto-gift is switched on (or its kit changes) becomes the cutoff: only players the
            // registry first sees at or after it are gifted, so turning this on never mass-gifts everyone
            // who already plays here.
            var wasOn = config.AutoGiftEnabled && config.AutoGiftKitId.Equals(normalized.AutoGiftKitId, StringComparison.OrdinalIgnoreCase);
            var enabledAt = !normalized.AutoGiftEnabled ? (DateTimeOffset?)null : wasOn ? config.AutoGiftEnabledAtUtc ?? DateTimeOffset.UtcNow : DateTimeOffset.UtcNow;
            config = normalized with { AutoGiftEnabledAtUtc = enabledAt };
            attempts.Clear();
            Persist(configPath, config);
            return new(true, "Kits saved.", config, []);
        }
    }

    private static string NewKitId() => "kit-" + Guid.NewGuid().ToString("N")[..8];

    // ---- Delivery ---------------------------------------------------------------------------------

    public async Task<KitCommandResult> TestProviderAsync(CancellationToken cancellationToken)
    {
        var status = runner.GetStatus();
        if (!status.CanDeliver) return new(false, string.Empty, status.Detail);
        return await runner.RunAsync("version", cancellationToken);
    }

    public async Task<KitGiveResult> GiveAsync(string kitId, string playerId, HeadlessPlayersSnapshot players, string trigger, CancellationToken cancellationToken)
    {
        KitDefinition? kit;
        lock (gate) kit = config.Kits.FirstOrDefault(k => k.Id.Equals(kitId, StringComparison.OrdinalIgnoreCase));
        if (kit is null) return new(false, "That kit no longer exists.", [], string.Empty);

        var status = runner.GetStatus();
        if (!status.CanDeliver) return new(false, status.Detail, [], string.Empty);

        var player = players.Available
            ? players.Players.FirstOrDefault(p => string.Equals((p.PlayerId ?? string.Empty).Trim(), playerId.Trim(), StringComparison.OrdinalIgnoreCase))
            : null;
        if (player is null) return new(false, "That player is not online, and kits can only be delivered to an online player.", [], string.Empty);

        var delivered = await DeliverAsync($"Kit '{kit.Name}'", kit, player, cancellationToken);
        if (!delivered.Success) return delivered;

        RecordClaim(new KitClaim(NormalizeId(player.PlayerId), player.Name, kit.Id, DateTimeOffset.UtcNow, trigger));
        Log("Information", $"Kit '{kit.Name}' given", $"{player.Name} received {kit.Entries.Count} entr{(kit.Entries.Count == 1 ? "y" : "ies")} ({trigger}).");
        return delivered with { Message = $"Gave '{kit.Name}' to {player.Name}." };
    }

    // v0.7.112.0 "Give Item": the Players page's own give, for items and Pals typed in by hand (the same
    // "item PalSphere 10" / "pal WeaselDragon 5" lines the kit editor uses). Same provider, same checks and
    // same honest failure handling as a kit, but it is a one-off: no kit is saved and no claim is recorded,
    // so it never affects auto-gift.
    public async Task<KitGiveResult> GiveEntriesAsync(IReadOnlyList<KitEntry> entries, string playerId, HeadlessPlayersSnapshot players, CancellationToken cancellationToken)
    {
        var adHoc = new KitDefinition("give-item", "Give Item", entries
            .Select(e => e with { Type = (e.Type ?? string.Empty).Trim(), Id = (e.Id ?? string.Empty).Trim() }).ToArray());
        var errors = Validate(new KitConfig(false, string.Empty, null, [adHoc]));
        if (errors.Count > 0) return new(false, errors[0].Replace("Kit 'Give Item': ", string.Empty), [], string.Empty);

        var status = runner.GetStatus();
        if (!status.CanDeliver) return new(false, status.Detail, [], string.Empty);

        var player = players.Available
            ? players.Players.FirstOrDefault(p => string.Equals((p.PlayerId ?? string.Empty).Trim(), (playerId ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
            : null;
        if (player is null) return new(false, "That player is not online, and items can only be given to an online player.", [], string.Empty);

        var delivered = await DeliverAsync("Give Item", adHoc, player, cancellationToken);
        if (!delivered.Success) return delivered;

        RecordGiven(adHoc.Entries);
        var summary = KitEntryText.Format(adHoc.Entries.Select(e => new KitTextEntry(e.Type, e.Id, e.Amount))).Replace('\n', ',');
        Log("Information", "Items given", $"{player.Name} received: {summary}.");
        return delivered with { Message = $"Gave {adHoc.Entries.Count} entr{(adHoc.Entries.Count == 1 ? "y" : "ies")} to {player.Name}." };
    }

    // Runs every give command for one player, stopping at the first failure or error-looking reply.
    private async Task<KitGiveResult> DeliverAsync(string label, KitDefinition kit, HeadlessPlayerSnapshot player, CancellationToken cancellationToken)
    {
        var commands = BuildCommands(kit, player.UserId);
        if (commands.Count == 0)
            return new(false, $"{player.Name} has no usable UserId (expected something like steam_7656…), so the give command cannot address them.", [], string.Empty);

        var sent = new List<string>();
        var responses = new List<string>();
        foreach (var command in commands)
        {
            var result = await runner.RunAsync(command, cancellationToken);
            sent.Add(command);
            responses.Add(result.Response);
            if (!result.Success)
            {
                Log("Warning", $"{label} delivery failed", $"{player.Name}: {result.Message}");
                return new(false, $"Delivery stopped: {result.Message}", sent, string.Join(" | ", responses.Where(r => r.Length > 0)));
            }

            if (LooksLikeFailure(result.Response))
            {
                Log("Warning", $"{label} delivery reported an error", $"{player.Name}: {result.Response}");
                return new(false, $"The server replied with what looks like an error: {result.Response}", sent, string.Join(" | ", responses.Where(r => r.Length > 0)));
            }
        }

        return new(true, string.Empty, sent, string.Join(" | ", responses.Where(r => r.Length > 0)));
    }

    // Auto-gift: only for a player the registry first saw at or after the moment auto-gift was enabled,
    // who has not already been given this kit, capped at MaximumDeliveryAttempts so a broken provider
    // can never turn into a give attempt every few seconds.
    public async Task EnforceAsync(HeadlessPlayersSnapshot players, CancellationToken cancellationToken)
    {
        if (!players.Available) return;
        KitConfig snapshot;
        lock (gate) snapshot = config;
        if (!snapshot.AutoGiftEnabled || snapshot.AutoGiftEnabledAtUtc is not { } enabledAt) return;
        var kit = snapshot.Kits.FirstOrDefault(k => k.Id.Equals(snapshot.AutoGiftKitId, StringComparison.OrdinalIgnoreCase));
        if (kit is null || !runner.GetStatus().CanDeliver) return;
        if (!await enforceGate.WaitAsync(0, cancellationToken)) return;
        try
        {
            var registryById = registry.Snapshot().ToDictionary(r => NormalizeId(r.PlayerId), r => r, StringComparer.OrdinalIgnoreCase);
            foreach (var player in players.Players)
            {
                var id = NormalizeId(player.PlayerId);
                if (id.Length == 0) continue;
                if (!registryById.TryGetValue(id, out var record) || record.FirstSeenUtc < enabledAt) continue;
                bool alreadyClaimed;
                int tried;
                lock (gate)
                {
                    alreadyClaimed = claims.Any(c => c.PlayerId.Equals(id, StringComparison.OrdinalIgnoreCase) && c.KitId.Equals(kit.Id, StringComparison.OrdinalIgnoreCase));
                    attempts.TryGetValue(id, out tried);
                }

                if (alreadyClaimed || tried >= MaximumDeliveryAttempts) continue;
                lock (gate) attempts[id] = tried + 1;
                var result = await GiveAsync(kit.Id, id, players, "auto", cancellationToken);
                if (!result.Success && tried + 1 >= MaximumDeliveryAttempts)
                    Log("Error", $"Kit '{kit.Name}' auto-gift gave up", $"{player.Name}: {result.Message} (stopped after {MaximumDeliveryAttempts} attempts; use Give Kit to retry by hand).");
            }
        }
        finally { enforceGate.Release(); }
    }

    public bool ForgetClaims(string playerId)
    {
        var id = NormalizeId(playerId);
        lock (gate)
        {
            var removed = claims.RemoveAll(c => c.PlayerId.Equals(id, StringComparison.OrdinalIgnoreCase)) > 0;
            attempts.Remove(id);
            if (removed) Persist(claimsPath, claims);
            return removed;
        }
    }

    // ---- Persistence ------------------------------------------------------------------------------

    private void RecordClaim(KitClaim claim)
    {
        lock (gate)
        {
            claims.Add(claim);
            if (claims.Count > MaximumClaims) claims = claims.Skip(claims.Count - MaximumClaims).ToList();
            Persist(claimsPath, claims);
        }
    }

    public IReadOnlyList<KitEntry> RecentlyGiven()
    {
        lock (gate) return given.ToArray();
    }

    // Newest first, one row per (type, id) with the last amount used, capped.
    public static List<KitEntry> MergeGiven(IReadOnlyList<KitEntry> history, IEnumerable<KitEntry> delivered)
    {
        var result = history.ToList();
        foreach (var entry in delivered)
        {
            result.RemoveAll(e => e.Type.Equals(entry.Type, StringComparison.OrdinalIgnoreCase) && e.Id.Equals(entry.Id, StringComparison.OrdinalIgnoreCase));
            result.Insert(0, entry);
        }
        return result.Take(MaximumGivenHistory).ToList();
    }

    private void RecordGiven(IEnumerable<KitEntry> delivered)
    {
        lock (gate)
        {
            given = MergeGiven(given, delivered);
            try { Persist(givenPath, given); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* the give itself succeeded */ }
        }
    }

    private void Log(string severity, string action, string detail) => activity.Record(severity, "Players", action, detail);
    private static string NormalizeId(string? value) => (value ?? string.Empty).Trim();

    private KitConfig LoadConfig()
    {
        try { return File.Exists(configPath) ? JsonSerializer.Deserialize<KitConfig>(File.ReadAllText(configPath)) ?? KitConfig.Default : KitConfig.Default; }
        catch { return KitConfig.Default; }
    }

    private List<KitClaim> LoadClaims()
    {
        try { return File.Exists(claimsPath) ? JsonSerializer.Deserialize<List<KitClaim>>(File.ReadAllText(claimsPath)) ?? [] : []; }
        catch { return []; }
    }

    private static List<T> LoadList<T>(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path)) ?? [] : []; }
        catch { return []; }
    }

    private static void Persist<T>(string path, T value)
    {
        var partial = path + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(value, JsonOptions));
        File.Move(partial, path, true);
    }
}
