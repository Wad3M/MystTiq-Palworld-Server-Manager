using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using MystTiq.Core.Services;

namespace MystTiq.HeadlessHost;

// v0.8.17.0: the HOST tab's view of the machine MystTiq runs on: processor, memory, the disks a server uses, and the
// network adapters' traffic. One instance for the whole fleet (it is the same machine for every server). Counters
// become rates against the previous reading; the first reading of a session samples twice, one second apart.
public sealed class HeadlessHostMonitor
{
    private static readonly TimeSpan FirstSampleGap = TimeSpan.FromSeconds(1);
    private readonly object gate = new();
    private readonly string processorName = HostMetricsReader.ProcessorName();
    private (CpuTimes Times, DateTimeOffset At)? lastCpu;
    private Dictionary<string, (long Received, long Sent, DateTimeOffset At)> lastNetwork = new(StringComparer.Ordinal);

    public async Task<HeadlessHostSnapshot> GetSnapshotAsync(IReadOnlyList<(string Role, string Path)> serverPaths, CancellationToken token)
    {
        bool first;
        lock (gate) first = lastCpu is null;
        if (first)
        {
            TakeCounters();
            await Task.Delay(FirstSampleGap, token);
        }

        var now = DateTimeOffset.UtcNow;
        var (cpuPercent, network) = TakeCounters();
        var memory = HostMetricsReader.ReadMemory();
        return new HeadlessHostSnapshot(
            Environment.MachineName,
            RuntimeInformation.OSDescription,
            processorName,
            Environment.ProcessorCount,
            cpuPercent,
            memory?.TotalBytes ?? 0,
            memory?.AvailableBytes ?? 0,
            (long)TimeSpan.FromMilliseconds(Environment.TickCount64).TotalSeconds,
            Disks(serverPaths),
            network,
            now);
    }

    private (double? CpuPercent, IReadOnlyList<HeadlessHostNetworkAdapter> Network) TakeCounters()
    {
        var now = DateTimeOffset.UtcNow;
        var cpu = HostMetricsReader.ReadCpuTimes();
        var adapters = new List<HeadlessHostNetworkAdapter>();
        var seen = new Dictionary<string, (long, long, DateTimeOffset)>(StringComparer.Ordinal);
        lock (gate)
        {
            double? cpuPercent = null;
            if (cpu is { } times)
            {
                if (lastCpu is { } previous) cpuPercent = HostMetricsReader.CpuPercent(previous.Times, times);
                lastCpu = (times, now);
            }

            foreach (var nic in SafeInterfaces())
            {
                IPInterfaceStatistics stats;
                try { stats = nic.GetIPStatistics(); } catch { continue; }
                var received = stats.BytesReceived;
                var sent = stats.BytesSent;
                seen[nic.Id] = (received, sent, now);
                double? inRate = null, outRate = null;
                if (lastNetwork.TryGetValue(nic.Id, out var before) && now > before.At)
                {
                    var seconds = (now - before.At).TotalSeconds;
                    if (received >= before.Received) inRate = (received - before.Received) / seconds;
                    if (sent >= before.Sent) outRate = (sent - before.Sent) / seconds;
                }
                adapters.Add(new HeadlessHostNetworkAdapter(nic.Name, nic.Description, nic.NetworkInterfaceType.ToString(),
                    nic.Speed > 0 ? nic.Speed : null, inRate, outRate, received, sent));
            }
            lastNetwork = seen;
            return (cpuPercent, adapters.OrderByDescending(a => (a.ReceivedBytesPerSecond ?? 0) + (a.SentBytesPerSecond ?? 0)).ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }

    // Up adapters that carry real traffic: no loopback or tunnel pseudo-adapters, no adapter that has never moved a
    // byte, and no filter layer. Found live on a Hyper-V host: Windows also lists every NDIS filter bound to an adapter
    // ("Ethernet-WFP Native MAC Layer LightWeight Filter-0000", "...-QoS Packet Scheduler-0000") as an adapter of its
    // own with the same counters, so the same traffic showed up to four times.
    private static IEnumerable<NetworkInterface> SafeInterfaces()
    {
        NetworkInterface[] all;
        try { all = NetworkInterface.GetAllNetworkInterfaces(); } catch { yield break; }
        var up = all.Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
            nic.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)).ToArray();
        var names = up.Select(nic => nic.Name).ToArray();
        foreach (var nic in up)
        {
            if (IsFilterLayer(nic.Name, names)) continue;
            try
            {
                var stats = nic.GetIPStatistics();
                if (stats.BytesReceived == 0 && stats.BytesSent == 0) continue;
            }
            catch { continue; }
            yield return nic;
        }
    }

    // "<adapter>-<filter name>-0000", where <adapter> is another listed adapter.
    public static bool IsFilterLayer(string name, IReadOnlyCollection<string> names)
    {
        if (name.Length < 6 || name[^5] != '-' || !name[^4..].All(char.IsAsciiDigit)) return false;
        return names.Any(other => !string.Equals(other, name, StringComparison.Ordinal) &&
            name.StartsWith(other + "-", StringComparison.Ordinal));
    }

    // The fixed disks, each marked with what of this server lives on it (the install, saves, backups).
    public static IReadOnlyList<HeadlessHostDisk> Disks(IReadOnlyList<(string Role, string Path)> serverPaths)
    {
        var disks = new List<HeadlessHostDisk>();
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives(); } catch { return disks; }
        var fixedDrives = drives.Where(d => { try { return d.IsReady && d.DriveType == DriveType.Fixed && d.TotalSize > 0; } catch { return false; } })
            .OrderByDescending(d => d.RootDirectory.FullName.Length).ToArray();
        var roles = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var (role, path) in serverPaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            string full;
            try { full = Path.GetFullPath(path); } catch { continue; }
            // The longest mount point that contains the path is the one it lives on.
            var owner = fixedDrives.FirstOrDefault(d => full.StartsWith(d.RootDirectory.FullName, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));
            if (owner is null) continue;
            if (!roles.TryGetValue(owner.Name, out var list)) roles[owner.Name] = list = [];
            if (!list.Contains(role)) list.Add(role);
        }
        // The disks holding something of this server first (found live: on a machine with a dozen data drives the
        // server's own disk was otherwise one row among many).
        foreach (var drive in fixedDrives.OrderByDescending(d => roles.ContainsKey(d.Name)).ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            // On Linux only the disks that hold something of this server, since /proc, /sys and friends are "fixed" too.
            roles.TryGetValue(drive.Name, out var used);
            if (!OperatingSystem.IsWindows() && used is null && drive.Name != "/") continue;
            string label;
            try { label = drive.VolumeLabel; } catch { label = string.Empty; }
            disks.Add(new HeadlessHostDisk(drive.Name, label, drive.DriveFormat, drive.TotalSize, drive.AvailableFreeSpace, used ?? []));
        }
        return disks;
    }
}

public sealed record HeadlessHostPageSnapshot(HeadlessHostSnapshot Host, HeadlessResourcePolicySnapshot Resources, HeadlessNetworkPolicySnapshot Bandwidth);

public sealed record HeadlessHostDisk(string Name, string Label, string Format, long TotalBytes, long FreeBytes, IReadOnlyList<string> Holds);

public sealed record HeadlessHostNetworkAdapter(
    string Name, string Description, string Kind, long? SpeedBitsPerSecond,
    double? ReceivedBytesPerSecond, double? SentBytesPerSecond, long ReceivedBytesTotal, long SentBytesTotal);

public sealed record HeadlessHostSnapshot(
    string MachineName,
    string OperatingSystem,
    string ProcessorName,
    int LogicalProcessors,
    double? CpuPercent,
    ulong MemoryTotalBytes,
    ulong MemoryAvailableBytes,
    long UptimeSeconds,
    IReadOnlyList<HeadlessHostDisk> Disks,
    IReadOnlyList<HeadlessHostNetworkAdapter> Network,
    DateTimeOffset ObservedAt);
