namespace PalworldManager.Models;

public sealed record WorldClockSnapshot(
    bool Available,
    long GameDateTimeTicks,
    long DayNumber,
    TimeSpan TimeOfDay,
    DateTime SourceWriteUtc,
    string Source,
    string Detail)
{
    public string TimeDisplay => Available ? $"{(int)TimeOfDay.TotalHours:00}:{TimeOfDay.Minutes:00}" : "—";
    public string DayDisplay => Available ? $"Day {DayNumber:N0}" : "Day —";
}

public sealed record WorldTelemetryPlayer(string Key, string Name);

public sealed record WorldTelemetryEvent(
    DateTime TimestampUtc,
    string Kind,
    string Summary,
    string Detail);

public sealed record WorldTelemetrySnapshot
{
    public long SessionId { get; init; }
    public DateTime? SessionStartedAt { get; init; }
    public TimeSpan SessionUptime { get; init; }
    public WorldClockSnapshot WorldClock { get; init; } =
        new(false, 0, 0, TimeSpan.Zero, DateTime.MinValue, "Unavailable", "No authoritative world clock is available.");
    public DateTime? LastWorldSaveUtc { get; init; }
    public DateTime? LastBackupLocal { get; init; }
    public int OnlinePlayers { get; init; }
    public int PeakPlayers { get; init; }
    public int SessionJoins { get; init; }
    public int SessionLeaves { get; init; }
    public int UniquePlayers { get; init; }
    public string LastPlayerEvent { get; init; } = "No player transition observed";
    public DateTime CapturedUtc { get; init; } = DateTime.UtcNow;
}

public sealed record WorldTelemetryUpdate(
    WorldTelemetrySnapshot Snapshot,
    IReadOnlyList<WorldTelemetryEvent> Events);
