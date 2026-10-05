// MystTiq v1.0.0.2: file reviewed for this release (2026-10-05).
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MystTiq.Core.Services;

/// <summary>v1.0.0.1: one IPv4 address of this machine, with its adapter; a gateway marks the adapter that reaches the LAN.</summary>
public sealed record LocalAddress(string Adapter, string Address, bool HasGateway);

/// <summary>
/// v1.0.0.1: the machine's addresses for the Dashboard (requested 2026-09-30): its local IPv4 addresses, which players on
/// the same network use, and whether an address is public (players on the internet use the public one).
/// </summary>
public static class HostAddresses
{
    public static IReadOnlyList<LocalAddress> Local()
    {
        var list = new List<LocalAddress>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                IPInterfaceProperties props;
                try { props = nic.GetIPProperties(); } catch { continue; }
                var hasGateway = props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
                foreach (var unicast in props.UnicastAddresses)
                {
                    var ip = unicast.Address;
                    if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip) || IsLinkLocal(ip)) continue;
                    list.Add(new LocalAddress(nic.Name, ip.ToString(), hasGateway));
                }
            }
        }
        catch { /* no adapters readable: none listed */ }
        // The adapter that reaches the network (it has a gateway) first; then by name.
        return [.. list.DistinctBy(a => a.Address).OrderByDescending(a => a.HasGateway).ThenBy(a => a.Adapter, StringComparer.OrdinalIgnoreCase)];
    }

    public static bool IsPublic(string? address)
    {
        if (!IPAddress.TryParse(address, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork) return false;
        var b = ip.GetAddressBytes();
        return !(b[0] == 10 || b[0] == 127 || b[0] == 0 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) ||
                 (b[0] == 100 && b[1] >= 64 && b[1] <= 127) || (b[0] == 169 && b[1] == 254) || b[0] >= 224);
    }

    private static bool IsLinkLocal(IPAddress ip) { var b = ip.GetAddressBytes(); return b[0] == 169 && b[1] == 254; }
}