namespace MystTiq.Desktop.Models;

public sealed class NotificationItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Read { get; set; }
    public bool Pinned { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public string StateText => $"{(Pinned ? "PINNED · " : string.Empty)}{(Read ? "READ" : "UNREAD")} · {CreatedUtc.ToLocalTime():g}";
}

public sealed class NotificationSnapshotDto
{
    public List<NotificationItemDto> Items { get; set; } = [];
    public int UnreadCount { get; set; }
    public int PinnedCount { get; set; }
    public DateTimeOffset ObservedAt { get; set; }
    public string Detail { get; set; } = string.Empty;
}
public sealed record NotificationFlagRequestDto(bool Value);
