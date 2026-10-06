// MystTiq v1.0.4.0: file reviewed for this release (2026-10-05).
using System.Text.Json;

namespace MystTiq.HeadlessHost;

// v1.0.2.0 (roadmap R-2, alert delivery proof): one send of one notification to one outside channel, and how it went.
public sealed record NotificationDeliveryRecord(DateTimeOffset AtUtc, string Channel, string Title, bool Success, string Detail, int Attempts);

// A channel's delivery health over the window. State: Delivered (its last send arrived), Failing (sends were tried in
// the window and the last one failed), NotProven (nothing was delivered in the window), Off (switched off).
public sealed record NotificationChannelHealth(
    string Channel, bool Enabled, string State, bool Flagged, string Detail,
    DateTimeOffset? LastAttemptUtc, DateTimeOffset? LastSuccessUtc, string? LastError, int AttemptsInWindow, int SuccessesInWindow);

public sealed record NotificationDeliveryHealth(int WindowDays, IReadOnlyList<NotificationChannelHealth> Channels, IReadOnlyList<NotificationDeliveryRecord> Recent, bool AnyFlagged, string Summary);

/// <summary>
/// v1.0.2.0 (roadmap R-2): every send to Discord, email or a webhook is recorded with its result (before, only failures
/// reached the Activity log, so a channel that silently stopped working looked the same as a quiet one). A switched-on
/// channel with no successful delivery in the window, or whose last send failed, is flagged on the Dashboard and in
/// Alert Center. Kept under the profile's notifications folder, newest last, at most 500 records.
/// </summary>
public sealed class NotificationDeliveryLog
{
    public static readonly TimeSpan Window = TimeSpan.FromDays(7);
    private const int Keep = 500;
    private readonly string path;
    private readonly object gate = new();
    private List<NotificationDeliveryRecord> records;

    public NotificationDeliveryLog(string notificationsRoot)
    {
        path = Path.Combine(notificationsRoot, "deliveries.json");
        try { records = File.Exists(path) ? JsonSerializer.Deserialize<List<NotificationDeliveryRecord>>(File.ReadAllText(path)) ?? [] : []; }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { records = []; }
    }

    public void Record(NotificationDeliveryRecord record)
    {
        lock (gate)
        {
            records.Add(record);
            if (records.Count > Keep) records.RemoveRange(0, records.Count - Keep);
            try
            {
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(records));
                File.Move(temp, path, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* the record stays in memory */ }
        }
    }

    public IReadOnlyList<NotificationDeliveryRecord> Snapshot() { lock (gate) return records.ToList(); }

    // Pure, so the logic harness covers every state.
    public static NotificationDeliveryHealth Health(IEnumerable<(string Channel, bool Enabled)> channels, IReadOnlyList<NotificationDeliveryRecord> records, DateTimeOffset now, TimeSpan window)
    {
        var days = Math.Max(1, (int)Math.Round(window.TotalDays));
        var result = new List<NotificationChannelHealth>();
        foreach (var (channel, enabled) in channels)
        {
            var mine = records.Where(r => r.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase)).OrderBy(r => r.AtUtc).ToList();
            var inWindow = mine.Where(r => now - r.AtUtc <= window).ToList();
            var last = mine.LastOrDefault();
            var lastSuccess = mine.LastOrDefault(r => r.Success);
            var lastError = mine.LastOrDefault(r => !r.Success)?.Detail;
            string state, detail;
            if (!enabled) { state = "Off"; detail = "Switched off."; }
            else if (inWindow.Count > 0 && last is { Success: false })
            {
                state = "Failing";
                detail = $"The last send failed: {last.Detail}" + (lastSuccess is null ? " Nothing has been delivered yet." : $" Last delivered {lastSuccess.AtUtc:yyyy-MM-dd HH:mm} UTC.");
            }
            else if (inWindow.Any(r => r.Success)) { state = "Delivered"; detail = $"Last delivered {lastSuccess!.AtUtc:yyyy-MM-dd HH:mm} UTC."; }
            else
            {
                state = "NotProven";
                detail = lastSuccess is null
                    ? "Nothing has been delivered through this channel yet. Send a test to prove it works."
                    : $"Nothing was delivered in the last {days} days (last delivered {lastSuccess.AtUtc:yyyy-MM-dd HH:mm} UTC). Send a test to prove it still works.";
            }
            result.Add(new(channel, enabled, state, enabled && state != "Delivered", detail, last?.AtUtc, lastSuccess?.AtUtc, lastError,
                inWindow.Count, inWindow.Count(r => r.Success)));
        }
        var flagged = result.Where(c => c.Flagged).ToList();
        var summary = result.All(c => !c.Enabled)
            ? "No outside channel is switched on; notifications only appear in MystTiq."
            : flagged.Count == 0
                ? $"Every switched-on channel delivered in the last {days} days."
                : $"{string.Join(", ", flagged.Select(c => c.Channel))}: no proven delivery in the last {days} days. Open Alert Center.";
        return new(days, result, records.OrderByDescending(r => r.AtUtc).Take(20).ToList(), flagged.Count > 0, summary);
    }
}
