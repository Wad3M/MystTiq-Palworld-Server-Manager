using PalworldManager.Services;
using PalworldManager.Models;
using System.Windows.Media;

namespace PalworldManager;

public partial class MainWindow
{
    private HistoricalAnalyticsService? historicalAnalytics;

    private TimeSpan SelectedHistoryRange => HistoricalRangeCombo.SelectedIndex switch
    {
        0 => TimeSpan.FromHours(1),
        1 => TimeSpan.FromHours(6),
        3 => TimeSpan.FromDays(7),
        4 => TimeSpan.FromDays(30),
        _ => TimeSpan.FromHours(24)
    };

    private void HistoricalRangeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) RefreshHistoricalAnalytics();
    }

    private void HistoricalRefresh_Click(object sender, RoutedEventArgs e) => RefreshHistoricalAnalytics();

    private void HistoricalExport_Click(object sender, RoutedEventArgs e)
    {
        if (historicalAnalytics is null) return;
        try
        {
            var path = historicalAnalytics.ExportCsv(SelectedHistoryRange);
            HistoricalStatusText.Text = "Exported: " + path;
            RecordAudit("Success", "History", "Historical analytics exported", path, 0);
        }
        catch (Exception ex)
        {
            HistoricalStatusText.Text = "Export failed: " + ex.Message;
        }
    }

    private void RefreshHistoricalAnalytics()
    {
        if (historicalAnalytics is null || HistoricalCpuLine is null) return;
        var snapshot = historicalAnalytics.Snapshot(SelectedHistoryRange);
        HistoricalCpuSummaryText.Text = $"{snapshot.CpuTrend} Avg {snapshot.AverageCpu:0.0}% • Peak {snapshot.PeakCpu:0.0}%";
        HistoricalMemorySummaryText.Text = $"{snapshot.MemoryTrend} Avg {FormatMemory(snapshot.AverageMemoryMb)} • Peak {FormatMemory(snapshot.PeakMemoryMb)}";
        HistoricalPlayersSummaryText.Text = $"{snapshot.PlayerTrend} Peak {snapshot.PeakPlayers} online";
        HistoricalWorldSummaryText.Text = $"{snapshot.WorldTrend} {FormatBytes(snapshot.WorldGrowthBytes)} growth";
        HistoricalSampleCountText.Text = $"{snapshot.Samples.Count} sample(s) in selected range";
        HistoricalBackupSummaryText.Text = snapshot.Latest is null
            ? "Backups: —"
            : $"Backups: {snapshot.Latest.BackupCount}";
        HistoricalUptimeSummaryText.Text = snapshot.Latest is null
            ? "Uptime: —"
            : "Uptime: " + TimeSpan.FromMinutes(snapshot.Latest.UptimeMinutes).ToString(@"dd\:hh\:mm");

        HistoricalCpuLine.Points = BuildPoints(snapshot.Samples.Select(x => x.CpuPercent), 300, 58, 100);
        HistoricalMemoryLine.Points = BuildPoints(snapshot.Samples.Select(x => x.MemoryMb), 300, 58);
        HistoricalPlayersLine.Points = BuildPoints(snapshot.Samples.Select(x => (double)x.OnlinePlayers), 300, 70);
        HistoricalWorldLine.Points = BuildPoints(snapshot.Samples.Select(x => (double)x.WorldSizeBytes), 300, 70);
        HistoricalStatusText.Text = snapshot.Samples.Count == 0
            ? "Historical collection starts automatically while MystTiq is running."
            : $"Updated {DateTime.Now:HH:mm:ss}";
    }

    private static PointCollection BuildPoints(IEnumerable<double> values, double width, double height, double? fixedMaximum = null)
    {
        var data = values.ToArray();
        var points = new PointCollection();
        if (data.Length == 0) return points;
        var max = fixedMaximum ?? data.Max();
        var min = fixedMaximum.HasValue ? 0 : data.Min();
        if (Math.Abs(max - min) < 0.001) max = min + 1;
        for (var i = 0; i < data.Length; i++)
        {
            var x = data.Length == 1 ? width : i * width / (data.Length - 1);
            var y = height - ((data[i] - min) / (max - min) * height);
            points.Add(new System.Windows.Point(x, Math.Clamp(y, 0, height)));
        }
        return points;
    }

    private static string FormatMemory(double mb) => mb >= 1024 ? $"{mb / 1024d:0.00} GB" : $"{mb:0} MB";

    private static string FormatBytes(long bytes)
    {
        var sign = bytes < 0 ? "-" : "+";
        var value = Math.Abs((double)bytes);
        if (value >= 1024 * 1024 * 1024) return $"{sign}{value / 1024 / 1024 / 1024:0.00} GB";
        if (value >= 1024 * 1024) return $"{sign}{value / 1024 / 1024:0.00} MB";
        if (value >= 1024) return $"{sign}{value / 1024:0.00} KB";
        return bytes == 0 ? "No" : $"{sign}{value:0} B";
    }
}
