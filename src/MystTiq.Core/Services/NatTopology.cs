using System.Net;
using System.Net.Sockets;
using MystTiq.Core.Models;
namespace MystTiq.Core.Services;

// v0.8.12.0: is there a second NAT between the router MystTiq can talk to (UPnP) and the internet? A port forward on the
// router only helps when the router itself holds the address the internet sees. Two cases defeat it:
//   - carrier-grade NAT: the ISP gives the router an address from 100.64.0.0/10 (RFC 6598) and shares one public
//     address among many customers, so nothing the user does at home opens a port;
//   - double NAT: the router's WAN address is itself private (10/8, 172.16/12, 192.168/16), i.e. it sits behind another
//     router (often an ISP modem-router), which also needs the forward, or bridge mode.
// Pure: the router's WAN address (UPnP GetExternalIPAddress) and the public address (ipify) come in as strings.
// MystTiq cannot send a real game packet from outside, so a Pass here means "no second NAT seen", not "reachable".
public static class NatTopology
{
    public const string TestName = "Second NAT (CGNAT / double NAT)";

    public static WanReachabilityCheck Classify(string? publicIp, string? routerWanIp)
    {
        if (string.IsNullOrWhiteSpace(routerWanIp) || !IPAddress.TryParse(routerWanIp.Trim(), out var wan) || wan.AddressFamily != AddressFamily.InterNetwork)
            return new(TestName, DiagnosticState.Skipped,
                "The router did not report its internet-side (WAN) address over UPnP, so a second NAT cannot be ruled out.",
                "Look at your router's status page: if its WAN/Internet address starts with 100.64–100.127, 10., 172.16–172.31 or 192.168., it is behind another NAT.");

        var wanText = wan.ToString();
        if (IsCarrierGrade(wan))
            return new(TestName, DiagnosticState.Fail,
                $"Your router's internet address is {wanText}, a carrier-grade NAT address (100.64.0.0/10). Your ISP shares one public address among many customers, so a port forward on your router cannot make the server reachable from the internet.",
                "Ask your ISP for a public IPv4 address (sometimes sold as a \"static\" or \"public IP\" option), or host through a tunnel or VPN service that gives you a public port.");

        if (IsPrivate(wan))
            return new(TestName, DiagnosticState.Fail,
                $"Your router's internet address is {wanText}, a private address: it sits behind another router or modem-router (double NAT). A forward on this router alone does not reach the internet.",
                "Forward the game port on the upstream device too (to this router's address), or put the upstream device in bridge mode or this router in its DMZ.");

        if (!string.IsNullOrWhiteSpace(publicIp) && IPAddress.TryParse(publicIp.Trim(), out var pub) && !pub.Equals(wan))
            return new(TestName, DiagnosticState.Warning,
                $"Your router reports {wanText} as its internet address, but the internet sees you as {pub}. Something between them translates addresses again (often the ISP's NAT), so a forward on your router may not be enough.",
                "Compare with your router's status page and ask your ISP whether your connection is behind their NAT.");

        return new(TestName, DiagnosticState.Pass,
            string.IsNullOrWhiteSpace(publicIp)
                ? $"Your router holds a public internet address ({wanText}); no second NAT was seen."
                : $"Your router holds the public address the internet sees ({wanText}); no second NAT was seen, so forwarding the port on this router is enough.");
    }

    // v0.8.12.0: evidence from the route out when the router will not report its WAN address (no UPnP). hops[0] is the
    // first hop (the home router). A public hop 2 rules a second NAT out (Pass); a 100.64/10 hop is a cautious Warning
    // (that range exists for CGNAT, though ISPs can use it internally); a private hop is "cannot tell" (see below). Never a
    // Fail. Returns null when the route gave nothing to go on.
    // routerRefused: the router answered UPnP but refused GetExternalIPAddress (the real router did, intermittently, as
    // UPnP error 501, and reported its public address on other tries). Said plainly; it is not evidence either way.
    public static WanReachabilityCheck? ClassifyRoute(IReadOnlyList<string?> hops, bool routerRefused = false)
    {
        var check = ClassifyRouteCore(hops);
        if (check is null || !routerRefused) return check;
        const string plain = "Your router did not report its internet address";
        return check with { Details = check.Details.Replace(plain, "Your router answered UPnP but would not report its internet address this time (UPnP error)") };
    }

    private static WanReachabilityCheck? ClassifyRouteCore(IReadOnlyList<string?> hops)
    {
        if (hops.Count < 2 || !IPAddress.TryParse(hops[0] ?? string.Empty, out var first) || !IsPrivate(first)) return null;
        for (var i = 1; i < hops.Count; i++)
        {
            if (!IPAddress.TryParse(hops[i] ?? string.Empty, out var hop) || hop.AddressFamily != AddressFamily.InterNetwork) continue;
            var n = i + 1;
            if (IsCarrierGrade(hop))
                return new(TestName, DiagnosticState.Warning,
                    $"Your router did not report its internet address, but hop {n} on the way out is {hop}, in the range ISPs use for carrier-grade NAT (100.64.0.0/10). If your connection is behind their NAT, a port forward at home cannot make the server reachable.",
                    "Check your router's status page: an internet (WAN) address starting 100.64–100.127 confirms it. Then ask your ISP for a public IPv4 address, or host through a tunnel or VPN service.");
            // A private hop after the router is NOT evidence enough: on this project's own network the router holds the
            // public address and the next hop (10.x) is the ISP's own equipment. Early drafts warned "double NAT" here and
            // were wrong on the real network, so this only says it cannot tell and how to find out.
            if (IsPrivate(hop))
                return new(TestName, DiagnosticState.Skipped,
                    $"Your router did not report its internet address, and hop {n} on the way out ({hop}) is a private address. That happens with double NAT (a router behind another router), but just as often on the ISP's own network, so it cannot tell which.",
                    "Check your router's status page: if its internet (WAN) address is private (10., 172.16–172.31, 192.168.), it is behind another router, and the game port must be forwarded on that device too (or use bridge mode). If it matches your public address, a forward on this router is enough.");
            // A public hop only rules a second NAT out when it is the very next hop. If hop 2 did not answer, it could be
            // the second NAT itself (seen on the test VM: hop 2 sometimes stays silent), so there is no verdict.
            if (i > 1) return null;
            return new(TestName, DiagnosticState.Pass,
                $"Your router did not report its internet address, but the first hop past it ({hop}) is public, so no second NAT was seen on the route out.");
        }
        return null;
    }

    public static bool IsCarrierGrade(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return b.Length == 4 && b[0] == 100 && b[1] >= 64 && b[1] <= 127;
    }

    public static bool IsPrivate(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return b.Length == 4 && (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168));
    }
}
