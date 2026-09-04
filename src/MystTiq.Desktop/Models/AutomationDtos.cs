namespace MystTiq.Desktop.Models;

public sealed class AutomationTriggerDto
{
    public string Kind { get; set; } = "DailyTime";
    public TimeOnly? TimeOfDayUtc { get; set; }
    public string DaysOfWeek { get; set; } = "All";
    public TimeSpan? Interval { get; set; }
    public int? IdleThresholdMinutes { get; set; }
    public int JitterSeconds { get; set; }
}

public sealed class AutomationConditionDto
{
    public bool RequireServerRunning { get; set; }
    public bool RequireServerStopped { get; set; }
}

public sealed class AutomationActionDto
{
    public string Kind { get; set; } = "CreateBackup";
    public string? NotificationSeverity { get; set; }
    public string? NotificationTitle { get; set; }
    public string? NotificationMessage { get; set; }
    public string? NotificationTemplateId { get; set; }
    public string? RconCommand { get; set; }
    public List<int>? WarningCountdownSecondsBeforeAction { get; set; }
    public string? WarningMessageTemplate { get; set; }
}

public sealed class AutomationRuleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public AutomationTriggerDto Trigger { get; set; } = new();
    public AutomationConditionDto Condition { get; set; } = new();
    public AutomationActionDto Action { get; set; } = new();
    public int MissedGraceSeconds { get; set; } = 300;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? NextDueUtc { get; set; }
    public DateTimeOffset? LastRunUtc { get; set; }
    public DateTimeOffset? IdleSinceUtc { get; set; }
    public string ScheduleText => Trigger.Kind switch
    {
        "Interval" => $"Every {Trigger.Interval}",
        "IdleEmpty" => $"When empty for {Trigger.IdleThresholdMinutes ?? 30} minute(s)",
        _ => $"Daily at {Trigger.TimeOfDayUtc:hh\\:mm} UTC ({Trigger.DaysOfWeek})"
    };
}

public sealed record AutomationRuleRequestDto(string Name, AutomationTriggerDto Trigger, AutomationConditionDto Condition, AutomationActionDto Action);
public sealed record AutomationEnabledRequestDto(bool Enabled);

public sealed class AutomationRunRecordDto
{
    public string Id { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTimeOffset DueUtc { get; set; }
    public DateTimeOffset? StartedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public string Detail { get; set; } = string.Empty;
    public string? OperationId { get; set; }
}
