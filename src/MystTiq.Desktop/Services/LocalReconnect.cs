// MystTiq v1.1.5.0: file reviewed for this release (2026-10-24).
using System.Net.Http;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v1.1.5.0 (seen 2026-10-23, after an install): while the local tab was not connected, the 5-second tick only looked for
/// PalServer and never tried the management API again, so a helper that answered after the start-up wait left the desktop
/// on "Service unreachable" until Refresh was clicked. Every other tick this asks /healthz quietly (no busy state, no
/// status text); the window connects only once it answers.
/// </summary>
public sealed class LocalReconnect
{
    public const int EveryTicks = 2;
    private int ticks;
    private bool probing;

    public async Task<bool> AnswersAsync(string? endpoint)
    {
        if (probing || string.IsNullOrWhiteSpace(endpoint) || ++ticks % EveryTicks != 0) return false;
        probing = true;
        try
        {
            using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = static (_, _, _, _) => true };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
            using var response = await client.GetAsync(endpoint.TrimEnd('/') + "/healthz");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
        finally { probing = false; }
    }
}
