using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using MystTiq.Core.Models;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

// v0.6.4.0 "Troubleshooting & Diagnostics Platform": local-PC diagnostics, entirely client-side --
// the point is diagnosing why the configured server can't be reached, so this must work even when
// the server is fully unreachable, unlike everything else in this app which is a thin API client.
// Produces the same MystTiq.Core.Models.DiagnosticFinding shape Doctor/Environment findings use
// (via DiagnosticFindingDto on the Desktop side) so all three categories render through one shared
// UI template. Today's alternative -- MainWindowViewModel.RefreshAsync's catch block showing a raw
// .NET exception Message for any connection failure -- gives no distinction between DNS failure,
// TCP refused, TLS failure, or timeout; this replaces that with a real staged diagnosis.
public sealed class LocalDiagnosticsService
{
    private static readonly TimeSpan StageTimeout = TimeSpan.FromSeconds(5);

    public async Task<IReadOnlyList<DiagnosticFindingDto>> DiagnoseConnectionAsync(ConnectionProfile profile, CancellationToken cancellationToken)
    {
        var findings = new List<DiagnosticFindingDto>();
        var host = profile.BaseAddress.Host;
        var port = profile.BaseAddress.IsDefaultPort
            ? (profile.BaseAddress.Scheme == Uri.UriSchemeHttps ? 443 : 80)
            : profile.BaseAddress.Port;

        // Stage 1: DNS resolution.
        IPAddress[] addresses;
        var dnsStart = DateTimeOffset.UtcNow;
        try
        {
            using var dnsTimeout = new CancellationTokenSource(StageTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, dnsTimeout.Token);
            addresses = await Dns.GetHostAddressesAsync(host, linked.Token);
            if (addresses.Length == 0) throw new SocketException((int)SocketError.HostNotFound);

            findings.Add(Finding("local-dns", DiagnosticState.Pass, "DNS Resolution", host,
                $"Resolved to {string.Join(", ", addresses.Select(a => a.ToString()))}.", dnsStart));
        }
        catch (Exception ex)
        {
            findings.Add(Finding("local-dns", DiagnosticState.Fail, "DNS Resolution", host,
                $"Could not resolve '{host}': {DescribeDnsFailure(ex)}.",
                dnsStart, "Confirm the hostname is correct and this machine has working DNS/internet access."));
            return findings;
        }

        // Stage 2: TCP connect.
        var tcpStart = DateTimeOffset.UtcNow;
        try
        {
            using var client = new TcpClient();
            using var tcpTimeout = new CancellationTokenSource(StageTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, tcpTimeout.Token);
            await client.ConnectAsync(host, port, linked.Token);
            findings.Add(Finding("local-tcp", DiagnosticState.Pass, "TCP Connect", $"{host}:{port}",
                "Connection established.", tcpStart));
        }
        catch (Exception ex)
        {
            findings.Add(Finding("local-tcp", DiagnosticState.Fail, "TCP Connect", $"{host}:{port}",
                DescribeTcpFailure(ex, host, port), tcpStart,
                DescribeTcpRecommendation(ex)));
            return findings;
        }

        // Stage 3: TLS handshake (+ pin check), only for https.
        if (profile.BaseAddress.Scheme == Uri.UriSchemeHttps)
        {
            var tlsStart = DateTimeOffset.UtcNow;
            try
            {
                using var client = new TcpClient();
                using var connectTimeout = new CancellationTokenSource(StageTimeout);
                using var connectLinked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectTimeout.Token);
                await client.ConnectAsync(host, port, connectLinked.Token);

                X509Certificate2? presented = null;
                using var ssl = new SslStream(client.GetStream(), leaveInnerStreamOpen: false,
                    (_, certificate, _, _) =>
                    {
                        if (certificate is X509Certificate2 cert2) presented = cert2;
                        else if (certificate is not null) presented = new X509Certificate2(certificate);
                        return true; // Always accept here -- we report trust/pin results as findings, not by failing the handshake.
                    });

                using var tlsTimeout = new CancellationTokenSource(StageTimeout);
                using var tlsLinked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, tlsTimeout.Token);
                await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host }, tlsLinked.Token);

                var pin = MystTiqApiClient.NormalizeFingerprint(profile.ServerCertificateSha256);
                if (!string.IsNullOrWhiteSpace(pin) && presented is not null)
                {
                    var matches = MystTiqApiClient.VerifyCertificatePin(presented, pin);
                    findings.Add(matches
                        ? Finding("local-tls", DiagnosticState.Pass, "TLS Handshake", host, "Handshake succeeded and the presented certificate matches the configured pin.", tlsStart)
                        : Finding("local-tls", DiagnosticState.Fail, "TLS Handshake", host,
                            $"Handshake succeeded, but the presented certificate ({presented.GetCertHashString()}) does not match the configured pin.",
                            tlsStart, "The server's certificate changed. Verify this is expected, then update the pinned fingerprint in the connection profile."));
                }
                else
                {
                    findings.Add(Finding("local-tls", DiagnosticState.Pass, "TLS Handshake", host,
                        $"Handshake succeeded ({ssl.SslProtocol}). No certificate pin configured -- OS trust-chain validation applies at real connect time.", tlsStart));
                }
            }
            catch (AuthenticationException ex)
            {
                findings.Add(Finding("local-tls", DiagnosticState.Fail, "TLS Handshake", host,
                    $"TLS handshake failed: {ex.Message}", tlsStart,
                    "The server's certificate may be untrusted, expired, or use an unsupported protocol version."));
                return findings;
            }
            catch (Exception ex)
            {
                findings.Add(Finding("local-tls", DiagnosticState.Fail, "TLS Handshake", host, ex.Message, tlsStart));
                return findings;
            }
        }

        // Stage 4: HTTP /healthz.
        var httpStart = DateTimeOffset.UtcNow;
        try
        {
            using var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = static (_, _, _, _) => true };
            using var http = new HttpClient(handler) { Timeout = StageTimeout };
            using var response = await http.GetAsync(new Uri(profile.BaseAddress, "/healthz"), cancellationToken);
            findings.Add(response.IsSuccessStatusCode
                ? Finding("local-http", DiagnosticState.Pass, "HTTP Health Check", "/healthz", $"HTTP {(int)response.StatusCode}.", httpStart)
                : Finding("local-http", DiagnosticState.Warning, "HTTP Health Check", "/healthz",
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}.", httpStart, "The server answered but did not report success -- check its own logs."));
        }
        catch (Exception ex)
        {
            findings.Add(Finding("local-http", DiagnosticState.Fail, "HTTP Health Check", "/healthz", ex.Message, httpStart));
        }

        return findings;
    }

    public IReadOnlyList<DiagnosticFindingDto> GetLocalMachineFindings()
    {
        var now = DateTimeOffset.UtcNow;
        var findings = new List<DiagnosticFindingDto>
        {
            Finding("local-runtime", DiagnosticState.Pass, ".NET Runtime", "This machine",
                System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, now)
        };

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var root = Path.GetPathRoot(localAppData);
            var drive = new DriveInfo(string.IsNullOrWhiteSpace(root) ? "C:\\" : root);
            var freeGiB = drive.AvailableFreeSpace / 1024d / 1024d / 1024d;
            // Same ≥5 GiB PASS / ≥2 GiB WARNING / else FAIL thresholds HeadlessDoctorService already
            // uses for the server's own disk-space check, for consistency across the unified model.
            var state = freeGiB >= 5 ? DiagnosticState.Pass : freeGiB >= 2 ? DiagnosticState.Warning : DiagnosticState.Fail;
            findings.Add(Finding("local-disk", state, "Local Disk Space", drive.Name, $"{freeGiB:F1} GiB free on {drive.Name}", now,
                state == DiagnosticState.Pass ? "" : "Free disk space on this machine before relying on local logs/exports."));
        }
        catch (Exception ex)
        {
            findings.Add(Finding("local-disk", DiagnosticState.Unknown, "Local Disk Space", "This machine", ex.Message, now));
        }

        return findings;
    }

    private static DiagnosticFindingDto Finding(string id, DiagnosticState state, string component, string location, string evidence, DateTimeOffset started, string recommendation = "") => new()
    {
        Id = id,
        Category = "Local Machine",
        Component = component,
        State = (int)state,
        Location = location,
        Evidence = evidence,
        Recommendation = recommendation,
        ActionKind = null,
        ActionSupported = false,
        ObservedAt = DateTimeOffset.UtcNow
    };

    private static string DescribeDnsFailure(Exception ex) => ex switch
    {
        SocketException { SocketErrorCode: SocketError.HostNotFound } => "host not found",
        SocketException se => $"{se.SocketErrorCode}",
        OperationCanceledException => "timed out",
        _ => ex.Message
    };

    private static string DescribeTcpFailure(Exception ex, string host, int port) => ex switch
    {
        SocketException { SocketErrorCode: SocketError.ConnectionRefused } => $"Connection to {host}:{port} was actively refused -- nothing is listening on that port, or a firewall rejected it.",
        SocketException { SocketErrorCode: SocketError.TimedOut } => $"Connection to {host}:{port} timed out -- the host may be unreachable, or a firewall is silently dropping the packets.",
        SocketException { SocketErrorCode: SocketError.HostUnreachable or SocketError.NetworkUnreachable } => $"{host} is unreachable from this network.",
        SocketException se => $"Connection to {host}:{port} failed: {se.SocketErrorCode}.",
        OperationCanceledException => $"Connection to {host}:{port} timed out.",
        _ => ex.Message
    };

    private static string DescribeTcpRecommendation(Exception ex) => ex switch
    {
        SocketException { SocketErrorCode: SocketError.ConnectionRefused } => "Confirm the server is running and listening on the configured port, and that no firewall on the server is blocking it.",
        SocketException { SocketErrorCode: SocketError.TimedOut } => "Check this machine's own outbound firewall/network, and any firewall between here and the server.",
        _ => "Verify the host/port in the connection profile and network connectivity between this machine and the server."
    };
}
