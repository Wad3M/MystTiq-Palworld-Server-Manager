namespace MystTiq.Desktop.Models;

public sealed class TemporaryBanEntryDto
{
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }

    public string RemainingText
    {
        get
        {
            var remaining = ExpiresAtUtc - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero) return "Expiring…";
            return remaining.TotalDays >= 1 ? $"{remaining.TotalDays:F1} day(s) left"
                : remaining.TotalHours >= 1 ? $"{remaining.TotalHours:F1} hour(s) left"
                : $"{Math.Max(1, remaining.TotalMinutes):F0} minute(s) left";
        }
    }
}

// Returned from GET /players/temp-bans -- read-only from the Desktop's perspective; mutations go
// through POST /players/{id}/temp-ban (create) and the existing Unban action (early lift).
public sealed class TemporaryBanConfigDto
{
    public List<TemporaryBanEntryDto> Entries { get; set; } = [];
}
