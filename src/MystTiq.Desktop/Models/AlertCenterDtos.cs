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
