namespace MystTiq.Desktop.Models;

public sealed class DiscordRoleMappingDto
{
    public string DiscordRoleId { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";
}

// Sent to PUT /notifications/discord-bot. BotToken left null/empty keeps whatever token is
// already stored server-side -- this form never receives back a previously saved token, so
// "save without retyping it" must never look like "clear it."
public sealed class DiscordBotConfigurationDto
{
    public bool Enabled { get; set; }
    public string? BotToken { get; set; }
    public string? GuildId { get; set; }
    public string? OwnerDiscordUserId { get; set; }
    public List<DiscordRoleMappingDto> RoleMappings { get; set; } = [];
}

// Returned from GET /notifications/discord-bot. Never carries the actual token, only whether one
// is configured.
public sealed class DiscordBotConfigurationViewDto
{
    public bool Enabled { get; set; }
    public bool TokenConfigured { get; set; }
    public string? GuildId { get; set; }
    public string? OwnerDiscordUserId { get; set; }
    public List<DiscordRoleMappingDto> RoleMappings { get; set; } = [];
    public string ConnectionState { get; set; } = "NotConfigured";
}
