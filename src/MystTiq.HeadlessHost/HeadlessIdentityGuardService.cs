// MystTiq v1.0.0.3: file reviewed for this release (2026-10-05).
using System.Diagnostics;
using System.Text.Json;
using MystTiq.Core.Providers;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.0.1, the identity guard (reported 2026-10-04: launching through MystTiq "changes the user information and doesn't
/// load the correct profile"). A Palworld server finds a Steam player's character by the player ID their Steam ID gives
/// (SteamPlayerUid). In some sessions the server gave the same accounts other IDs, and the players were sent to create a new
/// character while their real one sat unused. The guard compares each online Steam player's assigned ID with the expected
/// one; when their real character exists but they did not get it, it records the event (with what was running), raises a
/// Critical notification and, by default, kicks them before they create a duplicate character. A player whose expected
/// character does not exist (new, or moved from a co-op world) is left alone. It runs on the same poll as the whitelist.
/// </summary>
public sealed class HeadlessIdentityGuardService
{
    private const int MaximumEvents = 200;
    private readonly IServerPathProfile paths;
    private readonly HeadlessActivityLogService activity;
    private readonly HeadlessNotificationService notifications;
    private readonly PlayerModerationCoordinator playerModeration;
    private readonly string configPath;
    private readonly string eventsPath;
    private readonly object gate = new();
    private readonly HashSet<string> handledThisSession = new(StringComparer.OrdinalIgnoreCase);
    private IdentityGuardConfig config;

    public HeadlessIdentityGuardService(IServerPathProfile paths, HeadlessActivityLogService activity, HeadlessNotificationService notifications, PlayerModerationCoordinator playerModeration)
    {
        this.paths = paths;
        this.activity = activity;
        this.notifications = notifications;
        this.playerModeration = playerModeration;
        var root = Path.Combine(paths.ManagerRuntimeRoot, "players");
        Directory.CreateDirectory(root);
        configPath = Path.Combine(root, "identity-guard.json");
        eventsPath = Path.Combine(root, "identity-guard-events.json");
        config = Load();
    }

    public IdentityGuardConfig GetConfig() { lock (gate) return config; }

    public IdentityGuardConfig SaveConfig(IdentityGuardConfig updated)
    {
        lock (gate)
        {
            config = updated;
            File.WriteAllText(configPath, JsonSerializer.Serialize(updated));
            return config;
        }
    }

    public IReadOnlyList<IdentityGuardEvent> RecentEvents()
    {
        try { return File.Exists(eventsPath) ? JsonSerializer.Deserialize<List<IdentityGuardEvent>>(File.ReadAllText(eventsPath)) ?? [] : []; }
        catch { return []; }
    }

    public async Task EnforceAsync(HeadlessPlayersSnapshot players, CancellationToken cancellationToken)
    {
        if (!players.Available) return;
        IdentityGuardConfig snapshot;
        lock (gate) snapshot = config;
        var online = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var player in players.Players)
        {
            var userId = (player.UserId ?? string.Empty).Trim();
            if (userId.Length == 0) continue;
            online.Add(userId);
            var verdict = IdentityGuard.Judge(userId, player.PlayerId, ExistingCharacter);
            if (verdict is null) continue;
            lock (gate) if (!handledThisSession.Add(userId)) continue;
            if (!snapshot.Enabled) continue;

            var kicked = false;
            var kickNote = string.Empty;
            if (snapshot.KickMismatched)
            {
                var result = await playerModeration.ExecuteAsync("kick", userId,
                    "MystTiq: the server did not load your character. Please wait for the administrator and join again.", cancellationToken);
                kicked = result.Success;
                kickNote = result.Success ? " They were kicked so that no new character is made." : $" The kick failed: {result.Message}.";
            }

            var name = string.IsNullOrWhiteSpace(player.Name) ? userId : player.Name;
            var message = $"{name} ({userId}) was not given their character: the server assigned {verdict.AssignedDisplay}, but their Steam ID's character is {verdict.Expected[..8]}.{kickNote} Their character is safe. Restart the server and have them join again. If it happens again, compare the launch on Server > Launcher with a manual start.";
            activity.Record("Warning", "Players", "Identity guard", message);
            notifications.Create("Critical", "Player not given their character", message, pinned: true);
            Append(new IdentityGuardEvent(DateTimeOffset.UtcNow, name, userId, verdict.Assigned, verdict.Expected, kicked, SteamClientRunning(), message));
        }

        lock (gate) handledThisSession.RemoveWhere(id => !online.Contains(id));
    }

    // Whether the world has a character file for this player ID (any world under the save root, not the backup copies).
    private bool ExistingCharacter(string playerId)
    {
        try
        {
            if (!Directory.Exists(paths.SaveRoot)) return false;
            return Directory.EnumerateFiles(paths.SaveRoot, playerId + ".sav", SearchOption.AllDirectories)
                .Any(f => string.Equals(Path.GetFileName(Path.GetDirectoryName(f)), "Players", StringComparison.OrdinalIgnoreCase) &&
                          !f.Contains(Path.DirectorySeparatorChar + "backup" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    // Recorded with each event: the Steam client on the server's computer was not running before 2026-10-01 07:57, when
    // one wrong ID was given; it may matter, so each event says.
    private static bool? SteamClientRunning()
    {
        try { return Process.GetProcessesByName("steam").Length > 0; } catch { return null; }
    }

    private void Append(IdentityGuardEvent entry)
    {
        lock (gate)
        {
            var list = RecentEvents().ToList();
            list.Add(entry);
            if (list.Count > MaximumEvents) list.RemoveRange(0, list.Count - MaximumEvents);
            try { File.WriteAllText(eventsPath, JsonSerializer.Serialize(list)); } catch { }
        }
    }

    private IdentityGuardConfig Load()
    {
        try { return File.Exists(configPath) ? JsonSerializer.Deserialize<IdentityGuardConfig>(File.ReadAllText(configPath)) ?? IdentityGuardConfig.Default : IdentityGuardConfig.Default; }
        catch { return IdentityGuardConfig.Default; }
    }
}

public sealed record IdentityGuardConfig(bool Enabled, bool KickMismatched)
{
    public static IdentityGuardConfig Default { get; } = new(true, true);
}

public sealed record IdentityGuardEvent(DateTimeOffset At, string Name, string UserId, string Assigned, string Expected, bool Kicked, bool? SteamClientRunning, string Message);

/// <summary>A player who did not get their own character: the ID they were given (or "None" while creating one) and the expected one.</summary>
public sealed record IdentityVerdict(string Assigned, string Expected)
{
    public string AssignedDisplay => Assigned is "" or "None" ? "no character (the new-character screen)" : Assigned[..Math.Min(8, Assigned.Length)];
}

/// <summary>v1.0.0.1: the identity guard's rule, pure so the logic harness covers it.</summary>
public static class IdentityGuard
{
    public static IdentityVerdict? Judge(string userId, string? assignedPlayerId, Func<string, bool> characterExists)
    {
        var expected = SteamPlayerUid.FromUserId(userId);
        if (expected is null) return null;  // not a Steam player: no expected ID to compare with
        var assigned = (assignedPlayerId ?? string.Empty).Trim();
        if (string.Equals(assigned, expected, StringComparison.OrdinalIgnoreCase)) return null;
        // Only when their own character exists: a new player, or one moved from a co-op world (host ID ...0001), is fine.
        var shown = assigned.Length == 0 || assigned.Equals("None", StringComparison.OrdinalIgnoreCase) ? "None" : assigned.ToUpperInvariant();
        return characterExists(expected) ? new IdentityVerdict(shown, expected) : null;
    }
}