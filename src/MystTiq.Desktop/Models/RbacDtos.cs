namespace MystTiq.Desktop.Models;

public sealed class MystTiqPrincipalDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresUtc { get; set; }
    public bool IsExpired { get; set; }
}

// v0.7.114.0: named user accounts.
public sealed class UserAccountDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? ScopedServerProfileId { get; set; }
    public bool Enabled { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? LastLoginUtc { get; set; }
    public bool LockedOut { get; set; }
    public string StatusText => !Enabled ? "disabled" : LockedOut ? "locked (failed sign-ins)" : "active";
    public string LastLoginText => LastLoginUtc is { } at ? $"last sign-in {at.ToLocalTime():g}" : "never signed in";
    public string ScopeText => string.IsNullOrWhiteSpace(ScopedServerProfileId) ? "all servers" : $"server {ScopedServerProfileId}";
}

public sealed class UserAccountResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserAccountDto? Account { get; set; }
}

public sealed class UserLoginResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    public DateTimeOffset? ExpiresUtc { get; set; }
    public MystTiqPrincipalDto? Principal { get; set; }
}

public sealed record HeadlessCreatePrincipalRequestDto(string Name, string Role, DateTimeOffset? ExpiresUtc);

public sealed class HeadlessCreatePrincipalResultDto
{
    public MystTiqPrincipalDto Principal { get; set; } = new();
    public string PlaintextToken { get; set; } = string.Empty;
}
