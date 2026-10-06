// MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.0.1: the addresses players use to reach this server, for the Dashboard (requested 2026-09-30). Local addresses are
/// read from the adapters each time. The public address is asked of the router (UPnP) first and, when the router does not
/// give a public one (none answers, or it is behind another NAT), of api.ipify.org; the answer is kept for an hour and
/// shared by every server on this machine (PRIVACY.md lists the lookup).
/// </summary>
public sealed class HeadlessAddressService(WanReachabilityService wan)
{
    public static readonly TimeSpan PublicRefresh = TimeSpan.FromHours(1);
    private static readonly SemaphoreSlim PublicGate = new(1, 1);
    private static PublicAddress? cachedPublic;

    public async Task<HostAddressSnapshot> GetAsync(int gamePort, bool refresh, CancellationToken token)
    {
        var current = cachedPublic;
        if (refresh || current is null)
            current = await RefreshPublicAsync(token);
        else if (DateTimeOffset.UtcNow - current.CheckedAt > PublicRefresh)
            _ = Task.Run(() => RefreshPublicAsync(CancellationToken.None));
        return new HostAddressSnapshot(gamePort, HostAddresses.Local(), current?.Address, current?.Source, current?.CheckedAt, current?.Error);
    }

    private async Task<PublicAddress> RefreshPublicAsync(CancellationToken token)
    {
        await PublicGate.WaitAsync(token);
        try
        {
            // Another caller may have refreshed it while this one waited.
            if (cachedPublic is { } fresh && DateTimeOffset.UtcNow - fresh.CheckedAt < TimeSpan.FromSeconds(30)) return fresh;
            string? routerAddress = null;
            try
            {
                using var routerTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                routerTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                routerAddress = await wan.GetRouterWanAddressAsync(routerTimeout.Token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested) { }

            PublicAddress result;
            if (HostAddresses.IsPublic(routerAddress))
                result = new PublicAddress(routerAddress, "router", DateTimeOffset.UtcNow, null);
            else
            {
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                    var address = (await http.GetStringAsync("https://api.ipify.org?format=text", token)).Trim();
                    result = HostAddresses.IsPublic(address)
                        ? new PublicAddress(address, "ipify", DateTimeOffset.UtcNow, null)
                        : new PublicAddress(null, null, DateTimeOffset.UtcNow, "The public-address service gave no public IPv4 address.");
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !token.IsCancellationRequested)
                {
                    result = new PublicAddress(null, null, DateTimeOffset.UtcNow, $"The public address could not be found: {ex.Message}");
                }
            }
            cachedPublic = result;
            return result;
        }
        finally { PublicGate.Release(); }
    }

    private sealed record PublicAddress(string? Address, string? Source, DateTimeOffset CheckedAt, string? Error);
}

public sealed record HostAddressSnapshot(
    int GamePort, IReadOnlyList<LocalAddress> Local, string? PublicAddress, string? PublicSource, DateTimeOffset? PublicCheckedAt, string? PublicError);