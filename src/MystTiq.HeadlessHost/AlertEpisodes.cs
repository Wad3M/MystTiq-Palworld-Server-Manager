using System.Text.Json;

namespace MystTiq.HeadlessHost;

public enum EpisodeAction
{
    // Nothing to send: the condition is unchanged, or it was suppressed as flapping.
    None,
    // The condition has just become true: send the alert.
    Alert,
    // The condition has just cleared and an alert had been sent for it: send a recovery notice.
    Recovered,
    // The condition is still true, an alert is still open, and the reminder interval has elapsed since
    // the alert (or the last reminder): send a follow-up so a long-running Critical is not forgotten.
    Reminder
}

public sealed class EpisodeState
{
    public bool Active { get; set; }
    // True while an alert was sent for the current episode and no recovery has been sent yet.
    public bool AlertOpen { get; set; }
    public DateTimeOffset? LastAlertAt { get; set; }
    // v0.7.104.0: the id of the notification the open alert created, so it can be unpinned once the
    // episode ends. Null for an episode with no alert open (never alerted, or already resolved/closed).
    public string? AlertNotificationId { get; set; }
    // v0.7.107.0: when the last reminder was sent for the currently open alert, if any. Null means none
    // has been sent yet for this episode, so the reminder clock runs from LastAlertAt instead.
    public DateTimeOffset? LastReminderAt { get; set; }
}

// v0.7.102.0: an alert is about an episode (a condition that starts and later ends), not a timer. The Alert
// Center used to re-send a condition that stayed true every cooldown (30 to 60 minutes), each one a pinned
// Critical, and forgot everything on a restart: the test server had five identical "Low disk space"
// alerts among 83 unread. Now a condition alerts once when it starts, says nothing while it stays true, and
// sends one recovery notice when it clears. The cooldown survives as a flapping guard: a condition that
// clears and comes back inside it does not alert again. State is persisted, so a restart does not re-alert.
//
// v0.7.104.0: a pinned Critical alert used to stay pinned forever, even once the "Resolved: ..." notice
// had already been sent (or, worse, once the rule itself was switched off, which sends no notice at all).
// The caller now records which notification an open alert created (SetAlertNotificationId) and gets its
// id back from Observe/Close when the episode ends, so it can unpin that notification.
//
// v0.7.107.0: a condition that stayed true for a long time (days) sent exactly one alert and then nothing
// else until it cleared -- named as a gap since v0.7.102.0 first introduced episodes. An optional reminder
// interval (opt-in per call, defaults to none so every existing caller is unaffected) makes Observe return
// Reminder once that long since the alert (or the last reminder). A fresh episode, a recovery, or the rule
// being switched off all reset the reminder clock, so it never fires stale or carries into a new episode.
public sealed class AlertEpisodeTracker
{
    private readonly object gate = new();
    private readonly string? statePath;
    private readonly Dictionary<string, EpisodeState> states;

    public AlertEpisodeTracker(string? statePath = null)
    {
        this.statePath = statePath;
        states = Load(statePath);
    }

    public EpisodeAction Observe(string ruleKey, bool active, DateTimeOffset now, TimeSpan flapGuard)
        => Observe(ruleKey, active, now, flapGuard, out _);

    // Same as above, plus the id of the notification the closing alert created (null unless this call
    // returns Recovered and that alert is still known), so the caller can unpin it. reminderInterval is
    // opt-in: null (the default) means no reminders, matching every caller before v0.7.107.0 exactly.
    public EpisodeAction Observe(string ruleKey, bool active, DateTimeOffset now, TimeSpan flapGuard, out string? recoveredNotificationId, TimeSpan? reminderInterval = null)
    {
        lock (gate)
        {
            recoveredNotificationId = null;
            if (!states.TryGetValue(ruleKey, out var state)) states[ruleKey] = state = new EpisodeState();
            var action = EpisodeAction.None;
            var changed = false;

            if (active && !state.Active)
            {
                changed = true;
                state.Active = true;
                var recentlyAlerted = state.LastAlertAt is { } last && now - last < flapGuard;
                if (!recentlyAlerted)
                {
                    state.AlertOpen = true;
                    state.LastAlertAt = now;
                    state.LastReminderAt = null;
                    action = EpisodeAction.Alert;
                }
            }
            else if (!active && state.Active)
            {
                changed = true;
                state.Active = false;
                if (state.AlertOpen)
                {
                    state.AlertOpen = false;
                    recoveredNotificationId = state.AlertNotificationId;
                    state.AlertNotificationId = null;
                    state.LastReminderAt = null;
                    action = EpisodeAction.Recovered;
                }
            }
            else if (active && state.Active && state.AlertOpen && reminderInterval is { } interval && interval > TimeSpan.Zero)
            {
                // No transition this tick: the condition is still exactly as it was. Remind only once a
                // full interval has passed since the alert, or since the last reminder if one was already sent.
                var since = state.LastReminderAt ?? state.LastAlertAt;
                if (since is { } last && now - last >= interval)
                {
                    state.LastReminderAt = now;
                    changed = true;
                    action = EpisodeAction.Reminder;
                }
            }

            if (changed) Save();
            return action;
        }
    }

    // Records which notification the alert just sent for ruleKey created, so a later Observe/Close that
    // ends the episode can hand that id back for unpinning. A no-op if the episode is not known (should
    // not happen: Observe always creates the entry before the caller can send the alert it names here).
    public void SetAlertNotificationId(string ruleKey, string notificationId)
    {
        lock (gate)
        {
            if (!states.TryGetValue(ruleKey, out var state)) return;
            state.AlertNotificationId = notificationId;
            Save();
        }
    }

    // A rule that was switched off must not leave an episode open: closing it silently means turning
    // the rule back on later starts a fresh episode instead of being treated as still-active. Returns the
    // id of the notification the open alert created, if any, so the caller can unpin it -- switching a
    // rule off sends no "Resolved" notice, so without this the alert would stay pinned forever.
    public string? Close(string ruleKey)
    {
        lock (gate)
        {
            if (!states.TryGetValue(ruleKey, out var state) || (!state.Active && !state.AlertOpen)) return null;
            state.Active = false;
            state.AlertOpen = false;
            state.LastReminderAt = null;
            var notificationId = state.AlertNotificationId;
            state.AlertNotificationId = null;
            Save();
            return notificationId;
        }
    }

    public bool IsActive(string ruleKey)
    {
        lock (gate) return states.TryGetValue(ruleKey, out var s) && s.Active;
    }

    private void Save()
    {
        if (statePath is null) return;
        try
        {
            var partial = statePath + ".partial";
            File.WriteAllText(partial, JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(partial, statePath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing the persisted state only means one repeated alert after a restart; never fail the tick.
        }
    }

    private static Dictionary<string, EpisodeState> Load(string? path)
    {
        try
        {
            if (path is not null && File.Exists(path))
                return JsonSerializer.Deserialize<Dictionary<string, EpisodeState>>(File.ReadAllText(path)) ?? new(StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { }
        return new(StringComparer.Ordinal);
    }
}
