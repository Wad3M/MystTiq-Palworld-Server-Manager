// MystTiq v0.9.7.0: file reviewed for this release (2026-09-29).
namespace MystTiq.Desktop.Models;

public sealed class NotificationChannelConfigDto
{
    public string Channel { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? TargetUrl { get; set; }
}

public sealed class NotificationChannelConfigurationDto
{
    public List<NotificationChannelConfigDto> Channels { get; set; } = [];
}

public sealed class NotificationTemplateDto
{
    public string Id { get; set; } = string.Empty;
    public string TitleFormat { get; set; } = string.Empty;
    public string MessageFormat { get; set; } = string.Empty;
    public string DefaultSeverity { get; set; } = "Information";
}
