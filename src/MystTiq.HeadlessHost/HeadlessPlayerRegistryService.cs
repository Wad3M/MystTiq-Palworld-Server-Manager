// MystTiq v1.0.7.3: file reviewed for this release (2026-10-11).
using System.Text;
using System.Text.Json;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.6.6.0 "Player Registry, Activity Intelligence & World Explorer 2" -- real foundation slice.
// Persistent per-player identity/session history, built the same way v0.6.1.0's
// HeadlessHistoricalMetricsService already established: sample on every /status/poll (the
// existing polling cadence the Desktop already drives) rather than adding a second background
// timer to the app. That means observation only happens while something is actively polling --
// the same accepted limitation HeadlessHistoricalMetricsService already has for CPU/RAM history.
public sealed record PlayerRegistryRecord(
    string PlayerId,
    string LastKnownName,
    string SteamId,
    string UserId,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    int TotalSessions,
    double TotalPlaytimeMinutes,
    bool CurrentlyOnline,
    DateTimeOffset? CurrentSessionStartUtc);

public sealed record PlayerPresenceEvent(
    string PlayerId,
    string PlayerName,
    string EventType,
    DateTimeOffset ObservedAt);

public sealed class HeadlessPlayerRegistryService
{
    // A single /status/poll tick can never plausibly represent more real playtime than this --
    // caps accrual so a large gap between observations (MystTiq restart, nobody polling for a
    // while) can never get misattributed as playtime for a player who was online before and after.
    private static readonly TimeSpan MaximumPerTickAccrual = TimeSpan.FromMinutes(10);
    private const int MaximumEvents = 2000;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly object gate = new();
    private readonly string recordsPath;
    private readonly string eventsPath;
    private readonly Dictionary<string, PlayerRegistryRecord> records = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PlayerPresenceEvent> events = [];
    private DateTimeOffset lastPersistedAt = DateTimeOffset.MinValue;

    public HeadlessPlayerRegistryService(IServerPathProfile paths)
    {
        var root = Path.Combine(paths.ManagerRuntimeRoot, "players");
        Directory.CreateDirectory(root);
        recordsPath = Path.Combine(root, "player-registry.json");
        eventsPath = Path.Combine(root, "player-presence-events.json");
        Load();
    }

    public void Observe(HeadlessPlayersSnapshot players, DateTimeOffset observedAt)
    {
        if (!players.Available) return;

        lock (gate)
        {
            var changed = false;
            var onlineIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var player in players.Players)
            {
                var id = NormalizeId(player.PlayerId);
                if (string.IsNullOrWhiteSpace(id)) continue;
                onlineIds.Add(id);

                if (records.TryGetValue(id, out var existing))
                {
                    if (existing.CurrentlyOnline)
                    {
                        var accrued = Min(observedAt - existing.LastSeenUtc, MaximumPerTickAccrual);
                        records[id] = existing with
                        {
                            LastKnownName = Coalesce(player.Name, existing.LastKnownName),
                            SteamId = Coalesce(player.SteamId, existing.SteamId),
                            UserId = Coalesce(player.UserId, existing.UserId),
                            LastSeenUtc = observedAt,
                            TotalPlaytimeMinutes = existing.TotalPlaytimeMinutes + Math.Max(0, accrued.TotalMinutes)
                        };
                    }
                    else
                    {
                        events.Add(new PlayerPresenceEvent(id, Coalesce(player.Name, existing.LastKnownName), "Join", observedAt));
                        records[id] = existing with
                        {
                            LastKnownName = Coalesce(player.Name, existing.LastKnownName),
                            SteamId = Coalesce(player.SteamId, existing.SteamId),
                            UserId = Coalesce(player.UserId, existing.UserId),
                            LastSeenUtc = observedAt,
                            TotalSessions = existing.TotalSessions + 1,
                            CurrentlyOnline = true,
                            CurrentSessionStartUtc = observedAt
                        };
                    }
                }
                else
                {
                    records[id] = new PlayerRegistryRecord(id, player.Name, player.SteamId, player.UserId,
                        observedAt, observedAt, 1, 0, true, observedAt);
                    events.Add(new PlayerPresenceEvent(id, player.Name, "Join", observedAt));
                }
                changed = true;
            }

            foreach (var record in records.Values.Where(r => r.CurrentlyOnline && !onlineIds.Contains(r.PlayerId)).ToList())
            {
                events.Add(new PlayerPresenceEvent(record.PlayerId, record.LastKnownName, "Leave", observedAt));
                records[record.PlayerId] = record with { CurrentlyOnline = false, CurrentSessionStartUtc = null, LastSeenUtc = observedAt };
                changed = true;
            }

            if (events.Count > MaximumEvents)
                events.RemoveRange(0, events.Count - MaximumEvents);

            if (changed && DateTimeOffset.UtcNow - lastPersistedAt >= TimeSpan.FromSeconds(30))
                Save();
        }
    }

    public IReadOnlyList<PlayerRegistryRecord> Snapshot()
    {
        lock (gate)
            return records.Values.OrderByDescending(r => r.LastSeenUtc).ToArray();
    }

    public IReadOnlyList<PlayerPresenceEvent> RecentEvents(int maximum = 200)
    {
        lock (gate)
            return events.AsEnumerable().Reverse().Take(Math.Clamp(maximum, 1, MaximumEvents)).ToArray();
    }

    // v0.7.75.0: part of "Delete Player Completely" -- removes this player's own registry record
    // and every presence event naming them, then persists immediately (not on the usual 30s
    // throttle) since a deletion is a deliberate, one-shot admin action that should be durable
    // right away, not lost if the process exits before the next throttled save.
    public void Forget(string playerId)
    {
        var id = NormalizeId(playerId);
        if (string.IsNullOrWhiteSpace(id)) return;
        lock (gate)
        {
            records.Remove(id);
            events.RemoveAll(e => string.Equals(e.PlayerId, id, StringComparison.OrdinalIgnoreCase));
            Save();
        }
    }

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
    private static string Coalesce(string? incoming, string fallback) => string.IsNullOrWhiteSpace(incoming) ? fallback : incoming;
    private static string NormalizeId(string? value) => (value ?? string.Empty).Trim();

    // v1.0.7.0 (Q-1): the player history is the owner's record of who played (first seen, names, play time): an
    // unreadable one falls back to its last good copy, else it is kept as it is and not overwritten. Presence events are
    // a log and start empty. Neither ever blocks management start-up.
    private void Load()
    {
        foreach (var record in SavedFiles.Load(recordsPath, "player history", () => new List<PlayerRegistryRecord>(), options: JsonOptions))
            records[record.PlayerId] = record.CurrentlyOnline ? record with { CurrentlyOnline = false, CurrentSessionStartUtc = null } : record;
        events.AddRange(SavedFiles.Load(eventsPath, "player presence events", () => new List<PlayerPresenceEvent>(), ownerData: false, options: JsonOptions));
    }

    private void Save()
    {
        // A persistence failure must not affect PalServer lifecycle operations. A refused history (unreadable, not yet
        // repaired) keeps what it learns in memory only; the owner was alerted when it was read.
        try { SavedFiles.Save(recordsPath, records.Values.ToArray(), JsonOptions); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SavedFileRefusedException) { }
        try { SavedFiles.Save(eventsPath, events, JsonOptions); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        lastPersistedAt = DateTimeOffset.UtcNow;
    }
}
