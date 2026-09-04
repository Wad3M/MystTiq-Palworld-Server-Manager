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
