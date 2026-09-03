using System.Text.Json.Serialization;

namespace MystTiq.Desktop.Models;

/// <summary>Single coherent polling payload used by the desktop status loop.</summary>
public sealed class StatusPollingDto
{
    [JsonPropertyName("observedAt")] public DateTimeOffset ObservedAt { get; init; }
    [JsonPropertyName("status")] public ServerStatusDto Status { get; init; } = new();
    [JsonPropertyName("service")] public ServiceStatusDto Service { get; init; } = new();
    [JsonPropertyName("players")] public PlayersSnapshotDto Players { get; init; } = new();
    [JsonPropertyName("metrics")] public RuntimeMetricsSnapshotDto Metrics { get; init; } = new();
    [JsonPropertyName("logTail")] public LogTailSnapshotDto LogTail { get; init; } = new();
    [JsonPropertyName("world")] public WorldExplorerSnapshotDto World { get; init; } = new();
    [JsonPropertyName("backupInventory")] public BackupInventoryDto BackupInventory { get; init; } = new();
    [JsonPropertyName("palworldSettings")] public PalworldConfigurationSnapshotDto PalworldSettings { get; init; } = new();
}
