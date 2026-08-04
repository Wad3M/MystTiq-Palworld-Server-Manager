using System.Collections.Concurrent;

namespace PalworldManager.Services.Infrastructure;

public enum NotificationLevel
{
    Success,
    Information,
    Warning,
    Error
}

public sealed record InfrastructureNotification(
    DateTime TimestampUtc,
    NotificationLevel Level,
    string Category,
    string Title,
    string Message,
    int? PageIndex = null);

public sealed class NotificationService
{
    private readonly ConcurrentQueue<InfrastructureNotification> history = new();
    private readonly int capacity;

    public NotificationService(int capacity = 250)
    {
        if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
        this.capacity = capacity;
    }

    public event Action<InfrastructureNotification>? Published;

    public IReadOnlyList<InfrastructureNotification> Snapshot() => history.ToArray();

    public void Publish(
        NotificationLevel level,
        string message,
        string category = "System",
        string? title = null,
        int? pageIndex = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        var notification = new InfrastructureNotification(
            DateTime.UtcNow,
            level,
            string.IsNullOrWhiteSpace(category) ? "System" : category.Trim(),
            string.IsNullOrWhiteSpace(title) ? DefaultTitle(level) : title.Trim(),
            message.Trim(),
            pageIndex);

        history.Enqueue(notification);
        while (history.Count > capacity && history.TryDequeue(out _)) { }
        Published?.Invoke(notification);
    }

    private static string DefaultTitle(NotificationLevel level) => level switch
    {
        NotificationLevel.Success => "Operation completed",
        NotificationLevel.Warning => "Attention required",
        NotificationLevel.Error => "Operation failed",
        _ => "Information"
    };
}
