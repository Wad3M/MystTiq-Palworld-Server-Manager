using System.Diagnostics;

namespace PalworldManager.Services;

/// <summary>
/// Samples aggregate Palworld CPU and memory usage without owning server lifecycle.
/// </summary>
public sealed class ServerResourceMonitor
{
    private readonly object syncRoot = new();
    private readonly IReadOnlyList<string> processNames;
    private readonly Dictionary<int, CpuSample> cpuSamples = [];

    public ServerResourceMonitor(IEnumerable<string> processNames)
    {
        this.processNames = processNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public ServerResourceUsage Sample()
    {
        var memoryMb = 0d;
        var cpuPercent = 0d;
        var nowUtc = DateTime.UtcNow;
        var activeProcessIds = new HashSet<int>();

        lock (syncRoot)
        {
            foreach (var name in processNames)
            {
                foreach (var process in Process.GetProcessesByName(name))
                {
                    using (process)
                    {
                        try
                        {
                            process.Refresh();
                            activeProcessIds.Add(process.Id);
                            memoryMb += process.WorkingSet64 / 1024d / 1024d;

                            var currentCpu = process.TotalProcessorTime;
                            if (cpuSamples.TryGetValue(process.Id, out var previous))
                            {
                                var elapsedMs = (nowUtc - previous.TimestampUtc).TotalMilliseconds;
                                var cpuMs = (currentCpu - previous.TotalProcessorTime).TotalMilliseconds;
                                if (elapsedMs > 0 && cpuMs >= 0)
                                {
                                    cpuPercent += cpuMs / elapsedMs /
                                        Math.Max(1, Environment.ProcessorCount) * 100d;
                                }
                            }

                            cpuSamples[process.Id] = new CpuSample(currentCpu, nowUtc);
                        }
                        catch
                        {
                            // The process may exit between enumeration and sampling.
                        }
                    }
                }
            }

            foreach (var staleId in cpuSamples.Keys.Where(id => !activeProcessIds.Contains(id)).ToArray())
                cpuSamples.Remove(staleId);
        }

        return new ServerResourceUsage(
            Math.Max(0d, memoryMb),
            Math.Clamp(cpuPercent, 0d, 100d));
    }

    private readonly record struct CpuSample(TimeSpan TotalProcessorTime, DateTime TimestampUtc);
}
