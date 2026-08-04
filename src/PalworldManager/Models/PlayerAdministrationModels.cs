namespace PalworldManager.Models;

public sealed class PlayerAdministrationRecord
{
    public string PlayerKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsAdmin { get; set; }
    public bool IsWhitelisted { get; set; }
    public bool IsPermanentlyBanned { get; set; }
    public DateTime? TemporaryBanUntilUtc { get; set; }
    public List<PlayerAdministrationNote> Notes { get; set; } = [];
    public List<PlayerWarningRecord> Warnings { get; set; } = [];
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PlayerAdministrationNote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string Administrator { get; set; } = Environment.UserName;
    public string Category { get; set; } = "General";
    public string Text { get; set; } = "";
}

public sealed class PlayerWarningRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime IssuedUtc { get; set; } = DateTime.UtcNow;
    public string IssuedBy { get; set; } = Environment.UserName;
    public string Reason { get; set; } = "";
    public DateTime? ExpiresUtc { get; set; }
    public DateTime? ClearedUtc { get; set; }
    public string ClearedBy { get; set; } = "";
    public bool IsActive => ClearedUtc is null && (ExpiresUtc is null || ExpiresUtc > DateTime.UtcNow);
}

public sealed record PlayerAdministrationSummary(
    bool IsAdmin,
    bool IsWhitelisted,
    bool IsBanned,
    DateTime? TemporaryBanUntilUtc,
    int NoteCount,
    int ActiveWarningCount,
    int TotalWarningCount);
