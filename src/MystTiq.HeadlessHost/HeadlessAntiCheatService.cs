using System.Text.Json;
using System.Text.RegularExpressions;
using MystTiq.Core.Models;
using MystTiq.Core.Providers;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// "Anti-Cheat & Save-Integrity Scanning": mirrors HeadlessAlertCenterService's exact
// shape (rule records + persisted rule-set JSON + a Fire-equivalent with per-key cooldown) but
// for player/Pal integrity signals rather than system-resource metrics -- a different data
// domain, so kept as its own service rather than folded into AlertCenter.
//
// Two evaluation paths matched to cost: EvaluateLivePlayersAsync runs on every
// HeadlessAutomationService tick (15s) using data already being polled (HeadlessMonitoringService
// -- no new polling infrastructure); EvaluateThrottledAsync self-throttles to 10 minutes since it
// does a full Level.sav decode via HeadlessPalEditService.ListPalsAsync, reused exactly as-is and
// strictly read-only -- this service never mutates a Pal or a save file.
public sealed class HeadlessAntiCheatService
{
    private static readonly TimeSpan PalScanInterval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(30);
    private const int MaximumFindings = 200;

    // SteamID64: always exactly 17 digits, always starting "7656119" (minimum valid value
    // 76561197960265729). A genuinely citable structural fact, not a guessed threshold.
    private static readonly Regex SteamId64Pattern = new(@"^7656119\d{10}$", RegexOptions.Compiled);

    private readonly HeadlessNotificationService notifications;
    private readonly HeadlessActivityLogService activity;
    private readonly PlayerModerationCoordinator playerModeration;
    private readonly HeadlessPalEditService palEdit;
    private readonly object gate = new();
    private readonly string rulesPath;
    private readonly string findingsPath;
    private readonly List<AntiCheatFinding> findings = [];
    private readonly Dictionary<string, DateTimeOffset> lastFiredUtc = new(StringComparer.Ordinal);
    private AntiCheatRuleSet rules;
    private DateTimeOffset lastPalScanUtc = DateTimeOffset.MinValue;

    public HeadlessAntiCheatService(
        IServerPathProfile paths,
        HeadlessNotificationService notifications,
        HeadlessActivityLogService activity,
        PlayerModerationCoordinator playerModeration,
        HeadlessPalEditService palEdit)
    {
        this.notifications = notifications;
        this.activity = activity;
        this.playerModeration = playerModeration;
        this.palEdit = palEdit;

        var root = Path.Combine(paths.ManagerRuntimeRoot, "anticheat");
        Directory.CreateDirectory(root);
        rulesPath = Path.Combine(root, "rules.json");
        findingsPath = Path.Combine(root, "findings.json");
        rules = LoadRules();
        findings = LoadFindings();
    }

    public AntiCheatRuleSet GetRules() { lock (gate) return rules; }

    public AntiCheatRuleSet SaveRules(AntiCheatRuleSet updated)
    {
        lock (gate)
        {
            rules = updated;
            Persist(rulesPath, rules);
            return rules;
        }
    }

    public IReadOnlyList<AntiCheatFinding> RecentFindings(int maximum = 100)
    {
        lock (gate)
            return findings.AsEnumerable().Reverse().Take(Math.Clamp(maximum, 1, MaximumFindings)).ToArray();
    }

    public async Task EvaluateLivePlayersAsync(HeadlessPlayersSnapshot players, CancellationToken cancellationToken)
    {
        if (!players.Available) return;
        var set = GetRules();

        foreach (var player in players.Players)
        {
            if (set.InvalidSteamId.Enabled && !string.IsNullOrWhiteSpace(player.SteamId) && !SteamId64Pattern.IsMatch(player.SteamId))
            {
                await RecordAsync("invalid-steam-id", set.InvalidSteamId.Response, "Warning", player.PlayerId, player.Name,
                    $"Player '{player.Name}' has a malformed Steam ID ('{player.SteamId}') -- not a valid 17-digit SteamID64.", cancellationToken);
            }

            if (set.ImpossibleLevel.Enabled && int.TryParse(player.Level, out var level) && level > set.ImpossibleLevel.MaxAllowedLevel)
            {
                await RecordAsync("impossible-player-level", set.ImpossibleLevel.Response, "Warning", player.PlayerId, player.Name,
                    $"Player '{player.Name}' reports level {level}, above the configured maximum of {set.ImpossibleLevel.MaxAllowedLevel}.", cancellationToken);
            }
        }
    }

    public async Task EvaluateThrottledAsync(CancellationToken cancellationToken)
    {
        var set = GetRules();
        if (!set.PalStatAnomaly.Enabled) return;

        var now = DateTimeOffset.UtcNow;
        lock (gate)
        {
            if (now - lastPalScanUtc < PalScanInterval) return;
            lastPalScanUtc = now;
        }

        IReadOnlyList<PalInstanceSnapshot> pals;
        try { pals = await palEdit.ListPalsAsync(null, cancellationToken); }
        catch (Exception ex)
        {
            activity.Record("Warning", "Anti-Cheat", "Pal stat scan failed", ex.Message);
            return;
        }

        foreach (var pal in pals)
        {
            var reasons = new List<string>();
            if (pal.Level > set.ImpossibleLevel.MaxAllowedLevel) reasons.Add($"Level {pal.Level} (max {set.ImpossibleLevel.MaxAllowedLevel})");
            if (pal.Rank is < 0 or > 5) reasons.Add($"Rank {pal.Rank} (valid range 0-5)");
            if (pal.TalentHp is < 0 or > 100) reasons.Add($"Talent_HP {pal.TalentHp} (valid range 0-100)");
            if (pal.TalentShot is < 0 or > 100) reasons.Add($"Talent_Shot {pal.TalentShot} (valid range 0-100)");
            if (pal.TalentDefense is < 0 or > 100) reasons.Add($"Talent_Defense {pal.TalentDefense} (valid range 0-100)");
            if (reasons.Count == 0) continue;

            var ownerId = pal.OwnerPlayerId;
            var ownerName = pal.OwnerPlayerName ?? "(unowned)";
            var detail = $"Pal '{pal.NickName}' ({pal.CharacterId}, owned by {ownerName}) has out-of-bounds stats: {string.Join("; ", reasons)}.";
            // A Pal with no resolved owner can only ever be flagged -- there is no player to
            // kick/ban against.
            var response = string.IsNullOrWhiteSpace(ownerId) ? AntiCheatResponse.Flag : set.PalStatAnomaly.Response;
            // Cooldown identity is the Pal's own InstanceId, not the owner's playerId -- a single
            // player owning two simultaneously-anomalous Pals (or two different unowned Pals, which
            // would otherwise collide on the shared "(unowned)" label) must not have the second
            // Pal's finding silently swallowed by the first Pal's cooldown window. Enforcement
            // (Kick/Ban) still targets the real ownerId, unaffected by this.
            await RecordAsync("pal-stat-anomaly", response, "Warning", ownerId, ownerName, detail, cancellationToken, cooldownIdentity: pal.InstanceId);
        }
    }

    private async Task RecordAsync(string ruleKey, AntiCheatResponse response, string severity, string? playerId, string? playerName, string detail, CancellationToken cancellationToken, string? cooldownIdentity = null)
    {
        var cooldownKey = $"{ruleKey}:{cooldownIdentity ?? playerId ?? playerName ?? "unknown"}";
        lock (gate)
        {
            if (lastFiredUtc.TryGetValue(cooldownKey, out var last) && DateTimeOffset.UtcNow - last < DefaultCooldown) return;
            lastFiredUtc[cooldownKey] = DateTimeOffset.UtcNow;

            findings.Add(new AntiCheatFinding(ruleKey, severity, playerId, playerName, detail, DateTimeOffset.UtcNow, response));
            if (findings.Count > MaximumFindings) findings.RemoveRange(0, findings.Count - MaximumFindings);
            Persist(findingsPath, findings);
        }

        notifications.Create(severity, "Anti-cheat finding", detail, pinned: severity == "Critical");
        activity.Record(severity, "Anti-Cheat", $"Finding: {ruleKey}", detail);

        if (response == AntiCheatResponse.Flag || string.IsNullOrWhiteSpace(playerId)) return;

        var action = response == AntiCheatResponse.Kick ? "kick" : "ban";
        var result = await playerModeration.ExecuteAsync(action, playerId, $"Anti-cheat: {detail}", cancellationToken);
        activity.Record(result.Success ? "Information" : "Warning", "Anti-Cheat",
            $"{(result.Success ? "Enforced" : "Failed to enforce")} {action} for rule {ruleKey}", result.Message);
    }

    private AntiCheatRuleSet LoadRules()
    {
        try { return File.Exists(rulesPath) ? JsonSerializer.Deserialize<AntiCheatRuleSet>(File.ReadAllText(rulesPath)) ?? new() : new(); }
        catch { return new(); }
    }

    private List<AntiCheatFinding> LoadFindings()
    {
        try { return File.Exists(findingsPath) ? JsonSerializer.Deserialize<List<AntiCheatFinding>>(File.ReadAllText(findingsPath)) ?? [] : []; }
        catch { return []; }
    }

    private static void Persist<T>(string path, T value)
    {
        var partial = path + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(partial, path, true);
    }
}
