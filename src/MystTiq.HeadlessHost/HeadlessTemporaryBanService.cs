using System.Text.Json;
using MystTiq.Core.Models;
using MystTiq.Core.Providers;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.7.15.0 "Temporary Ban" -- one of two backend-required stub buttons left on the Players page
// since before this session (the other, Whitelist, shipped in v0.7.10.0 and its own stub button
// removed from this same card as part of this release). Bans immediately via the existing
// ban RCON/REST path (PlayerModerationCoordinator, unchanged), then persists an expiry timestamp
// and unbans automatically once it elapses. Enforcement runs from the same /status/poll cadence
// HeadlessWhitelistService already uses -- observation only happens while something is actively
// polling, the same accepted limitation already documented there.
public sealed class HeadlessTemporaryBanService
{
    private readonly string configPath;
    private readonly HeadlessActivityLogService activity;
    private readonly PlayerModerationCoordinator playerModeration;
    private readonly object gate = new();
    private TemporaryBanConfig config;

    public HeadlessTemporaryBanService(IServerPathProfile paths, HeadlessActivityLogService activity, PlayerModerationCoordinator playerModeration)
    {
        this.activity = activity;
        this.playerModeration = playerModeration;
        var root = Path.Combine(paths.ManagerRuntimeRoot, "players");
        Directory.CreateDirectory(root);
        configPath = Path.Combine(root, "temporary-bans.json");
        config = Load();
    }

    public TemporaryBanConfig GetConfig()
    {
        lock (gate) return config;
    }

    public async Task<PlayerModerationResult> BanAsync(string playerId, string playerName, string reason, TimeSpan duration, CancellationToken cancellationToken)
    {
        playerId = (playerId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(playerId))
            return new PlayerModerationResult(false, false, "none", "ban", playerId, "A player UserID/SteamID is required.");
        if (duration <= TimeSpan.Zero)
            return new PlayerModerationResult(false, false, "none", "ban", playerId, "Duration must be greater than zero.");

        var result = await playerModeration.ExecuteAsync("ban", playerId, reason, cancellationToken);
        if (!result.Success) return result;

        var expiresAt = DateTimeOffset.UtcNow + duration;
        lock (gate)
        {
            var entries = config.Entries.Where(e => !e.PlayerId.Equals(playerId, StringComparison.OrdinalIgnoreCase)).ToList();
            entries.Add(new TemporaryBanEntry(playerId, playerName ?? string.Empty, reason ?? string.Empty, expiresAt));
            config = config with { Entries = entries };
            Persist();
        }

        activity.Record("Warning", "Players", "Temporary ban applied", $"{playerName} ({playerId}) banned until {expiresAt:u}: {reason}");
        return result;
    }

    // Manual early lift (the existing Unban button/action) -- clears the tracked expiry so the
    // sweep below doesn't attempt a second, redundant unban once its own timer elapses.
    public void ForgetIfPresent(string playerId)
    {
        playerId = (playerId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(playerId)) return;
        lock (gate)
        {
            if (!config.Entries.Any(e => e.PlayerId.Equals(playerId, StringComparison.OrdinalIgnoreCase))) return;
            config = config with { Entries = config.Entries.Where(e => !e.PlayerId.Equals(playerId, StringComparison.OrdinalIgnoreCase)).ToList() };
            Persist();
        }
    }

    public async Task EnforceAsync(CancellationToken cancellationToken)
    {
        List<TemporaryBanEntry> expired;
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            expired = config.Entries.Where(e => e.ExpiresAtUtc <= now).ToList();
            if (expired.Count == 0) return;
        }

        foreach (var entry in expired)
        {
            // Removed after this single attempt regardless of outcome -- a failing RCON/REST
            // connection would otherwise retry every poll tick forever; a failure is still fully
            // visible in the activity log, and the existing manual Unban button remains available.
            lock (gate) config = config with { Entries = config.Entries.Where(e => !e.PlayerId.Equals(entry.PlayerId, StringComparison.OrdinalIgnoreCase)).ToList() };

            var result = await playerModeration.ExecuteAsync("unban", entry.PlayerId, "Temporary ban expired.", cancellationToken);
            activity.Record(result.Success ? "Success" : "Error", "Players", "Temporary ban expired",
                result.Success
                    ? $"{entry.PlayerName} ({entry.PlayerId}) was automatically unbanned: temporary ban expired."
                    : $"{entry.PlayerName} ({entry.PlayerId})'s temporary ban expired, but the automatic unban failed: {result.Message}");
        }

        lock (gate) Persist();
    }

    private TemporaryBanConfig Load()
    {
        try { return File.Exists(configPath) ? JsonSerializer.Deserialize<TemporaryBanConfig>(File.ReadAllText(configPath)) ?? TemporaryBanConfig.Default : TemporaryBanConfig.Default; }
        catch { return TemporaryBanConfig.Default; }
    }

    private void Persist()
    {
        var partial = configPath + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(partial, configPath, true);
    }
}
