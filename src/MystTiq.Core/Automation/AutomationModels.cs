using System.Text.Json.Serialization;

namespace MystTiq.Core.Automation;

// Kind-agnostic scheduled-automation contracts. Originally time-based triggers only
// (DailyTime / Interval); v0.6.9.0 adds IdleEmpty, a continuously-observed state trigger
// (0 online players for N minutes) rather than a fixed schedule -- evaluated separately from
// the NextDueUtc polling path below, since "how long has it been idle" is live state, not a
// computable future timestamp. True event-based triggers (on-crash, on-join) still need
// provider event emission this app doesn't have yet.

[JsonConverter(typeof(AutomationRuleIdJsonConverter))]
public readonly record struct AutomationRuleId(string Value)
{
    public static AutomationRuleId New() => new(Guid.NewGuid().ToString("N"));
    public override string ToString() => Value;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AutomationTriggerKind { DailyTime, Interval, IdleEmpty }

[Flags]
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AutomationDayOfWeekMask
{
    None = 0,
    Sunday = 1, Monday = 2, Tuesday = 4, Wednesday = 8, Thursday = 16, Friday = 32, Saturday = 64,
    All = Sunday | Monday | Tuesday | Wednesday | Thursday | Friday | Saturday
}

public sealed record AutomationTrigger
{
    public required AutomationTriggerKind Kind { get; init; }

    // DailyTime only.
    public TimeOnly? TimeOfDayUtc { get; init; }
    public AutomationDayOfWeekMask DaysOfWeek { get; init; } = AutomationDayOfWeekMask.All;

    // Interval only.
    public TimeSpan? Interval { get; init; }

    // IdleEmpty only: fire once the server has had 0 online players continuously for this long.
    public int? IdleThresholdMinutes { get; init; }

    public int JitterSeconds { get; init; }
}

public sealed record AutomationCondition
{
    public bool RequireServerRunning { get; init; }
    public bool RequireServerStopped { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AutomationActionKind { CreateBackup, StartServer, StopServer, RestartServer, SendNotification, SendRconCommand }

public sealed record AutomationAction
{
    public required AutomationActionKind Kind { get; init; }

    // SendNotification only. If NotificationTemplateId is set, the resolved template's
    // TitleFormat/MessageFormat/DefaultSeverity are used (with {ruleName}/{severity} placeholder
    // substitution), overriding the literal fields below.
    public string? NotificationSeverity { get; init; }
    public string? NotificationTitle { get; init; }
    public string? NotificationMessage { get; init; }
    public string? NotificationTemplateId { get; init; }

    // SendRconCommand only.
    public string? RconCommand { get; init; }

    // StopServer / RestartServer only. Each entry is seconds-before-action; broadcast via RCON
    // at each offset (substituting {seconds} into the template) before the lifecycle action runs.
    public IReadOnlyList<int>? WarningCountdownSecondsBeforeAction { get; init; }
    public string? WarningMessageTemplate { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AutomationRunState { Scheduled, Running, Completed, Failed, Missed, Skipped, Cancelled }

public sealed class AutomationRule
{
    public required AutomationRuleId Id { get; init; }
    public required string Name { get; set; }
    public bool Enabled { get; set; } = true;
    public required AutomationTrigger Trigger { get; set; }
    public AutomationCondition Condition { get; set; } = new();
    public required AutomationAction Action { get; set; }
    public int MissedGraceSeconds { get; set; } = 300;
    public DateTimeOffset CreatedUtc { get; init; }
    public DateTimeOffset? NextDueUtc { get; set; }
    public DateTimeOffset? LastRunUtc { get; set; }

    // IdleEmpty only: when the server was first observed with 0 online players, reset to null the
    // instant any player is seen online. Persisted so an idle streak survives a MystTiq restart.
    public DateTimeOffset? IdleSinceUtc { get; set; }
}

public sealed class AutomationRunRecord
{
    public required string Id { get; init; }
    public required AutomationRuleId RuleId { get; init; }
    public required string RuleName { get; init; }
    public AutomationRunState State { get; set; }
    public required DateTimeOffset DueUtc { get; init; }
    public DateTimeOffset? StartedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public string Detail { get; set; } = string.Empty;

    // Ties back to an Operations.OperationRecord when the action acquired a coordinator lock
    // (CreateBackup/StartServer/StopServer/RestartServer); null for SendNotification/SendRconCommand.
    public string? OperationId { get; set; }
}
