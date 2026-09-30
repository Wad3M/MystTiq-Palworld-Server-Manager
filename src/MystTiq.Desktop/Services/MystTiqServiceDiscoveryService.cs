// MystTiq v0.9.9.0: file reviewed for this release (2026-09-29).
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace MystTiq.Desktop.Services;

public sealed record DiscoveredMystTiqService(Uri BaseAddress, string Address, bool UsesTls, bool AuthenticationEnabled)
{
    public string DisplayName => $"MystTiq @ {BaseAddress}";
}

/// <summary>v0.9.6.0: one address range the search covers: "192.168.1.0/24" on the adapter "Ethernet".</summary>
public sealed record DiscoveryRange(string Cidr, string Adapter, IReadOnlyList<string> Addresses, bool Virtual = false)
{
    public string DisplayText => string.IsNullOrWhiteSpace(Adapter) ? Cidr : $"{Cidr} ({Adapter})";
}

/// <summary>
/// v0.9.6.0: which ranges to search. Virtual adapters (WSL, Hyper-V, Docker, VMware, VirtualBox) are left out unless
/// asked for; ExtraRanges adds ranges the computer is not directly on ("10.0.5.0/24", "10.0.5.7"), separated by commas.
/// </summary>
public sealed record DiscoveryOptions(bool IncludeVirtualAdapters = false, string? ExtraRanges = null);

/// <summary>v0.9.6.0: the ranges a search covers, the virtual ones it leaves out, and extra entries it could not read.</summary>
public sealed record DiscoveryPlan(IReadOnlyList<DiscoveryRange> Ranges, IReadOnlyList<DiscoveryRange> Skipped, IReadOnlyList<string> Problems);

/// <summary>v0.9.6.0: how far a search is: its plan, addresses checked out of all, and a service just found (or null).</summary>
public sealed record DiscoveryProgress(DiscoveryPlan Plan, int Checked, int Total, DiscoveredMystTiqService? Found, TimeSpan Elapsed)
{
    public IReadOnlyList<DiscoveryRange> Ranges => Plan.Ranges;
}

public interface IMystTiqServiceDiscoveryService
{
    Task<IReadOnlyList<DiscoveredMystTiqService>> DiscoverAsync(
        int port = 8213,
        DiscoveryOptions? options = null,
        IProgress<DiscoveryProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Discovers MystTiq management services on directly-connected IPv4 LANs by probing only
/// the unauthenticated /healthz identity endpoint. TLS certificate validation is relaxed
/// only for this identity probe; normal API connections retain OS validation/pinning.
///
/// v0.9.6.0: two passes instead of one. The first only opens a TCP connection to the port, 256 addresses at a time with a
/// short timeout: a computer without MystTiq refuses at once, and an address with nothing on it stops at the timeout. Only
/// the addresses that accept get the slower HTTPS/HTTP identity probe. Searching three adapters' /24 ranges took about
/// half a minute (every address waited for two 700 ms web requests, 32 at a time); it now takes a couple of seconds.
/// Progress reports the ranges searched, the count checked, and each service the moment it answers.
/// </summary>
public sealed class MystTiqServiceDiscoveryService : IMystTiqServiceDiscoveryService
{
    private const int Parallelism = 256;
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(2500);

    public Task<IReadOnlyList<DiscoveredMystTiqService>> DiscoverAsync(
        int port = 8213,
        DiscoveryOptions? options = null,
        IProgress<DiscoveryProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        ScanAsync(GetPlan(options ?? new DiscoveryOptions()), port, progress, cancellationToken);

    /// <summary>Searches the given ranges (the logic harness passes its own instead of this computer's networks).</summary>
    public static Task<IReadOnlyList<DiscoveredMystTiqService>> ScanAsync(
        IReadOnlyList<DiscoveryRange> ranges,
        int port,
        IProgress<DiscoveryProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        ScanAsync(new DiscoveryPlan(ranges, [], []), port, progress, cancellationToken);

    public static async Task<IReadOnlyList<DiscoveredMystTiqService>> ScanAsync(
        DiscoveryPlan plan,
        int port,
        IProgress<DiscoveryProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var ranges = plan.Ranges;
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port));

        var clock = Stopwatch.StartNew();
        var candidates = Candidates(ranges);
        var results = new ConcurrentDictionary<string, DiscoveredMystTiqService>(StringComparer.OrdinalIgnoreCase);
        var checkedCount = 0;
        progress?.Report(new DiscoveryProgress(plan, 0, candidates.Count, null, clock.Elapsed));

        await Parallel.ForEachAsync(candidates, new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = cancellationToken }, async (address, token) =>
        {
            DiscoveredMystTiqService? found = null;
            if (await AcceptsAsync(address, port, token).ConfigureAwait(false))
            {
                found = await ProbeAsync(address, port, token).ConfigureAwait(false);
                if (found is not null && !results.TryAdd(found.BaseAddress.AbsoluteUri, found))
                    found = null;
            }

            var done = Interlocked.Increment(ref checkedCount);
            if (found is not null || done % 16 == 0 || done == candidates.Count)
                progress?.Report(new DiscoveryProgress(plan, done, candidates.Count, found, clock.Elapsed));
        }).ConfigureAwait(false);

        return Order(results.Values);
    }

    public static IReadOnlyList<DiscoveredMystTiqService> Order(IEnumerable<DiscoveredMystTiqService> services) => services
        .OrderByDescending(item => IPAddress.IsLoopback(IPAddress.Parse(item.Address)))
        .ThenBy(item => item.Address, StringComparer.OrdinalIgnoreCase)
        .ThenByDescending(item => item.UsesTls)
        .ToArray();

    /// <summary>
    /// This computer (127.0.0.1) and the subnet of each IPv4 address of every adapter that is up (at most a /22), each range
    /// once, plus the extra ranges asked for.
    /// </summary>
    public static DiscoveryPlan GetPlan(DiscoveryOptions options)
    {
        var adapters = new List<(string Name, string Description, IPAddress Address, bool HasGateway, int PrefixLength)>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;
            var properties = nic.GetIPProperties();
            var hasGateway = properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
            foreach (var unicast in properties.UnicastAddresses)
                adapters.Add((nic.Name, nic.Description, unicast.Address, hasGateway, unicast.PrefixLength));
        }

        return BuildPlan(adapters, options);
    }

    /// <summary>The ranges for a list of adapter addresses with every adapter included (the logic harness's shorthand).</summary>
    public static IReadOnlyList<DiscoveryRange> BuildRanges(IEnumerable<(string Adapter, IPAddress Address)> adapters) =>
        BuildPlan(adapters.Select(a => (a.Adapter, string.Empty, a.Address, false, 24)), new DiscoveryOptions(IncludeVirtualAdapters: true)).Ranges;

    /// <summary>The plan for adapters whose subnet is not known (treated as /24).</summary>
    public static DiscoveryPlan BuildPlan(IEnumerable<(string Adapter, string Description, IPAddress Address, bool HasGateway)> adapters, DiscoveryOptions options) =>
        BuildPlan(adapters.Select(a => (a.Adapter, a.Description, a.Address, a.HasGateway, 24)), options);

    // v0.9.8.0: an adapter's own subnet is searched, but never wider than a /22 (1,022 addresses, a few seconds at 256 at a
    // time), so a large office or VPN network is still not swept end to end. Before, every adapter was searched as a /24.
    public const int WidestPrefix = 22;

    /// <summary>
    /// Loopback first, link-local and IPv6 skipped, each /24 once; virtual adapters apart unless included. An adapter is
    /// virtual only when its name says so and it has no default gateway: a Hyper-V external switch ("vEthernet (…
    /// External)") carries the computer's real network, with the router as its gateway, and was skipped by name alone on
    /// this machine, leaving only 127.0.0.1 to search. Real adapters claim their range first, so a physical card on the
    /// same /24 as a virtual one is never dropped as a duplicate of it.
    /// </summary>
    public static DiscoveryPlan BuildPlan(IEnumerable<(string Adapter, string Description, IPAddress Address, bool HasGateway, int PrefixLength)> adapters, DiscoveryOptions options)
    {
        var ranges = new List<DiscoveryRange> { new("127.0.0.1", "this computer", ["127.0.0.1"]) };
        var skipped = new List<DiscoveryRange>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = adapters
            .Select(a => (a.Adapter, a.Address, Virtual: IsVirtualAdapter(a.Adapter, a.Description) && !a.HasGateway, Prefix: Math.Clamp(a.PrefixLength is > 0 and <= 32 ? a.PrefixLength : 24, WidestPrefix, 30)))
            .OrderBy(a => a.Virtual)
            .ToArray();
        foreach (var (adapter, address, isVirtual, prefix) in ordered)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
                continue;
            var bytes = address.GetAddressBytes();
            if (bytes[0] == 169 && bytes[1] == 254)
                continue;
            var value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes);
            var network = value & (uint.MaxValue << (32 - prefix));
            var size = 1u << (32 - prefix);
            var cidr = $"{ToAddress(network)}/{prefix}";
            if (!seen.Add(cidr))
                continue;

            // The adapter's own address first, then every host (a subnet's network and broadcast addresses answer nothing).
            var hosts = new List<string>((int)size) { address.ToString() };
            for (var i = 1u; i < size - 1; i++)
            {
                var candidate = ToAddress(network + i);
                if (candidate != hosts[0]) hosts.Add(candidate);
            }

            var range = new DiscoveryRange(cidr, adapter, hosts, isVirtual);
            if (isVirtual && !options.IncludeVirtualAdapters) skipped.Add(range);
            else ranges.Add(range);
        }

        var problems = new List<string>();
        foreach (var extra in ParseExtraRanges(options.ExtraRanges, problems))
        {
            if (seen.Add(extra.Cidr)) ranges.Add(extra);
        }

        return new DiscoveryPlan(ranges, skipped, problems);
    }

    private static readonly string[] VirtualMarkers =
        ["vEthernet", "Hyper-V", "WSL", "Docker", "VMware", "VirtualBox", "Virtual Ethernet", "docker0", "virbr", "veth", "br-", "lxcbr", "lxdbr", "cni", "flannel"];

    /// <summary>
    /// Adapters for virtual machines and containers on this computer (WSL, Hyper-V, Docker, VMware, VirtualBox, libvirt):
    /// their range holds this computer's own guests, not other computers on the network.
    /// </summary>
    public static bool IsVirtualAdapter(string? name, string? description) =>
        VirtualMarkers.Any(marker =>
            (name?.StartsWith(marker, StringComparison.OrdinalIgnoreCase) ?? false)
            || (marker.Length > 4 && ((name?.Contains(marker, StringComparison.OrdinalIgnoreCase) ?? false) || (description?.Contains(marker, StringComparison.OrdinalIgnoreCase) ?? false))));

    /// <summary>
    /// Extra ranges typed by the user: single addresses ("10.0.5.7") and networks from /22 to /32 ("10.0.5.0/24"),
    /// separated by commas, semicolons or spaces. Anything else is named in <paramref name="problems"/>.
    /// </summary>
    public static IReadOnlyList<DiscoveryRange> ParseExtraRanges(string? text, List<string> problems)
    {
        var ranges = new List<DiscoveryRange>();
        if (string.IsNullOrWhiteSpace(text)) return ranges;
        foreach (var entry in text.Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            var slash = entry.IndexOf('/');
            var addressText = slash < 0 ? entry : entry[..slash];
            var prefix = 32;
            if (!IPAddress.TryParse(addressText, out var address) || address.AddressFamily != AddressFamily.InterNetwork
                || (slash >= 0 && (!int.TryParse(entry[(slash + 1)..], out prefix) || prefix < WidestPrefix || prefix > 32)))
            {
                problems.Add(entry);
                continue;
            }

            var value = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(address.GetAddressBytes());
            var network = prefix == 32 ? value : value & (uint.MaxValue << (32 - prefix));
            var size = 1u << (32 - prefix);
            var hosts = new List<string>();
            for (var i = 0u; i < size; i++)
            {
                // A /24 to /30 has a network and a broadcast address that no computer answers on.
                if (prefix <= 30 && (i == 0 || i == size - 1)) continue;
                hosts.Add(ToAddress(network + i));
            }

            var cidr = prefix == 32 ? address.ToString() : $"{ToAddress(network)}/{prefix}";
            ranges.Add(new DiscoveryRange(cidr, "added", hosts));
        }

        return ranges;
    }
    private static string ToAddress(uint value)
    {
        var bytes = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return new IPAddress(bytes).ToString();
    }

    // Every address once, in range order (this computer, then each adapter's own address first).
    private static IReadOnlyList<string> Candidates(IReadOnlyList<DiscoveryRange> ranges)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return ranges.SelectMany(r => r.Addresses).Where(seen.Add).ToArray();
    }

    private static async Task<bool> AcceptsAsync(string address, int port, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConnectTimeout);
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(IPAddress.Parse(address), port, timeout.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
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
            if (ex is TaskCanceledException && cancellationToken.IsCancellationRequested) throw;
            return null;
        }
    }
}
