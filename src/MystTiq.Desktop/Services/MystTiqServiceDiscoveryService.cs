using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace MystTiq.Desktop.Services;

public sealed record DiscoveredMystTiqService(Uri BaseAddress, string Address, bool UsesTls, bool AuthenticationEnabled)
{
    public string DisplayName => $"MystTiq @ {BaseAddress}";
}

public interface IMystTiqServiceDiscoveryService
{
    Task<IReadOnlyList<DiscoveredMystTiqService>> DiscoverAsync(
        int port = 8213,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Discovers MystTiq management services on directly-connected IPv4 LANs by probing only
/// the unauthenticated /healthz identity endpoint. TLS certificate validation is relaxed
/// only for this identity probe; normal API connections retain OS validation/pinning.
/// </summary>
public sealed class MystTiqServiceDiscoveryService : IMystTiqServiceDiscoveryService
{
    private const int MaxCandidatesPerInterface = 254;
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(700);

    public async Task<IReadOnlyList<DiscoveredMystTiqService>> DiscoverAsync(
        int port = 8213,
        CancellationToken cancellationToken = default)
    {
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        var candidates = GetCandidateAddresses();
        var results = new ConcurrentDictionary<string, DiscoveredMystTiqService>(StringComparer.OrdinalIgnoreCase);
        using var gate = new SemaphoreSlim(32, 32);

        var tasks = candidates.Select(async address =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var discovered = await ProbeAsync(address, port, cancellationToken).ConfigureAwait(false);
                if (discovered is not null)
                    results.TryAdd(discovered.BaseAddress.AbsoluteUri, discovered);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.Values
            .OrderByDescending(item => IPAddress.IsLoopback(IPAddress.Parse(item.Address)))
            .ThenBy(item => item.Address, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(item => item.UsesTls)
            .ToArray();
    }

    private static IReadOnlyList<string> GetCandidateAddresses()
    {
        var addresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "127.0.0.1" };

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            foreach (var unicast in nic.GetIPProperties().UnicastAddresses)
            {
                var address = unicast.Address;
                if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                    continue;

                var bytes = address.GetAddressBytes();
                if (bytes[0] == 169 && bytes[1] == 254)
                    continue;

                addresses.Add(address.ToString());

                // Deliberately cap discovery to the host's local /24 neighborhood even when
                // an adapter has a broader mask. This avoids sweeping large enterprise/VPN ranges.
                for (var host = 1; host <= MaxCandidatesPerInterface; host++)
                    addresses.Add(new IPAddress([bytes[0], bytes[1], bytes[2], (byte)host]).ToString());
            }
        }

        return addresses.ToArray();
    }

    private static async Task<DiscoveredMystTiqService?> ProbeAsync(
        string address,
        int port,
        CancellationToken cancellationToken)
    {
        // Prefer the secure endpoint when both happen to exist.
        foreach (var scheme in new[] { Uri.UriSchemeHttps, Uri.UriSchemeHttp })
        {
            var baseAddress = new Uri($"{scheme}://{address}:{port}");
            var health = await ReadMystTiqHealthEndpointAsync(baseAddress, cancellationToken).ConfigureAwait(false);
            if (health is not null)
                return new DiscoveredMystTiqService(baseAddress, address, scheme == Uri.UriSchemeHttps, health.Value);
        }

        return null;
    }

    private static async Task<bool?> ReadMystTiqHealthEndpointAsync(Uri baseAddress, CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler();
        if (baseAddress.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            // Discovery reads only public identity metadata. The real client never inherits this callback.
            handler.ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;
        }

        using var client = new HttpClient(handler) { BaseAddress = baseAddress, Timeout = ProbeTimeout };
        try
        {
            using var response = await client.GetAsync("/healthz", cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("component", out var component) ||
                !string.Equals(component.GetString(), "mysttiq-headless", StringComparison.OrdinalIgnoreCase))
                return null;

            var authenticationEnabled = document.RootElement.TryGetProperty("authentication", out var authentication) && authentication.GetBoolean();
            return authenticationEnabled;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return null;
        }
    }
}
