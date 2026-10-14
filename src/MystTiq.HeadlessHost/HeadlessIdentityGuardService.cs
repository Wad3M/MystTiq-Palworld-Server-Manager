// MystTiq v1.0.9.2: file reviewed for this release (2026-10-12).
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
    // v1.0.7.3: when each online player was first seen with no character yet (still loading, or on the new-character screen).
    private readonly Dictionary<string, DateTimeOffset> noCharacterSince = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<DateTimeOffset> clock;
    private IdentityGuardConfig config;

    private readonly Func<IReadOnlyList<string>> launchArguments;

    // v1.0.6.1: launchArguments are the server's saved start arguments, named in the alert (the wrong-character starts all
    // had -log, -stdout or -FullStdOutLogOutput; double-click starts, with no arguments, kept the players' characters).
    public HeadlessIdentityGuardService(IServerPathProfile paths, HeadlessActivityLogService activity, HeadlessNotificationService notifications, PlayerModerationCoordinator playerModeration,
        Func<IReadOnlyList<string>>? launchArguments = null, Func<DateTimeOffset>? clock = null)
    {
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.launchArguments = launchArguments ?? (() => []);
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
            SavedFiles.SaveText(configPath, JsonSerializer.Serialize(updated));
            config = updated;
            return config;
        }
    }

    public IReadOnlyList<IdentityGuardEvent> RecentEvents()
    {
        return SavedFiles.Load(eventsPath, "identity guard events", () => new List<IdentityGuardEvent>(), ownerData: false);
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
            if (verdict is null)
            {
                lock (gate) noCharacterSince.Remove(userId);
                continue;
            }
            // v1.0.7.3 (owner report 2026-10-11): a returning player is listed with no character ("None") while still loading
            // into the world. The guard used to kick at once, so the owner (Wade) was kicked 8 s after connecting, twice,
            // before his own character had loaded. No character now waits LoadingGrace; a different character is acted on
            // at once, as before.
            if (verdict.Assigned == "None")
            {
                var now = clock();
                DateTimeOffset since;
                lock (gate)
                {
                    if (!noCharacterSince.TryGetValue(userId, out since)) noCharacterSince[userId] = since = now;
                }
                if (!IdentityGuard.NoCharacterIsDue(since, now)) continue;
            }
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
            var message = $"{name} ({userId}) was not given their character: the server assigned {verdict.AssignedDisplay}, but their Steam ID's character is {verdict.Expected[..8]}.{kickNote} Their character is safe. "
                + IdentityGuard.LaunchAdvice(launchArguments());
            activity.Record("Warning", "Players", "Identity guard", message);
            notifications.Create("Critical", "Player not given their character", message, pinned: true);
            Append(new IdentityGuardEvent(DateTimeOffset.UtcNow, name, userId, verdict.Assigned, verdict.Expected, kicked, SteamClientRunning(), message));
        }

        lock (gate)
        {
            handledThisSession.RemoveWhere(id => !online.Contains(id));
            foreach (var gone in noCharacterSince.Keys.Where(id => !online.Contains(id)).ToList()) noCharacterSince.Remove(gone);
        }
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
            try { SavedFiles.SaveText(eventsPath, JsonSerializer.Serialize(list)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* the event is still alerted and logged */ }
        }
    }

    // v1.0.7.0 (Q-1): an unreadable identity guard setting is never overwritten.
    private IdentityGuardConfig Load() => SavedFiles.Load(configPath, "identity guard settings", () => IdentityGuardConfig.Default);
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
    // v1.0.6.1: the start options present in every wrong-character start (2026-10-04, 2026-10-06), never in a good one.
    public static readonly string[] SuspectArguments = ["-log", "-stdout", "-FullStdOutLogOutput"];

    public static bool HasSuspectArguments(IEnumerable<string> arguments) =>
        arguments.Any(a => SuspectArguments.Any(s => a.Equals(s, StringComparison.OrdinalIgnoreCase)) || a.StartsWith("-abslog=", StringComparison.OrdinalIgnoreCase));

    // What the alert advises, from the arguments the server was started with (MystTiq's own @mysttiq: options left out).
    public static string LaunchAdvice(IReadOnlyList<string> arguments)
    {
        var shown = SecretRedaction.Arguments(arguments.Where(a => !a.StartsWith("@mysttiq:", StringComparison.OrdinalIgnoreCase))).ToList();
        return HasSuspectArguments(shown)
            ? $"This server starts PalServer with {string.Join(" ", shown)}. Players were given the wrong character every time it started with -log, -stdout or -FullStdOutLogOutput, and kept theirs when it started like a double-click: choose Like double-click on Server > Launcher, save, restart the server, then have them join again."
            : "Restart the server and have them join again. If it happens again, compare the launch on Server > Launcher with a manual start.";
    }

    /// <summary>
    /// v1.0.7.3: how long a player listed with no character is given to finish loading before the guard acts. A returning
    /// player's character appears within seconds; making a new character on the new-character screen takes longer.
    /// </summary>
    public static TimeSpan LoadingGrace { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>True once a player first seen with no character at <paramref name="since"/> has had the loading grace.</summary>
    public static bool NoCharacterIsDue(DateTimeOffset since, DateTimeOffset now) => now - since >= LoadingGrace;

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