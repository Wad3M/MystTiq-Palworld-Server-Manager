// MystTiq v1.0.0.6: file reviewed for this release (2026-10-05).
namespace MystTiq.Desktop.Models;

// v1.0.0.2: unique player names, GET/PUT /players/name-guard. Like the whitelist, the whole list is read and replaced at
// once (reserve or release locally, one Save sends it).
public sealed class NameClaimDto
{
    public string Name { get; set; } = string.Empty;
    // The account that owns the name (steam_…); empty: nobody may use it.
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public bool Reserved { get; set; }
    public DateTimeOffset ClaimedAt { get; set; }

    // Shown as typed (player names and IDs are not translated).
    public string NameVerbatim => Name;
    public string OwnerVerbatim => OwnerId.Length == 0 ? "—" : OwnerName.Length > 0 && !OwnerName.Equals(OwnerId, StringComparison.OrdinalIgnoreCase) ? $"{OwnerName} · {OwnerId}" : OwnerId;
    public string Kind => OwnerId.Length == 0 ? "Blocked for everyone" : Reserved ? "Reserved" : "First to use it";
}

public sealed class NameGuardConfigDto
{
    public bool Enabled { get; set; } = true;
    public bool KickDuplicates { get; set; } = true;
    public List<NameClaimDto> Claims { get; set; } = [];
}

public sealed class NameGuardEventDto
{
    public DateTimeOffset At { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string PlayerId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public bool Kicked { get; set; }
    public string Message { get; set; } = string.Empty;

    public string AtVerbatim => At.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);
    public string NameVerbatim => Name;
    public string UserIdVerbatim => UserId;
    public string Outcome => Kicked ? "Kicked" : "Not kicked";
}

public sealed class NameGuardSnapshotDto
{
    public NameGuardConfigDto Config { get; set; } = new();
    public List<NameGuardEventDto> Events { get; set; } = [];
}
