// MystTiq v1.1.1.2: file reviewed for this release (2026-10-19).
namespace MystTiq.Desktop.Models;

// v1.1.0.2 (roadmap F-2): GET /players/ids, PUT /players/ids/settings, POST /players/ids/move. The join check's record,
// each account's learned player ID and the characters waiting to be moved to the ID the game now gives the account.
public sealed class PlayerIdCheckDto
{
    public DateTimeOffset At { get; set; }
    public string Name { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Assigned { get; set; } = string.Empty;
    public string Expected { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;

    // Shown as typed (player names and IDs are not translated).
    public string AtVerbatim => At.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);
    public string NameVerbatim => Name;
    public string IdsVerbatim => Short(Expected) == Short(Assigned) ? Short(Assigned) : $"{Short(Expected)} → {Short(Assigned)}";
    public string Outcome => Result switch
    {
        "Correct" => "Correct ID",
        "FirstSeen" => "First seen",
        "NewIdFromGame" => "New ID from the game",
        "Moved" => "Moved to the new ID",
        _ => Result
    };

    internal static string Short(string id) => id.Length >= 8 ? id[..8] : id;
}

public sealed class PlayerIdMoveRequestDto
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FromPlayerId { get; set; } = string.Empty;
    public string ToPlayerId { get; set; } = string.Empty;
    public DateTimeOffset Since { get; set; }

    public string NameVerbatim => Name.Length > 0 && !Name.Equals(UserId, StringComparison.OrdinalIgnoreCase) ? $"{Name} · {UserId}" : UserId;
    public string IdsVerbatim => $"{PlayerIdCheckDto.Short(FromPlayerId)} → {PlayerIdCheckDto.Short(ToPlayerId)}";
    public string SinceVerbatim => Since.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);
}

public sealed class PlayerIdLedgerDto
{
    public bool AutoMove { get; set; } = true;
    public Dictionary<string, string> Known { get; set; } = [];
    public List<PlayerIdCheckDto> Checks { get; set; } = [];
    public List<PlayerIdMoveRequestDto> Pending { get; set; } = [];
}

public sealed class PlayerIdMoveResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
