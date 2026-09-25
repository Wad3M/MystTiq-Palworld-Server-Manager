using System.Runtime.InteropServices;

namespace MystTiq.Core.Services;

// v0.8.17.0: the machine a server runs on -- its processor time, memory and processor name -- read the way the
// operating system counts them (GetSystemTimes/GlobalMemoryStatusEx on Windows, /proc on Linux). Nothing here is
// sampled on its own; HeadlessHostMonitor keeps the previous reading to turn counters into rates.
public readonly record struct CpuTimes(ulong Idle, ulong Total);
public readonly record struct HostMemory(ulong TotalBytes, ulong AvailableBytes);

public static class HostMetricsReader
{
    public static CpuTimes? ReadCpuTimes()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Kernel time includes idle time.
                if (!GetSystemTimes(out var idle, out var kernel, out var user)) return null;
                return new CpuTimes(idle, kernel + user);
            }
            var first = File.ReadLines("/proc/stat").First(l => l.StartsWith("cpu ", StringComparison.Ordinal));
            return ParseProcStatCpu(first);
        }
        catch { return null; }
    }

    // "cpu  user nice system idle iowait irq softirq steal guest guest_nice"; idle includes iowait, and guest time is
    // already inside user time, so it is not added again.
    public static CpuTimes? ParseProcStatCpu(string line)
    {
        var values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(v => ulong.TryParse(v, out var n) ? n : 0UL).ToArray();
        if (values.Length < 4) return null;
        ulong total = 0;
        for (var i = 0; i < Math.Min(values.Length, 8); i++) total += values[i];
        var idle = values[3] + (values.Length > 4 ? values[4] : 0);
        return new CpuTimes(idle, total);
    }

    // Busy share of the processor between two readings, 0..100; null when the counters did not move.
    public static double? CpuPercent(CpuTimes previous, CpuTimes current)
    {
        if (current.Total <= previous.Total || current.Idle < previous.Idle) return null;
        var total = (double)(current.Total - previous.Total);
        var idle = (double)(current.Idle - previous.Idle);
        return Math.Clamp((total - idle) / total * 100d, 0d, 100d);
    }

    public static HostMemory? ReadMemory()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
                return GlobalMemoryStatusEx(ref status) ? new HostMemory(status.TotalPhys, status.AvailPhys) : null;
            }
            return ParseMemInfo(File.ReadAllLines("/proc/meminfo"));
        }
        catch { return null; }
    }

    public static HostMemory? ParseMemInfo(IEnumerable<string> lines)
    {
        ulong? total = null, available = null;
        foreach (var line in lines)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !ulong.TryParse(parts[1], out var kib)) continue;
            if (parts[0] == "MemTotal:") total = kib * 1024;
            else if (parts[0] == "MemAvailable:") available = kib * 1024;
        }
        return total is { } t && available is { } a ? new HostMemory(t, a) : null;
    }

    public static string ProcessorName()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return (key?.GetValue("ProcessorNameString") as string)?.Trim() ?? string.Empty;
            }
            var line = File.ReadLines("/proc/cpuinfo").FirstOrDefault(l => l.StartsWith("model name", StringComparison.Ordinal));
            return line is null ? string.Empty : line[(line.IndexOf(':') + 1)..].Trim();
        }
        catch { return string.Empty; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
