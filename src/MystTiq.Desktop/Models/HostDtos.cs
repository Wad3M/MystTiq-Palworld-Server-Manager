using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

// v0.8.17.0: the HOST tab -- the machine a server runs on, and that server's processes with their priority and eco mode.
public sealed class HostPageSnapshotDto
{
    [JsonPropertyName("host")] public HostSnapshotDto Host { get; init; } = new();
    [JsonPropertyName("resources")] public ResourcePolicySnapshotDto Resources { get; init; } = new();
    // v0.8.18.0: bandwidth.
    [JsonPropertyName("bandwidth")] public BandwidthSnapshotDto Bandwidth { get; init; } = new();
}

// v0.8.20.0: the machine's history (one reading a minute, 7 days).
public sealed class HostHistorySampleDto
{
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("cpuPercent")] public double? CpuPercent { get; init; }
    [JsonPropertyName("memoryUsedPercent")] public double MemoryUsedPercent { get; init; }
    [JsonPropertyName("sentBytesPerSecond")] public double? SentBytesPerSecond { get; init; }
    [JsonPropertyName("receivedBytesPerSecond")] public double? ReceivedBytesPerSecond { get; init; }
    [JsonPropertyName("adapter")] public string? Adapter { get; init; }
}

public sealed class HostHistoryDto
{
    [JsonPropertyName("samples")] public IReadOnlyList<HostHistorySampleDto> Samples { get; init; } = [];
    [JsonPropertyName("rangeHours")] public double RangeHours { get; init; }
    [JsonPropertyName("readingsInRange")] public int ReadingsInRange { get; init; }
    [JsonPropertyName("averageCpuPercent")] public double? AverageCpuPercent { get; init; }
    [JsonPropertyName("peakCpuPercent")] public double? PeakCpuPercent { get; init; }
    [JsonPropertyName("averageMemoryUsedPercent")] public double? AverageMemoryUsedPercent { get; init; }
    [JsonPropertyName("peakMemoryUsedPercent")] public double? PeakMemoryUsedPercent { get; init; }
    [JsonPropertyName("peakSentBytesPerSecond")] public double? PeakSentBytesPerSecond { get; init; }
    [JsonPropertyName("peakReceivedBytesPerSecond")] public double? PeakReceivedBytesPerSecond { get; init; }
    [JsonPropertyName("firstReadingAt")] public DateTimeOffset? FirstReadingAt { get; init; }
}

// v0.8.18.0: the server's network limits (Engine.ini), kept by MystTiq and written before every start.
public sealed class NetworkPolicyDto
{
    [JsonPropertyName("mode")] public string Mode { get; init; } = "GameDefault";
    [JsonPropertyName("perPlayerMbps")] public double PerPlayerMbps { get; init; } = 8;
    [JsonPropertyName("tickRate")] public int TickRate { get; init; } = 60;
    [JsonPropertyName("uploadBudgetMbps")] public double? UploadBudgetMbps { get; init; }
}

public sealed class BandwidthSnapshotDto
{
    [JsonPropertyName("policy")] public NetworkPolicyDto Policy { get; init; } = new();
    [JsonPropertyName("running")] public bool Running { get; init; }
    [JsonPropertyName("policyInEngineIni")] public bool PolicyInEngineIni { get; init; }
    [JsonPropertyName("restartNeeded")] public bool RestartNeeded { get; init; }
    [JsonPropertyName("platform")] public string Platform { get; init; } = string.Empty;
    [JsonPropertyName("gameDefaultPerPlayerMbps")] public double GameDefaultPerPlayerMbps { get; init; }
    [JsonPropertyName("gameDefaultTickRate")] public int GameDefaultTickRate { get; init; }
    [JsonPropertyName("engineMaxClientRate")] public long? EngineMaxClientRate { get; init; }
    [JsonPropertyName("engineMaxInternetClientRate")] public long? EngineMaxInternetClientRate { get; init; }
    [JsonPropertyName("engineTickRate")] public int? EngineTickRate { get; init; }
    [JsonPropertyName("effectivePerPlayerMbps")] public double EffectivePerPlayerMbps { get; init; }
    [JsonPropertyName("effectiveTickRate")] public int EffectiveTickRate { get; init; }
    [JsonPropertyName("maxPlayers")] public int? MaxPlayers { get; init; }
    [JsonPropertyName("worstCaseUploadMbps")] public double? WorstCaseUploadMbps { get; init; }
    [JsonPropertyName("overUploadBudget")] public bool OverUploadBudget { get; init; }
    [JsonPropertyName("suggestedPerPlayerMbps")] public double? SuggestedPerPlayerMbps { get; init; }
    [JsonPropertyName("engineIniPath")] public string EngineIniPath { get; init; } = string.Empty;
}

public sealed class NetworkPolicySaveResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("snapshot")] public BandwidthSnapshotDto? Snapshot { get; init; }
}

public sealed class HostSnapshotDto
{
    [JsonPropertyName("machineName")] public string MachineName { get; init; } = string.Empty;
    [JsonPropertyName("operatingSystem")] public string OperatingSystem { get; init; } = string.Empty;
    [JsonPropertyName("processorName")] public string ProcessorName { get; init; } = string.Empty;
    [JsonPropertyName("logicalProcessors")] public int LogicalProcessors { get; init; }
    [JsonPropertyName("cpuPercent")] public double? CpuPercent { get; init; }
    [JsonPropertyName("memoryTotalBytes")] public ulong MemoryTotalBytes { get; init; }
    [JsonPropertyName("memoryAvailableBytes")] public ulong MemoryAvailableBytes { get; init; }
    [JsonPropertyName("uptimeSeconds")] public long UptimeSeconds { get; init; }
    [JsonPropertyName("disks")] public IReadOnlyList<HostDiskDto> Disks { get; init; } = [];
    [JsonPropertyName("network")] public IReadOnlyList<HostNetworkAdapterDto> Network { get; init; } = [];
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
}

public sealed class HostDiskDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; init; } = string.Empty;
    [JsonPropertyName("format")] public string Format { get; init; } = string.Empty;
    [JsonPropertyName("totalBytes")] public long TotalBytes { get; init; }
    [JsonPropertyName("freeBytes")] public long FreeBytes { get; init; }
    [JsonPropertyName("holds")] public IReadOnlyList<string> Holds { get; init; } = [];

    public string Title => string.IsNullOrWhiteSpace(Label) ? Name : $"{Name} ({Label})";
    public double UsedPercent => TotalBytes <= 0 ? 0 : Math.Clamp(100d * (TotalBytes - FreeBytes) / TotalBytes, 0, 100);
    public string UsageText => $"{HostFormat.Bytes(FreeBytes)} free of {HostFormat.Bytes(TotalBytes)} · {UsedPercent:0}% used";
    public string HoldsText => Holds.Count == 0 ? "Nothing of this server" : "Holds this server's " + HostFormat.JoinList(Holds);
    public bool IsLow => TotalBytes > 0 && FreeBytes * 10 < TotalBytes;
}

public sealed class HostNetworkAdapterDto
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;
    [JsonPropertyName("kind")] public string Kind { get; init; } = string.Empty;
    [JsonPropertyName("speedBitsPerSecond")] public long? SpeedBitsPerSecond { get; init; }
    [JsonPropertyName("receivedBytesPerSecond")] public double? ReceivedBytesPerSecond { get; init; }
    [JsonPropertyName("sentBytesPerSecond")] public double? SentBytesPerSecond { get; init; }

    public string Title => string.IsNullOrWhiteSpace(Description) || Description == Name ? Name : $"{Name} · {Description}";
    public string RateText => $"↓ {HostFormat.Rate(ReceivedBytesPerSecond)}   ↑ {HostFormat.Rate(SentBytesPerSecond)}";
    public string LinkText => SpeedBitsPerSecond is { } bits ? $"{Kind} · link {HostFormat.LinkSpeed(bits)}" : Kind;
}

public sealed class ResourcePolicyDto
{
    [JsonPropertyName("priority")] public string Priority { get; init; } = "Default";
    [JsonPropertyName("ecoMode")] public string EcoMode { get; init; } = "Off";
    [JsonPropertyName("ecoAfterEmptyMinutes")] public int EcoAfterEmptyMinutes { get; init; } = 10;
    // v0.8.24.0: "0-3, 6"; null or empty = every core.
    [JsonPropertyName("cores")] public string? Cores { get; init; }
}

public sealed class ProcessResourceStateDto
{
    [JsonPropertyName("processId")] public int ProcessId { get; init; }
    [JsonPropertyName("processName")] public string ProcessName { get; init; } = string.Empty;
    [JsonPropertyName("priority")] public string Priority { get; init; } = string.Empty;
    [JsonPropertyName("efficiency")] public string Efficiency { get; init; } = string.Empty;
    [JsonPropertyName("workingSetBytes")] public long WorkingSetBytes { get; init; }
    [JsonPropertyName("cores")] public string Cores { get; init; } = "Unknown";

    public string Title => $"{ProcessName} (PID {ProcessId})";
    public string StateText => $"Priority {HostFormat.Priority(Priority)} · efficiency mode {HostFormat.Efficiency(Efficiency)} · cores {(Cores == "Unknown" ? "unknown" : Cores)} · {HostFormat.Bytes(WorkingSetBytes)} memory";
}

public sealed class ResourcePolicySnapshotDto
{
    [JsonPropertyName("policy")] public ResourcePolicyDto Policy { get; init; } = new();
    [JsonPropertyName("running")] public bool Running { get; init; }
    [JsonPropertyName("ecoActive")] public bool EcoActive { get; init; }
    [JsonPropertyName("ecoReason")] public string EcoReason { get; init; } = string.Empty;
    [JsonPropertyName("playersOnline")] public int? PlayersOnline { get; init; }
    [JsonPropertyName("efficiencyModeSupported")] public bool EfficiencyModeSupported { get; init; }
    [JsonPropertyName("processes")] public IReadOnlyList<ProcessResourceStateDto> Processes { get; init; } = [];
    [JsonPropertyName("lastError")] public string LastError { get; init; } = string.Empty;
    [JsonPropertyName("lastAppliedUtc")] public DateTimeOffset? LastAppliedUtc { get; init; }
    [JsonPropertyName("processorCount")] public int ProcessorCount { get; init; }
}

public sealed class ResourcePolicySaveResultDto
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("message")] public string Message { get; init; } = string.Empty;
    [JsonPropertyName("policy")] public ResourcePolicyDto Policy { get; init; } = new();
    [JsonPropertyName("snapshot")] public ResourcePolicySnapshotDto? Snapshot { get; init; }
}

// Plain, testable text for the HOST tab.
public static class HostFormat
{
    public static string Bytes(double bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return unit == 0 ? $"{value:0} B" : $"{value:0.#} {units[unit]}";
    }

    // Network rates in bits per second, as connections are sold; "—" when there is no reading yet.
    public static string Rate(double? bytesPerSecond)
    {
        if (bytesPerSecond is not { } b) return "—";
        var bits = b * 8;
        return bits >= 1_000_000_000 ? $"{bits / 1_000_000_000:0.0} Gbit/s"
            : bits >= 1_000_000 ? $"{bits / 1_000_000:0.0} Mbit/s"
            : bits >= 1_000 ? $"{bits / 1_000:0} kbit/s"
            : $"{bits:0} bit/s";
    }

    public static string LinkSpeed(long bits) =>
        bits >= 1_000_000_000 ? $"{bits / 1_000_000_000d:0.#} Gbit/s" : $"{bits / 1_000_000d:0} Mbit/s";

    public static string Uptime(long seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalDays >= 1 ? $"{(int)t.TotalDays} d {t.Hours} h" : t.TotalHours >= 1 ? $"{t.Hours} h {t.Minutes} min" : $"{t.Minutes} min";
    }

    public static string Priority(string priority) => priority switch
    {
        "BelowNormal" => "below normal",
        "AboveNormal" => "above normal",
        "Normal" => "normal",
        "High" => "high",
        "Idle" => "idle",
        "RealTime" => "real-time",
        _ => "unknown",
    };

    public static string Efficiency(string state) => state switch
    {
        "On" => "on",
        "Off" => "off",
        "Default" => "system default",
        _ => "not available",
    };

    public static string JoinList(IReadOnlyList<string> items) => items.Count switch
    {
        0 => string.Empty,
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " and " + items[^1],
    };
}
