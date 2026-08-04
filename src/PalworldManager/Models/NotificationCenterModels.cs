namespace PalworldManager.Models;

public sealed class NotificationEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string Severity { get; set; } = "Information";
    public string Category { get; set; } = "System";
    public string Title { get; set; } = "Notification";
    public string Message { get; set; } = "";
    public int? PageIndex { get; set; }
    public bool IsRead { get; set; }
    public bool IsPinned { get; set; }

    public string TimestampDisplay => TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string ReadState => IsRead ? "Read" : "Unread";
    public string PinState => IsPinned ? "Pinned" : "";
    public string SeverityDisplay => Severity switch
    {
        "Critical" => "Critical",
        "Warning" => "Warning",
        "Success" => "Success",
        _ => "Information"
    };
}
