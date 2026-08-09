using PalworldManager.Models;

namespace PalworldManager.Services;

/// <summary>
/// Session-scoped live-world telemetry aggregator.
/// It owns transient player/session counters and combines them with authoritative
/// save-backed world clock/save timestamps supplied by the active-world pipeline.
/// </summary>
public sealed class WorldTelemetryService
{
    private readonly object gate = new();
    private readonly WorldClockProvider worldClock = new();
    private long sessionId;
    private bool initializedPlayers;
    private Dictionary<string, WorldTelemetryPlayer> onlinePlayers = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> uniqueKeys = new(StringComparer.OrdinalIgnoreCase);
    private int peakPlayers;
    private int joins;
    private int leaves;
    private string lastPlayerEvent = "No player transition observed";
    private long? lastWorldDay;

    public WorldTelemetryUpdate Update(
        long activeSessionId,
        DateTime? sessionStartedAt,
        IEnumerable<WorldTelemetryPlayer> players,
        string decodedWorldJsonPath,
        DateTime levelSaveWriteUtc,
        DateTime? latestBackupLocal)
    {
        lock (gate)
        {
            if (activeSessionId != sessionId)
                ResetSession(activeSessionId);

            var nowUtc = DateTime.UtcNow;
            var incoming = players
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToDictionary(x => x.Key, x => x, StringComparer.OrdinalIgnoreCase);

            var currentKeys = incoming.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var events = new List<WorldTelemetryEvent>();

            if (!initializedPlayers)
            {
                onlinePlayers = new Dictionary<string, WorldTelemetryPlayer>(incoming, StringComparer.OrdinalIgnoreCase);
                uniqueKeys.UnionWith(currentKeys);
                peakPlayers = Math.Max(peakPlayers, currentKeys.Count);
                initializedPlayers = true;
            }
            else
            {
                foreach (var key in currentKeys.Except(onlinePlayers.Keys, StringComparer.OrdinalIgnoreCase))
                {
                    joins++;
                    uniqueKeys.Add(key);
                    var name = DisplayName(incoming[key]);
                    lastPlayerEvent = $"{name} joined";
                    events.Add(new(nowUtc, "PlayerJoined", lastPlayerEvent, $"Session join #{joins}"));
                }

                foreach (var key in onlinePlayers.Keys.Except(currentKeys, StringComparer.OrdinalIgnoreCase))
                {
                    leaves++;
                    var name = onlinePlayers.TryGetValue(key, out var previous) ? DisplayName(previous) : key;
                    lastPlayerEvent = $"{name} left";
                    events.Add(new(nowUtc, "PlayerLeft", lastPlayerEvent, $"Session leave #{leaves}"));
                }

                onlinePlayers = new Dictionary<string, WorldTelemetryPlayer>(incoming, StringComparer.OrdinalIgnoreCase);
                uniqueKeys.UnionWith(currentKeys);
                peakPlayers = Math.Max(peakPlayers, currentKeys.Count);
            }

            var clock = worldClock.Read(decodedWorldJsonPath, levelSaveWriteUtc);
            if (clock.Available)
            {
                if (lastWorldDay.HasValue && lastWorldDay.Value != clock.DayNumber)
                {
                    events.Add(new(nowUtc, "WorldDayChanged", $"Day {clock.DayNumber:N0} began",
                        $"Saved world clock advanced from Day {lastWorldDay.Value:N0} to Day {clock.DayNumber:N0}."));
                }
                lastWorldDay = clock.DayNumber;
            }

            var uptime = sessionStartedAt.HasValue ? DateTime.Now - sessionStartedAt.Value : TimeSpan.Zero;
            if (uptime < TimeSpan.Zero) uptime = TimeSpan.Zero;

            var snapshot = new WorldTelemetrySnapshot
            {
                SessionId = activeSessionId,
                SessionStartedAt = sessionStartedAt,
                SessionUptime = uptime,
                WorldClock = clock,
                LastWorldSaveUtc = levelSaveWriteUtc == DateTime.MinValue ? null : levelSaveWriteUtc,
                LastBackupLocal = latestBackupLocal,
                OnlinePlayers = currentKeys.Count,
                PeakPlayers = peakPlayers,
                SessionJoins = joins,
                SessionLeaves = leaves,
                UniquePlayers = uniqueKeys.Count,
                LastPlayerEvent = lastPlayerEvent,
                CapturedUtc = nowUtc
            };

            return new(snapshot, events);
        }
    }

    private void ResetSession(long newSessionId)
    {
        sessionId = newSessionId;
        initializedPlayers = false;
        onlinePlayers = new(StringComparer.OrdinalIgnoreCase);
        uniqueKeys = new(StringComparer.OrdinalIgnoreCase);
        peakPlayers = 0;
        joins = 0;
        leaves = 0;
        lastPlayerEvent = "No player transition observed";
        lastWorldDay = null;
    }

    private static string DisplayName(WorldTelemetryPlayer player) =>
        string.IsNullOrWhiteSpace(player.Name) ? player.Key : player.Name;
}
