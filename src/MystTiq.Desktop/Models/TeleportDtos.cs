namespace MystTiq.Desktop.Models;

// v0.7.113.0: mirrors of HeadlessTeleportService's records (Teleport Points on the Map page).
public sealed class TeleportPointDto
{
    public string Name { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double? Z { get; set; }
}

public sealed class TeleportConfigDto
{
    public bool Enabled { get; set; }
    public string CommandPrefix { get; set; } = "!tp";
    public int CooldownSeconds { get; set; } = 60;
    public List<TeleportPointDto> Points { get; set; } = [];
}

public sealed class TeleportUseDto
{
    public DateTimeOffset AtUtc { get; set; }
    public string PlayerName { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Point { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string ListText => $"{AtUtc.ToLocalTime():HH:mm:ss}  {PlayerName} → {Point}  {(Success ? "✓" : "✗")} {Detail}";
}

public sealed class TeleportSnapshotDto
{
    public TeleportConfigDto Config { get; set; } = new();
    public KitProviderStatusDto Provider { get; set; } = new();
    public string ChatSource { get; set; } = string.Empty;
    public List<TeleportUseDto> RecentUses { get; set; } = [];
}

public sealed class TeleportSaveResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public TeleportConfigDto Config { get; set; } = new();
    public List<string> Errors { get; set; } = [];
}

public sealed class TeleportActionResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
}

public sealed class TeleportCaptureResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Z { get; set; }
    public string Response { get; set; } = string.Empty;
}
