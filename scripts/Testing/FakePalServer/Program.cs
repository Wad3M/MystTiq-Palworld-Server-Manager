using System.Net;
using System.Net.Sockets;

// v0.8.2.0: behaves like a PalServer that started and became ready: accepts (and ignores) PalServer's own launch
// arguments, binds its UDP game port (from -port=N, default 8211) so MystTiq's readiness check sees it, and runs until
// killed. "-notready" skips binding, for a server that never becomes ready. Test use only.
var port = 8211;
foreach (var arg in args)
    if (arg.StartsWith("-port=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg[6..], out var p) && p is > 0 and <= 65535)
        port = p;

UdpClient? udp = null;
if (!args.Any(a => a.Equals("-notready", StringComparison.OrdinalIgnoreCase)))
    udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));

Console.WriteLine($"FakePalServer running (port {port}, ready={udp is not null}).");
await Task.Delay(Timeout.Infinite);
