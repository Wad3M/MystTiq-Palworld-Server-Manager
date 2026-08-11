using System.Runtime.InteropServices;
using MystTiq.Core.Models;

namespace MystTiq.Core.Services;

public sealed class LinuxDistributionService
{
    public LinuxDistributionInfo Detect()
    {
        if (!OperatingSystem.IsLinux())
            throw new PlatformNotSupportedException("Linux distribution detection requires Linux.");

        var values = ReadOsRelease("/etc/os-release");
        var kernel = ReadKernelRelease();
        if (string.IsNullOrWhiteSpace(kernel))
            kernel = RuntimeInformation.OSDescription;
        var architecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();

        return new LinuxDistributionInfo(
            Get(values, "ID", "unknown"),
            Get(values, "VERSION_ID", "unknown"),
            Get(values, "PRETTY_NAME", "Unknown Linux"),
            kernel,
            architecture);
    }

    private static Dictionary<string, string> ReadOsRelease(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(path))
            return result;

        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                value = value[1..^1].Replace("\\\"", "\"");

            result[key] = value;
        }

        return result;
    }

    private static string ReadKernelRelease()
    {
        try
        {
            const string path = "/proc/sys/kernel/osrelease";
            return File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key, string fallback) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
}
