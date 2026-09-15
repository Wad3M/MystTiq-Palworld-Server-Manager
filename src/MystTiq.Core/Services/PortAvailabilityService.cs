using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

// v0.7.2.0: a thin, standalone wrapper over INetworkDiagnosticsPlatformService's already-working
// "list every bound TCP/UDP endpoint on this machine" capability (Windows via netstat, Linux via
// its own implementation) -- reused as-is, just answering an arbitrary candidate port instead of
// one already-configured profile's own port the way NetworkDiagnosticsService does.
public sealed class PortAvailabilityService
{
    private readonly INetworkDiagnosticsPlatformService platform;
    public PortAvailabilityService(INetworkDiagnosticsPlatformService platform) => this.platform = platform;

    public async Task<PortCheckResult> CheckAsync(int port, string protocol, CancellationToken token = default)
    {
        var endpoints = protocol.Equals("UDP", StringComparison.OrdinalIgnoreCase)
            ? await platform.GetUdpEndpointsAsync(token)
            : await platform.GetTcpListenersAsync(token);
        var match = endpoints.FirstOrDefault(e => e.LocalPort == port);
        return new PortCheckResult(port, protocol.ToUpperInvariant(), match is not null, match?.ProcessName, match?.OwningProcessId);
    }
}
