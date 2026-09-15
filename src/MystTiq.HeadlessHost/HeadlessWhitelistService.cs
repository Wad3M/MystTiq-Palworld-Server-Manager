using System.Text.Json;
using MystTiq.Core.Models;
using MystTiq.Core.Providers;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.7.10.0 "Whitelist" -- the last verified competitive feature gap from this session's survey,
// and named as an explicit deferred roadmap item in ProviderModels.cs's own comment before this.
// Opt-in per-profile: when Enabled, any online player whose PlayerId isn't listed is kicked
// automatically. Enforcement runs from the same poll cadence HeadlessPlayerRegistryService.Observe
// already uses (GET /status/poll) rather than a second background timer -- observation (and now
// enforcement) only happens while something is actively polling, the same accepted limitation
// already documented for the player registry. Reuses PlayerModerationCoordinator's existing
// REST-first-RCON-fallback kick path rather than a third, whitelist-specific moderation route.
public sealed class HeadlessWhitelistService
{
    private readonly string configPath;
    private readonly HeadlessActivityLogService activity;
    private readonly PlayerModerationCoordinator playerModeration;
    private readonly object gate = new();
    private WhitelistConfig config;

    // A player already kicked this "session" (i.e. since they were last seen offline, or since the
    // config last changed) is not re-kicked on every single 5-second poll tick before the kick's
    // own effect -- them actually leaving the online list -- is observed.
    private readonly HashSet<string> recentlyEnforced = new(StringComparer.OrdinalIgnoreCase);

    public HeadlessWhitelistService(IServerPathProfile paths, HeadlessActivityLogService activity, PlayerModerationCoordinator playerModeration)
    {
        this.activity = activity;
        this.playerModeration = playerModeration;
        var root = Path.Combine(paths.ManagerRuntimeRoot, "players");
        Directory.CreateDirectory(root);
        configPath = Path.Combine(root, "whitelist.json");
        config = Load();
    }

    public WhitelistConfig GetConfig()
    {
        lock (gate) return config;
    }

    public WhitelistConfig SaveConfig(WhitelistConfig updated)
    {
        lock (gate)
        {
            var normalized = updated.Entries
                .Select(e => e with { PlayerId = (e.PlayerId ?? string.Empty).Trim() })
                .Where(e => !string.IsNullOrWhiteSpace(e.PlayerId))
                .GroupBy(e => e.PlayerId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToArray();
            config = updated with { Entries = normalized };
            Persist();
            recentlyEnforced.Clear();
            return config;
        }
    }

    public async Task EnforceAsync(HeadlessPlayersSnapshot players, CancellationToken cancellationToken)
    {
        if (!players.Available) return;

        WhitelistConfig snapshot;
        lock (gate) snapshot = config;
        if (!snapshot.Enabled)
        {
            lock (gate) recentlyEnforced.Clear();
            return;
        }

        var allowed = new HashSet<string>(snapshot.Entries.Select(e => e.PlayerId), StringComparer.OrdinalIgnoreCase);
        var onlineIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var player in players.Players)
        {
            var id = (player.PlayerId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(id)) continue;
            onlineIds.Add(id);
            if (allowed.Contains(id)) continue;

            bool alreadyHandled;
            lock (gate) alreadyHandled = !recentlyEnforced.Add(id);
            if (alreadyHandled) continue;

            var result = await playerModeration.ExecuteAsync("kick", id, "Removed: not on this server's whitelist.", cancellationToken);
            activity.Record(result.Success ? "Warning" : "Error", "Players", "Whitelist auto-kick",
                result.Success
                    ? $"{player.Name} ({id}) was kicked: not on the whitelist."
                    : $"{player.Name} ({id}) is not on the whitelist, but the auto-kick failed: {result.Message}");
        }

        lock (gate) recentlyEnforced.RemoveWhere(id => !onlineIds.Contains(id));
    }

    private WhitelistConfig Load()
    {
        try { return File.Exists(configPath) ? JsonSerializer.Deserialize<WhitelistConfig>(File.ReadAllText(configPath)) ?? WhitelistConfig.Default : WhitelistConfig.Default; }
        catch { return WhitelistConfig.Default; }
    }

    private void Persist()
    {
        var partial = configPath + ".partial";
        File.WriteAllText(partial, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(partial, configPath, true);
    }
}
