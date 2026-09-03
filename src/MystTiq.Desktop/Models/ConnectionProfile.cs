namespace MystTiq.Desktop.Models;

/// <summary>
/// Persistable connection metadata. Bearer tokens are deliberately NOT part of this model.
/// They remain process-memory-only in the desktop client.
/// </summary>
public sealed record ConnectionProfile(
    string Id,
    string Name,
    Uri BaseAddress,
    string? ServerCertificateSha256 = null)
{
    public static ConnectionProfile LocalDefault { get; } =
        new("local-default", "Local MystTiq", new Uri("http://127.0.0.1:8213"));

    public bool UsesHttps => BaseAddress.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
