// MystTiq v1.0.8.7: file reviewed for this release (2026-10-12).
using System.Text.RegularExpressions;
using System.Windows.Input;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

/// <summary>
/// v1.0.8.0 (roadmap C-0, the pilot): the Console page and the server log lines, moved out of MainWindowViewModel
/// unchanged. The window passes each log tail to <see cref="Apply"/>; the Dashboard reads <see cref="LogLines"/>.
/// </summary>
public sealed class ConsoleLogViewModel : PageViewModel
{
    public const int LineFirstSeenLimit = 3000;
    private static readonly Regex ConsoleTimestamp = new(@"^\[\d{4}-\d{2}-\d{2}[ T]", RegexOptions.Compiled);

    // v1.0.7.3 (roadmap Q-6): the tail last applied, each line's first-seen time, and whether the filtered view is behind
    // (it is only rebuilt while the Console page is open).
    private IReadOnlyList<string> _lastLogTail = [];
    private readonly Dictionary<string, DateTimeOffset> _lineFirstSeen = new(StringComparer.Ordinal);
    private bool _filterStale;
    private string _severityFilter = "All";
    private string _categoryFilter = "All";
    private string _searchText = string.Empty;
    private bool _hideRoutineRest = true;
    private bool _paused;
    private string _viewState = "Live";

    public ConsoleLogViewModel(SessionContext session) : base(session, NavigationPage.Console)
    {
        PauseCommand = new RelayCommand(TogglePause);
        ClearCommand = new RelayCommand(ClearView);
    }

    /// <summary>The newest log tail, oldest first, each line with a timestamp in front.</summary>
    public BulkObservableCollection<string> LogLines { get; } = [];
    /// <summary>What the Console page shows: newest first, filtered. An export writes exactly this.</summary>
    public BulkObservableCollection<string> FilteredLogLines { get; } = [];

    public IReadOnlyList<string> SeverityOptions { get; } = ["All", "Errors", "Warnings", "Information"];
    public IReadOnlyList<string> CategoryOptions { get; } = ["All", "MystTiq", "PalServer", "REST", "Mods"];
    public string SeverityFilter { get => _severityFilter; set { if (SetField(ref _severityFilter, value ?? "All")) ApplyFilter(); } }
    public string CategoryFilter { get => _categoryFilter; set { if (SetField(ref _categoryFilter, value ?? "All")) ApplyFilter(); } }
    public string SearchText { get => _searchText; set { if (SetField(ref _searchText, value ?? string.Empty)) ApplyFilter(); } }
    public bool HideRoutineRest { get => _hideRoutineRest; set { if (SetField(ref _hideRoutineRest, value)) ApplyFilter(); } }
    public bool Paused { get => _paused; private set => SetField(ref _paused, value); }
    public string ViewState { get => _viewState; private set => SetField(ref _viewState, value); }

    public ICommand PauseCommand { get; }
    public ICommand ClearCommand { get; }

    /// <summary>Applies a log tail. Returns false when it is the tail already shown (v1.0.7.3: nothing then changes).</summary>
    public bool Apply(LogTailSnapshotDto snapshot)
    {
        if (snapshot.Lines.SequenceEqual(_lastLogTail)) return false;
        _lastLogTail = snapshot.Lines.ToArray();

        LogLines.ReplaceAll(snapshot.Lines.Select(line => EnsureTimestampFirst(line, snapshot.ObservedAt)).ToList());
        if (!Paused)
        {
            if (IsShown) ApplyFilter();
            else _filterStale = true;
        }
        if (_lineFirstSeen.Count > LineFirstSeenLimit)
        {
            var keep = snapshot.Lines.ToHashSet(StringComparer.Ordinal);
            foreach (var old in _lineFirstSeen.Keys.Where(k => !keep.Contains(k)).ToList()) _lineFirstSeen.Remove(old);
        }
        return true;
    }

    public override void OnShown() => EnsureFilterCurrent();

    /// <summary>The filtered view, brought up to date when it is needed (the page opens, or it is exported).</summary>
    public void EnsureFilterCurrent()
    {
        if (_filterStale && !Paused) ApplyFilter();
    }

    // v1.0.7.3 (roadmap Q-6): a line without its own timestamp is stamped with the time MystTiq first saw it, once. It used
    // to get each poll's time, so the same line showed a new time on every refresh.
    private string EnsureTimestampFirst(string line, DateTimeOffset observedAt)
    {
        if (string.IsNullOrWhiteSpace(line) || ConsoleTimestamp.IsMatch(line)) return line;
        if (!_lineFirstSeen.TryGetValue(line, out var seen)) _lineFirstSeen[line] = seen = observedAt;
        return $"[{seen.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}] {line}";
    }

    private void ApplyFilter()
    {
        if (Paused) return;
        _filterStale = false;
        var filtered = new List<string>();
        // v0.7.46.0: LogLines stays oldest-first (the Dashboard wants that); the Console page reads newest-first.
        foreach (var line in LogLines.Reverse())
        {
            if (HideRoutineRest && IsRoutineRestLine(line)) continue;
            if (!SeverityMatches(line, SeverityFilter)) continue;
            if (!CategoryMatches(line, CategoryFilter)) continue;
            if (!string.IsNullOrWhiteSpace(SearchText) && !line.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) continue;
            filtered.Add(line);
        }
        FilteredLogLines.ReplaceAll(filtered);
        ViewState = $"Live · {FilteredLogLines.Count}/{LogLines.Count} lines";
    }

    private static bool IsRoutineRestLine(string line) =>
        line.Contains("/api/v1/status/poll", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("/api/v1/metrics", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("/api/v1/players", StringComparison.OrdinalIgnoreCase);

    private static bool SeverityMatches(string line, string filter)
    {
        if (filter == "All") return true;
        var error = line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("FATAL", StringComparison.OrdinalIgnoreCase) || line.Contains("EXCEPTION", StringComparison.OrdinalIgnoreCase);
        var warning = line.Contains("WARN", StringComparison.OrdinalIgnoreCase);
        return filter switch { "Errors" => error, "Warnings" => warning, "Information" => !error && !warning, _ => true };
    }

    private static bool CategoryMatches(string line, string filter)
    {
        if (filter == "All") return true;
        var category = line.Contains("[MYSTTIQ]", StringComparison.OrdinalIgnoreCase) ? "MystTiq"
            : line.Contains("REST", StringComparison.OrdinalIgnoreCase) || line.Contains("/api/v1/", StringComparison.OrdinalIgnoreCase) ? "REST"
            : line.Contains("PalDefender", StringComparison.OrdinalIgnoreCase) || line.Contains("AdminCommands", StringComparison.OrdinalIgnoreCase) || line.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) ? "Mods"
            : "PalServer";
        return category.Equals(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void TogglePause()
    {
        Paused = !Paused;
        if (Paused) ViewState = $"Paused · {FilteredLogLines.Count} visible lines";
        else ApplyFilter();
    }

    private void ClearView()
    {
        FilteredLogLines.Clear();
        ViewState = "View cleared · server log was not deleted";
    }
}
