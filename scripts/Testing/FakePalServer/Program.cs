// MystTiq v1.0.4.0: file reviewed for this release (2026-10-05).
using System.Net;
using System.Net.Sockets;

// v0.8.2.0: behaves like a PalServer that started and became ready: accepts (and ignores) PalServer's own launch
// arguments, binds its UDP game port (from -port=N, default 8211) so MystTiq's readiness check sees it, and runs until
// killed. "-notready" skips binding, for a server that never becomes ready. Test use only.
var port = 8211;
foreach (var arg in args)
    if (arg.StartsWith("-port=", StringComparison.OrdinalIgnoreCase) && int.TryParse(arg[6..], out var p) && p is > 0 and <= 65535)
        port = p;

// v1.0.0.1: a stand-in MOD makes it hang like a real stuck start: when UE4SS's mods.txt next to it enables "HangsStartup",
// it never binds its port (for the stuck-start protocol's smoke).
var modsTxt = Path.Combine(AppContext.BaseDirectory, "Pal", "Binaries", "Win64", "ue4ss", "Mods", "mods.txt");
var hangs = File.Exists(modsTxt) && File.ReadAllLines(modsTxt).Any(line =>
    line.Replace(" ", string.Empty).Equals("HangsStartup:1", StringComparison.OrdinalIgnoreCase));

UdpClient? udp = null;
if (!hangs && !args.Any(a => a.Equals("-notready", StringComparison.OrdinalIgnoreCase)))
    udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));

// v1.0.0.1: the arguments it was started with, so a smoke can check what MystTiq passes (nothing beyond -port= by default).
try { File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "launch-args.txt"), args); } catch { }
Console.WriteLine($"FakePalServer running (port {port}, ready={udp is not null}).");
await Task.Delay(Timeout.Infinite);
