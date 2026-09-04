using System.Text.Json;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

public sealed class HeadlessNotificationService
{
    private const int MaximumNotifications = 500;
    private readonly object gate = new();
    private readonly string statePath;
    private readonly HeadlessActivityLogService activity;
    private readonly HeadlessNotificationRoutingService? routing;

    public HeadlessNotificationService(IServerPathProfile paths, HeadlessActivityLogService activity, HeadlessNotificationRoutingService? routing = null)
    {
        this.activity = activity;
        this.routing = routing;
        var root = Path.Combine(paths.ManagerRuntimeRoot, "notifications");
        Directory.CreateDirectory(root);
        statePath = Path.Combine(root, "state.json");
    }

    public HeadlessNotificationSnapshot GetSnapshot()
    {
        lock (gate)
        {
            var items = Load().OrderByDescending(x => x.Pinned).ThenByDescending(x => x.CreatedUtc).ToList();
            return new(items, items.Count(x => !x.Read), items.Count(x => x.Pinned), DateTimeOffset.UtcNow,
                $"{items.Count} notification(s), {items.Count(x => !x.Read)} unread.");
        }
    }

    public HeadlessNotificationSnapshot CreateSelfTest()
    {
        lock (gate)
        {
            var items = Load();
            var batch = Guid.NewGuid().ToString("N")[..8];
            items.AddRange(new[]
            {
                New("Information", "Notification diagnostics", $"Information channel test ({batch}).", false),
                New("Success", "Notification diagnostics", $"Success channel test ({batch}).", false),
                New("Warning", "Notification diagnostics", $"Warning channel test ({batch}).", false),
                New("Critical", "Notification diagnostics", $"Critical channel test ({batch}).", true)
            });
            Save(items);
            activity.Record("Information", "Notifications", "Created notification self-test", $"batch={batch}; count=4");
            return GetSnapshotUnsafe();
        }
    }

    // General-purpose notification creation -- the entire integration surface the Alert Center
    // and the automation SendNotification action need. No parallel notification pipeline.
    public HeadlessNotificationSnapshot Create(string severity, string title, string message, bool pinned = false)
    {
        HeadlessNotificationSnapshot snapshot;
        lock (gate)
        {
            var items = Load();
            items.Add(New(severity, title, message, pinned));
            Save(items);
            activity.Record("Information", "Notifications", "Created notification", $"severity={severity}; title={title}");
            snapshot = GetSnapshotUnsafe();
        }
        routing?.Dispatch(severity, title, message);
        return snapshot;
    }

    public HeadlessNotificationSnapshot SetRead(string id, bool read) => Mutate(id, "read", item => item.Read = read);
    public HeadlessNotificationSnapshot SetPinned(string id, bool pinned) => Mutate(id, "pin", item => item.Pinned = pinned);

    public HeadlessNotificationSnapshot MarkAllRead()
    {
        lock (gate)
        {
            var items = Load(); foreach (var item in items) item.Read = true; Save(items);
            activity.Record("Information", "Notifications", "Marked all notifications read", $"count={items.Count}");
            return GetSnapshotUnsafe();
        }
    }

    public HeadlessNotificationSnapshot Dismiss(string id)
    {
        lock (gate)
        {
            var items = Load(); var removed = items.RemoveAll(x => x.Id.Equals(id, StringComparison.Ordinal));
            if (removed == 0) throw new KeyNotFoundException("Notification was not found.");
            Save(items); activity.Record("Information", "Notifications", "Dismissed notification", $"id={id}");
            return GetSnapshotUnsafe();
        }
    }

    private HeadlessNotificationSnapshot Mutate(string id, string action, Action<HeadlessNotificationItem> mutation)
    {
        lock (gate)
        {
            var items = Load(); var item = items.SingleOrDefault(x => x.Id.Equals(id, StringComparison.Ordinal))
                ?? throw new KeyNotFoundException("Notification was not found.");
            mutation(item); Save(items);
            activity.Record("Information", "Notifications", $"Updated notification {action}", $"id={id}");
            return GetSnapshotUnsafe();
        }
    }

    private HeadlessNotificationSnapshot GetSnapshotUnsafe()
    {
        var items = Load().OrderByDescending(x => x.Pinned).ThenByDescending(x => x.CreatedUtc).ToList();
        return new(items, items.Count(x => !x.Read), items.Count(x => x.Pinned), DateTimeOffset.UtcNow,
            $"{items.Count} notification(s), {items.Count(x => !x.Read)} unread.");
    }

    private List<HeadlessNotificationItem> Load()
    {
        try { return File.Exists(statePath) ? JsonSerializer.Deserialize<List<HeadlessNotificationItem>>(File.ReadAllText(statePath)) ?? [] : []; }
        catch (JsonException) { return []; }
        catch (IOException) { return []; }
    }

    private void Save(List<HeadlessNotificationItem> items)
    {
        var retained = items.OrderByDescending(x => x.Pinned).ThenByDescending(x => x.CreatedUtc).Take(MaximumNotifications).ToList();
        var partial = statePath + ".tmp";
        File.WriteAllText(partial, JsonSerializer.Serialize(retained, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(partial, statePath, true);
    }

    private static HeadlessNotificationItem New(string severity, string title, string message, bool pinned) => new()
    { Id = Guid.NewGuid().ToString("N"), Severity = severity, Title = title, Message = message, Pinned = pinned, CreatedUtc = DateTimeOffset.UtcNow };
}

public sealed class HeadlessNotificationItem
{
    public string Id { get; set; } = string.Empty;
    public string Severity { get; set; } = "Information";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Read { get; set; }
    public bool Pinned { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
public sealed record HeadlessNotificationSnapshot(IReadOnlyList<HeadlessNotificationItem> Items, int UnreadCount, int PinnedCount, DateTimeOffset ObservedAt, string Detail);
public sealed record HeadlessNotificationFlagRequest(bool Value);
