// MystTiq v1.0.2.0: file reviewed for this release (2026-10-05).
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.2.0 (roadmap R-1): asks the server's own REST API (GET /v1/api/info) whether it still answers. Any HTTP answer
/// counts, even a refusal: a frozen process gives none. With the REST API switched off, or without an admin password,
/// there is nothing to ask, which is never a reason to restart (CannotTell).
/// </summary>
public sealed class PalworldRestResponsivenessProbe : IServerResponsivenessProbe
{
    private readonly string settingsPath;
    private readonly TimeSpan timeout;

    public PalworldRestResponsivenessProbe(IServerPathProfile paths, TimeSpan? timeout = null)
        : this(Path.Combine(paths.ConfigRoot, "PalWorldSettings.ini"), timeout) { }

    public PalworldRestResponsivenessProbe(string settingsPath, TimeSpan? timeout = null)
    {
        this.settingsPath = settingsPath;
        this.timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    public async Task<ResponsivenessProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        string text;
        try { text = await File.ReadAllTextAsync(settingsPath, cancellationToken); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return ResponsivenessProbeResult.CannotTell; }
        if (HeadlessMonitoringService.ReadBooleanOption(text, "RESTAPIEnabled") is not true) return ResponsivenessProbeResult.CannotTell;
        var password = HeadlessMonitoringService.ReadQuotedOption(text, "AdminPassword");
        if (string.IsNullOrWhiteSpace(password)) return ResponsivenessProbeResult.CannotTell;
        var port = HeadlessMonitoringService.ReadIntegerOption(text, "RESTAPIPort") ?? 8212;

        using var handler = new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false, ConnectTimeout = timeout };
        using var client = new HttpClient(handler) { Timeout = timeout };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/v1/api/info") { Version = HttpVersion.Version11 };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"admin:{password}")));
        request.Headers.ConnectionClose = true;
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return ResponsivenessProbeResult.Answered;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return ResponsivenessProbeResult.NoAnswer;
        }
    }
}
