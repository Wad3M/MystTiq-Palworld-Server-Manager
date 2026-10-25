// MystTiq v1.1.5.0: file reviewed for this release (2026-10-25).
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

    private readonly Func<string, string?> expectedFor;
    private readonly Func<bool> autoMove;
    private readonly Func<string, string> kickReason;
    private readonly HashSet<string> makingCharacter = new(StringComparer.OrdinalIgnoreCase);
    // v1.1.1.0: when each account was last told, on the new-character screen, what to do (kept while they rejoin).
    private readonly Dictionary<string, DateTimeOffset> toldWhatToDo = new(StringComparer.OrdinalIgnoreCase);

    // v1.1.0.2 (roadmap F-2): the expected ID comes from the player ID ledger (learned from the game's own assignments;
    // the pre-1.0 Steam formula only when nothing is learned). The launch arguments the alert used to name were disproven
    // as a cause on 2026-10-13: the game itself gives accounts new IDs.
    public HeadlessIdentityGuardService(IServerPathProfile paths, HeadlessActivityLogService activity, HeadlessNotificationService notifications, PlayerModerationCoordinator playerModeration,
        Func<string, string?>? expectedFor = null, Func<DateTimeOffset>? clock = null, Func<bool>? autoMove = null, Func<string, string>? kickReason = null)
    {
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        this.expectedFor = expectedFor ?? SteamPlayerUid.FromUserId;
        this.autoMove = autoMove ?? (() => false);
        this.kickReason = kickReason ?? (_ => "MystTiq: the server did not load your character. Please wait for the administrator and join again.");
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
            var verdict = IdentityGuard.Judge(userId, player.PlayerId, ExistingCharacter, expectedFor(userId));
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
                // v1.1.0.2: with automatic moves on, the player may make a character: it gives the game's new ID, and the
                // move then replaces it with their real one (checked and kicked as soon as it exists).
                if (autoMove())
                {
                    // v1.1.1.0: a player left on the new-character screen did not know what to do. The first time (again
                    // after NewCharacterNoticeInterval) they are disconnected with the steps; on their next join they are
                    // let through to make the character the move replaces.
                    bool tell;
                    lock (gate)
                    {
                        tell = !toldWhatToDo.TryGetValue(userId, out var toldAt) || now - toldAt >= IdentityGuard.NewCharacterNoticeInterval;
                        if (tell) toldWhatToDo[userId] = now;
                    }
                    if (tell && snapshot.Enabled && snapshot.KickMismatched)
                    {
                        var told = await playerModeration.ExecuteAsync("kick", userId, IdentityGuard.NewCharacterNotice, cancellationToken);
                        activity.Record(told.Success ? "Information" : "Warning", "Players", "Identity guard",
                            $"{(string.IsNullOrWhiteSpace(player.Name) ? userId : player.Name)} ({userId}) was on the new-character screen while their character is under {verdict.Expected[..8]}. " +
                            (told.Success ? "They were disconnected with the steps: join again, make a character with any name, and MystTiq moves theirs onto it." : $"Telling them failed: {told.Message}."));
                        lock (gate) noCharacterSince.Remove(userId);
                        continue;
                    }
                    bool first;
                    lock (gate) first = makingCharacter.Add(userId);
                    if (first) activity.Record("Information", "Players", "Identity guard",
                        $"{(string.IsNullOrWhiteSpace(player.Name) ? userId : player.Name)} ({userId}) is on the new-character screen while their character is under {verdict.Expected[..8]}; once they make one, MystTiq moves their character onto it.");
                    continue;
                }
            }
            lock (gate) if (!handledThisSession.Add(userId)) continue;
            if (!snapshot.Enabled) continue;

            var kicked = false;
            var kickNote = string.Empty;
            if (snapshot.KickMismatched)
            {
                var result = await playerModeration.ExecuteAsync("kick", userId, kickReason(userId), cancellationToken);
                kicked = result.Success;
                kickNote = result.Success ? " They were kicked so that no new character is made." : $" The kick failed: {result.Message}.";
            }

            var name = string.IsNullOrWhiteSpace(player.Name) ? userId : player.Name;
            var message = $"{name} ({userId}) was not given their character: the server assigned {verdict.AssignedDisplay}, but their account's character is {verdict.Expected[..8]}.{kickNote} Their character is safe. "
                + IdentityGuard.MoveAdvice(autoMove());
            activity.Record("Warning", "Players", "Identity guard", message);
            notifications.Create("Critical", "Player not given their character", message, pinned: true);
            Append(new IdentityGuardEvent(DateTimeOffset.UtcNow, name, userId, verdict.Assigned, verdict.Expected, kicked, SteamClientRunning(), message));
        }

        lock (gate)
        {
            handledThisSession.RemoveWhere(id => !online.Contains(id));
            foreach (var gone in noCharacterSince.Keys.Where(id => !online.Contains(id)).ToList()) noCharacterSince.Remove(gone);
            makingCharacter.RemoveWhere(id => !online.Contains(id));
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
    // v1.1.1.0: the v1.0.6.1 advice naming the start options and the launcher preset to pick is gone; roadmap F-2 showed the
    // game itself gives accounts new player IDs, whatever the start options.

    /// <summary>
    /// v1.1.0.2 (roadmap F-2): what the alert advises. The game gives accounts new player IDs since its 1.0 update
    /// (shown 2026-10-13 with every way of starting the server); the fix is moving the character to the new ID.
    /// </summary>
    public static string MoveAdvice(bool autoMove) => autoMove
        ? "The game gave this account a new player ID. MystTiq moves their character to it when nobody is online, with a safety backup first, and starts the server again."
        : "The game gave this account a new player ID. Move their character to it on Players > Player IDs (a safety backup is taken first).";

    /// <summary>
    /// v1.0.7.3: how long a player listed with no character is given to finish loading before the guard acts. A returning
    /// player's character appears within seconds; making a new character on the new-character screen takes longer.
    /// </summary>
    public static TimeSpan LoadingGrace { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// v1.1.1.0: what a player on the new-character screen is told (the disconnect message) when their character exists
    /// under the ID they had before and automatic moves are on.
    /// </summary>
    public const string NewCharacterNotice =
        "MystTiq: your character is safe. The game gave your account a new player ID. Join again and make a character with any name; MystTiq then moves your own character onto it and asks you to join once more.";

    /// <summary>v1.1.1.0: how often a player is told <see cref="NewCharacterNotice"/>; in between they are let through.</summary>
    public static TimeSpan NewCharacterNoticeInterval { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>True once a player first seen with no character at <paramref name="since"/> has had the loading grace.</summary>
    public static bool NoCharacterIsDue(DateTimeOffset since, DateTimeOffset now) => now - since >= LoadingGrace;

    public static IdentityVerdict? Judge(string userId, string? assignedPlayerId, Func<string, bool> characterExists, string? expectedPlayerId = null)
    {
        // v1.1.0.2: the expected ID is the learned one when given (the game no longer derives IDs from the Steam ID).
        var expected = expectedPlayerId ?? SteamPlayerUid.FromUserId(userId);
        if (expected is null) return null;  // not a Steam player: no expected ID to compare with
        var assigned = (assignedPlayerId ?? string.Empty).Trim();
        if (string.Equals(assigned, expected, StringComparison.OrdinalIgnoreCase)) return null;
        // Only when their own character exists: a new player, or one moved from a co-op world (host ID ...0001), is fine.
        var shown = assigned.Length == 0 || assigned.Equals("None", StringComparison.OrdinalIgnoreCase) ? "None" : assigned.ToUpperInvariant();
        return characterExists(expected) ? new IdentityVerdict(shown, expected) : null;
    }
}