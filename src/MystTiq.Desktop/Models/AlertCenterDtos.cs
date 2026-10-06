// MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
namespace MystTiq.Desktop.Models;

public sealed class AlertThresholdRuleDto
{
    public bool Enabled { get; set; }
    public double ThresholdPercent { get; set; }
    public int CooldownMinutes { get; set; }
}

public sealed class AlertMemoryRuleDto
{
    public bool Enabled { get; set; }
    public double ThresholdMb { get; set; }
    public int CooldownMinutes { get; set; }
}

public sealed class AlertDiskDaysRuleDto
{
    public bool Enabled { get; set; }
    public double ThresholdDays { get; set; }
    public int CooldownMinutes { get; set; }
}

public sealed class AlertSimpleRuleDto
{
    public bool Enabled { get; set; }
    public int CooldownMinutes { get; set; }
}

public sealed class AlertRuleSetDto
{
    public AlertThresholdRuleDto HighCpuSustained { get; set; } = new();
    public AlertMemoryRuleDto HighMemorySustained { get; set; } = new();
    public AlertThresholdRuleDto LowDiskSpace { get; set; } = new();
    public AlertDiskDaysRuleDto DiskSpaceExhaustionPredicted { get; set; } = new();
    public AlertSimpleRuleDto ModHealthDegraded { get; set; } = new();
    // v0.9.8.0: the game server behind Steam's build, or PalDefender not updated for the game.
    public AlertSimpleRuleDto ComponentOutdated { get; set; } = new();
    // v0.7.108.0: how often a still-active condition's "Still active: ..." reminder repeats. 0 turns
    // reminders off entirely.
    public int ReminderMinutes { get; set; } = 1440;
    // v0.7.111.0: this profile's crash-recovery alert settings and its alert mute. Both must round-trip
    // here, because Save Rules sends the whole rule set back and anything missing would be reset.
    public CrashAlertRuleDto CrashAlerts { get; set; } = new();
    public DateTimeOffset? MutedUntilUtc { get; set; }

    public bool IsMuted => MutedUntilUtc is { } until && until > DateTimeOffset.UtcNow;
    public string MuteStatusText => IsMuted
        ? $"All alerts for this server are muted until {MutedUntilUtc!.Value.ToLocalTime():ddd d MMM, HH:mm}. Nothing is sent until then; anything still wrong alerts when the mute ends."
        : "Alerts are on.";
}

public sealed class CrashAlertRuleDto
{
    public bool Enabled { get; set; } = true;
    public bool RecoveryNotices { get; set; } = true;
}

public sealed class AlertMuteRequestDto
{
    public int Minutes { get; set; }
}

public sealed class DiskSpacePredictionDto
{
    public long FreeBytes { get; set; }
    public double GrowthBytesPerDay { get; set; }
    public DateTimeOffset? ProjectedFullUtc { get; set; }
    public double? DaysRemaining { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string FreeGb => $"{FreeBytes / 1024d / 1024d / 1024d:F1} GB free";
}

// v0.8.4.0: pausing outside delivery (Discord, email, webhooks); notifications still appear on the Notifications page.
// v1.0.2.0 (roadmap R-2): each outside channel's delivery health and the latest sends (NotificationDeliveryLog).
public sealed class NotificationChannelHealthDto
{
    public string Channel { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string State { get; set; } = string.Empty;
    public bool Flagged { get; set; }
    public string Detail { get; set; } = string.Empty;
    public DateTimeOffset? LastAttemptUtc { get; set; }
    public DateTimeOffset? LastSuccessUtc { get; set; }
    public string? LastError { get; set; }
    public int AttemptsInWindow { get; set; }
    public int SuccessesInWindow { get; set; }
    public string StateText => State switch { "NotProven" => "Not proven", _ => State };
}

public sealed class NotificationDeliveryRecordDto
{
    public DateTimeOffset AtUtc { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Detail { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public string WhenVerbatim => AtUtc.ToLocalTime().ToString("g");
    public string ResultText => Success ? "Delivered" : "Failed";
}

public sealed class NotificationDeliveryHealthDto
{
    public int WindowDays { get; set; } = 7;
    public List<NotificationChannelHealthDto> Channels { get; set; } = [];
    public List<NotificationDeliveryRecordDto> Recent { get; set; } = [];
    public bool AnyFlagged { get; set; }
    public string Summary { get; set; } = string.Empty;
    public bool HasRecent => Recent.Count > 0;
}

public sealed class NotificationDeliveryStateDto
{
    public DateTimeOffset? PausedUntilUtc { get; set; }
    public List<string> ExternalChannels { get; set; } = [];
    public string Detail { get; set; } = string.Empty;

    public bool IsPaused => PausedUntilUtc is { } until && until > DateTimeOffset.UtcNow;
    public string StatusText => IsPaused
        ? $"Paused until {PausedUntilUtc!.Value.ToLocalTime():ddd d MMM, HH:mm}. Everything still appears on the Notifications page; nothing is sent to {(ExternalChannels.Count == 0 ? "outside channels" : string.Join(", ", ExternalChannels))}, and notifications from the pause are not sent afterwards."
        : ExternalChannels.Count == 0
            ? "On. No outside channel (Discord, email, webhook) is switched on yet, so notifications only appear in MystTiq."
            : $"On: notifications are also sent to {string.Join(", ", ExternalChannels)}.";
}

public sealed class NotificationDeliveryPauseRequestDto
{
    public int Minutes { get; set; }
}

public sealed class NotificationTestResultDto
{
    public string Message { get; set; } = string.Empty;
    public NotificationDeliveryStateDto Delivery { get; set; } = new();
}
