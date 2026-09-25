using System.Globalization;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.ViewModels;

// v0.8.20.0: the HOST tab's history card -- the machine's processor, memory and busiest adapter over the last hour,
// day or week. Loaded when the page opens, when the range changes, and about once a minute with the 5-second tick
// (a new reading is taken once a minute).
public sealed partial class MainWindowViewModel
{
    private static readonly double[] HistoryRangeHours = [1, 24, 24 * 7];
    private HostHistoryDto? _hostHistory;
    private int _hostHistoryRangeIndex = 1;
    private string _hostHistoryStatusText = string.Empty;
    private int _hostHistoryTick;

    public IReadOnlyList<string> HostHistoryRangeOptions { get; } = ["Last hour", "Last 24 hours", "Last 7 days"];

    public int HostHistoryRangeIndex
    {
        get => _hostHistoryRangeIndex;
        set
        {
            if (value < 0 || value >= HistoryRangeHours.Length || value == _hostHistoryRangeIndex) return;
            _hostHistoryRangeIndex = value;
            RaisePropertyChanged();
            _ = RefreshHostHistoryAsync();
        }
    }

    public IReadOnlyList<HostHistorySampleDto> HostHistorySamples => _hostHistory?.Samples ?? [];
    public string HostHistoryStatusText { get => _hostHistoryStatusText; private set => SetField(ref _hostHistoryStatusText, value); }

    public string HostHistorySummaryText => _hostHistory is not { ReadingsInRange: > 0 } h
        ? "No readings yet in this range. MystTiq reads the machine once a minute while it runs."
        : string.Join(" · ", new[]
        {
            h.AverageCpuPercent is { } ac && h.PeakCpuPercent is { } pc ? $"Processor {ac:0} % on average, peak {pc:0} %" : null,
            h.AverageMemoryUsedPercent is { } am && h.PeakMemoryUsedPercent is { } pm ? $"memory {am:0} % used on average, peak {pm:0} %" : null,
            h.PeakSentBytesPerSecond is { } ps ? $"busiest upload {HostFormat.Rate(ps)}" : null,
        }.Where(s => s is not null));

    public string HostHistoryCoverageText => _hostHistory is not { ReadingsInRange: > 0 } h ? string.Empty
        : $"{h.ReadingsInRange} readings since {h.FirstReadingAt?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)}. Blue: processor · violet: memory (both 0-100 %) · green: upload of the busiest adapter, to its own peak.";

    // Called with every HOST refresh; loads the history on the page's first refresh and then about once a minute.
    private async Task MaybeRefreshHostHistoryAsync(bool force)
    {
        if (!force && _hostHistoryTick++ % 12 != 0) return;
        await RefreshHostHistoryAsync();
    }

    private async Task RefreshHostHistoryAsync()
    {
        if (SelectedProfile is null) return;
        try
        {
            _hostHistory = await _api.GetHostHistoryAsync(SelectedProfile, HistoryRangeHours[HostHistoryRangeIndex], BearerToken);
            HostHistoryStatusText = string.Empty;
        }
        catch (Exception ex) { HostHistoryStatusText = "The history could not be read: " + ex.Message; }
        ApplyHostHistory(_hostHistory);
    }

    private void ApplyHostHistory(HostHistoryDto? history)
    {
        _hostHistory = history;
        RaisePropertyChanged(nameof(HostHistorySamples));
        RaisePropertyChanged(nameof(HostHistorySummaryText));
        RaisePropertyChanged(nameof(HostHistoryCoverageText));
    }
}
