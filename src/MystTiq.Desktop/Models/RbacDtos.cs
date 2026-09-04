namespace MystTiq.Desktop.Models;

public sealed class MystTiqPrincipalDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresUtc { get; set; }
    public bool IsExpired { get; set; }
}

public sealed record HeadlessCreatePrincipalRequestDto(string Name, string Role, DateTimeOffset? ExpiresUtc);

public sealed class HeadlessCreatePrincipalResultDto
{
    public MystTiqPrincipalDto Principal { get; set; } = new();
    public string PlaintextToken { get; set; } = string.Empty;
}
