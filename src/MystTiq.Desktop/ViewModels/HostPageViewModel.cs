// MystTiq v1.1.5.1: file reviewed for this release (2026-10-26).
using MystTiq.Desktop.Models;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace MystTiq.Desktop.ViewModels;

/// <summary>
/// v1.0.8.1 (roadmap C-1): the HOST page -- the machine the connected server runs on, the server's priority and eco
/// mode, the readings history and the network limits -- moved out of MainWindowViewModel unchanged (it was the Host,
/// HostHistory and Bandwidth partial files). The window shows it as <c>vm.Host</c> and refreshes it while the page is open.
/// </summary>
public sealed class HostPageViewModel : PageViewModel
{
    public HostPageViewModel(SessionContext session) : base(session, NavigationPage.Host) => InitializeHostCommands();

    // v0.8.17.0: the HOST tab -- the machine the connected server runs on (for a remote server, that machine, not this
    // PC), and the server's process priority and eco mode. Kept in its own file; the rest of the ViewModel only routes to it.
    public static readonly string[] PriorityKeys = ["Default", "BelowNormal", "Normal", "AboveNormal", "High"];
    public static readonly string[] EcoModeKeys = ["Off", "On", "WhenEmpty"];

    private HostPageSnapshotDto? _hostSnapshot;
    private string _hostStatusText = "Open this page to read the machine.";
    private int _selectedPriorityIndex;
    private int _selectedEcoModeIndex;
    private string _ecoAfterEmptyMinutesText = "10";
    private string _coresText = string.Empty;
    private string _resourcePolicyStatusText = string.Empty;
    // An edit not yet saved is not overwritten by the auto-refresh.
    private bool _resourcePolicyEdited;
    private bool _hostRefreshRunning;


    public ICommand RefreshHostCommand { get; private set; } = null!;
    public ICommand SaveResourcePolicyCommand { get; private set; } = null!;

    public ObservableCollection<HostDiskDto> HostDisks { get; } = [];
    public ObservableCollection<HostNetworkAdapterDto> HostNetwork { get; } = [];
    public ObservableCollection<ProcessResourceStateDto> HostProcesses { get; } = [];

    public IReadOnlyList<string> PriorityOptions { get; } =
        ["Leave as it is (default)", "Below normal", "Normal", "Above normal", "High"];
    public IReadOnlyList<string> EcoModeOptions { get; } =
        ["Off", "On", "When no one is online"];

    public bool HasHostData => _hostSnapshot is not null;
    public string HostStatusText { get => _hostStatusText; private set => SetField(ref _hostStatusText, value); }
    public string HostMachineText => _hostSnapshot is { } s ? $"{s.Host.MachineName} · {s.Host.OperatingSystem}" : "—";
    public string HostProcessorText => _hostSnapshot is { } s
        ? $"{(string.IsNullOrWhiteSpace(s.Host.ProcessorName) ? "Processor" : s.Host.ProcessorName)} · {s.Host.LogicalProcessors} logical processors"
        : "—";
    public double HostCpuPercent => _hostSnapshot?.Host.CpuPercent ?? 0;
    public string HostCpuText => _hostSnapshot?.Host.CpuPercent is { } cpu ? $"{cpu:0} % busy" : "—";
    public double HostMemoryPercent => _hostSnapshot is { Host.MemoryTotalBytes: > 0 } s
        ? 100d * (s.Host.MemoryTotalBytes - s.Host.MemoryAvailableBytes) / s.Host.MemoryTotalBytes : 0;
    public string HostMemoryText => _hostSnapshot is { Host.MemoryTotalBytes: > 0 } s
        ? $"{HostFormat.Bytes(s.Host.MemoryTotalBytes - s.Host.MemoryAvailableBytes)} used of {HostFormat.Bytes(s.Host.MemoryTotalBytes)} · {HostFormat.Bytes(s.Host.MemoryAvailableBytes)} available"
        : "—";
    public string HostUptimeText => _hostSnapshot is { } s ? "Up " + HostFormat.Uptime(s.Host.UptimeSeconds) : "—";

    public int SelectedPriorityIndex
    {
        get => _selectedPriorityIndex;
        set
        {
            if (value < 0 || value >= PriorityKeys.Length || value == _selectedPriorityIndex) return;
            _selectedPriorityIndex = value; _resourcePolicyEdited = true;
            RaisePropertyChanged(); RaisePropertyChanged(nameof(PriorityWarningText)); RaisePropertyChanged(nameof(HasPriorityWarning));
        }
    }

    public int SelectedEcoModeIndex
    {
        get => _selectedEcoModeIndex;
        set
        {
            if (value < 0 || value >= EcoModeKeys.Length || value == _selectedEcoModeIndex) return;
            _selectedEcoModeIndex = value; _resourcePolicyEdited = true;
            RaisePropertyChanged(); RaisePropertyChanged(nameof(IsEcoWhenEmpty));
        }
    }

    public string EcoAfterEmptyMinutesText
    {
        get => _ecoAfterEmptyMinutesText;
        set { if (SetField(ref _ecoAfterEmptyMinutesText, value)) _resourcePolicyEdited = true; }
    }

    // v0.8.24.0: processor cores ("0-3, 6"); empty = every core. The service checks the list against the machine.
    public string CoresText
    {
        get => _coresText;
        set { if (SetField(ref _coresText, value)) _resourcePolicyEdited = true; }
    }

    public string CoresHintText => _hostSnapshot?.Resources is { ProcessorCount: > 0 } r
        ? $"Leave empty for every core. This machine has cores 0-{r.ProcessorCount - 1}; write them like 0-3, 6. Pinning a server to fewer cores leaves the others to other servers and programs."
        : "Leave empty for every core; write them like 0-3, 6.";

    public bool IsEcoWhenEmpty => SelectedEcoModeIndex == 2;
    public bool HasPriorityWarning => SelectedPriorityIndex == 4;
    public string PriorityWarningText =>
        "High puts the server ahead of everything else on the machine, including MystTiq and remote access, so they can respond slowly while the server is busy.";

    public string ResourcePolicyStatusText { get => _resourcePolicyStatusText; private set => SetField(ref _resourcePolicyStatusText, value); }

    public string EcoStateText => _hostSnapshot?.Resources is not { } r ? "—"
        : !r.Running ? "The server is not running; the settings apply when it starts."
        : r.EcoActive ? "Eco mode is active. " + r.EcoReason
        : r.EcoReason.Contains("full speed", StringComparison.OrdinalIgnoreCase) ? r.EcoReason
        : "Full speed. " + r.EcoReason;
    public bool EcoActive => _hostSnapshot?.Resources.EcoActive == true;
    public string EfficiencyNoteText => _hostSnapshot?.Resources is { EfficiencyModeSupported: false }
        ? "This machine has no efficiency mode (it is a Windows feature): eco mode lowers the priority only. On Linux, going back to a higher priority needs the MystTiq service installed by v0.8.21.0 or later (run service-install again), or MystTiq running as root."
        : "Eco mode switches on the operating system's efficiency mode for the server and runs it below normal priority: it uses less power and leaves the processor to other work, but the server may run less smoothly.";
    public string ResourceErrorText => _hostSnapshot?.Resources.LastError ?? string.Empty;
    public bool HasResourceError => !string.IsNullOrWhiteSpace(ResourceErrorText);

    private void InitializeHostCommands()
    {
        RefreshHostCommand = new AsyncCommand(() => RefreshHostAsync(silent: false), () => !Session.IsBusy);
        SaveResourcePolicyCommand = new AsyncCommand(SaveResourcePolicyAsync, () => !Session.IsBusy);
        InitializeBandwidthCommands();
    }

    internal void RaiseHostCommandStates()
    {
        (RefreshHostCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (SaveResourcePolicyCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        RaiseBandwidthCommandStates();
    }

    // silent: the auto-refresh while the page is open -- no busy state, and errors only in the page's own status line.
    internal async Task RefreshHostAsync(bool silent)
    {
        if (Session.SelectedProfile is null || _hostRefreshRunning) return;
        _hostRefreshRunning = true;
        if (!silent) Session.IsBusy = true;
        try
        {
            var snapshot = await Session.Api.GetHostAsync(Session.SelectedProfile, Session.BearerToken);
            ApplyHostSnapshot(snapshot);
            // v0.8.20.0: the history with the first (not silent) load, then about once a minute.
            await MaybeRefreshHostHistoryAsync(force: !silent);
            HostStatusText = $"Read {snapshot.Host.ObservedAt.ToLocalTime():HH:mm:ss}. Updates every few seconds while this page is open.";
        }
        catch (Exception ex) { HostStatusText = "The machine could not be read: " + ex.Message; }
        finally
        {
            _hostRefreshRunning = false;
            if (!silent) Session.IsBusy = false;
        }
    }

    private void ApplyHostSnapshot(HostPageSnapshotDto snapshot)
    {
        _hostSnapshot = snapshot;
        Replace(HostDisks, snapshot.Host.Disks);
        Replace(HostNetwork, snapshot.Host.Network);
        Replace(HostProcesses, snapshot.Resources.Processes);
        if (!_resourcePolicyEdited) LoadPolicyFields(snapshot.Resources.Policy);
        ApplyBandwidthSnapshot(snapshot.Bandwidth);
        foreach (var name in new[]
                 {
                     nameof(HasHostData), nameof(HostMachineText), nameof(HostProcessorText), nameof(HostCpuPercent), nameof(HostCpuText),
                     nameof(HostMemoryPercent), nameof(HostMemoryText), nameof(HostUptimeText), nameof(EcoStateText), nameof(EcoActive),
                     nameof(EfficiencyNoteText), nameof(ResourceErrorText), nameof(HasResourceError), nameof(CoresHintText)
                 })
            RaisePropertyChanged(name);
    }

    private void LoadPolicyFields(ResourcePolicyDto policy)
    {
        _selectedPriorityIndex = Math.Max(0, Array.IndexOf(PriorityKeys, policy.Priority));
        _selectedEcoModeIndex = Math.Max(0, Array.IndexOf(EcoModeKeys, policy.EcoMode));
        _ecoAfterEmptyMinutesText = policy.EcoAfterEmptyMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _coresText = policy.Cores ?? string.Empty;
        _resourcePolicyEdited = false;
        foreach (var name in new[] { nameof(SelectedPriorityIndex), nameof(SelectedEcoModeIndex), nameof(EcoAfterEmptyMinutesText), nameof(CoresText), nameof(IsEcoWhenEmpty),
                     nameof(HasPriorityWarning), nameof(PriorityWarningText) })
            RaisePropertyChanged(name);
    }

    private async Task SaveResourcePolicyAsync()
    {
        if (Session.SelectedProfile is null) return;
        if (!int.TryParse(EcoAfterEmptyMinutesText.Trim(), out var minutes) || minutes is < 1 or > 240)
        {
            ResourcePolicyStatusText = "Minutes before eco mode must be a whole number from 1 to 240.";
            return;
        }
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.SaveResourcePolicyAsync(Session.SelectedProfile,
                new ResourcePolicyDto { Priority = PriorityKeys[SelectedPriorityIndex], EcoMode = EcoModeKeys[SelectedEcoModeIndex], EcoAfterEmptyMinutes = minutes,
                    Cores = string.IsNullOrWhiteSpace(CoresText) ? null : CoresText.Trim() },
                Session.BearerToken);
            ResourcePolicyStatusText = result.Message;
            if (result.Success)
            {
                _resourcePolicyEdited = false;
                if (result.Snapshot is { } applied && _hostSnapshot is { } current)
                    ApplyHostSnapshot(new HostPageSnapshotDto { Host = current.Host, Resources = applied, Bandwidth = current.Bandwidth });
            }
        }
        catch (Exception ex) { ResourcePolicyStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private static void Replace<T>(ObservableCollection<T> target, IReadOnlyList<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    // v0.8.20.0: the HOST tab's history card -- the machine's processor, memory and busiest adapter over the last hour,
    // day or week. Loaded when the page opens, when the range changes, and about once a minute with the 5-second tick
    // (a new reading is taken once a minute).
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
        if (Session.SelectedProfile is null) return;
        try
        {
            _hostHistory = await Session.Api.GetHostHistoryAsync(Session.SelectedProfile, HistoryRangeHours[HostHistoryRangeIndex], Session.BearerToken);
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

    // v0.8.18.0: the HOST tab's bandwidth card -- the server's network limits in its Engine.ini (per-player rate and network
    // updates per second), MystTiq's policy for them, and what they mean for the connection's upload.
    private BandwidthSnapshotDto? _bandwidth;
    private int _bandwidthModeIndex;
    private string _perPlayerMbpsText = "8";
    private string _tickRateText = "60";
    private string _uploadBudgetMbpsText = string.Empty;
    private string _bandwidthStatusText = string.Empty;
    private bool _bandwidthEdited;

    public ICommand SaveNetworkPolicyCommand { get; private set; } = null!;
    public ICommand ApplyBandwidthSuggestionCommand { get; private set; } = null!;

    public IReadOnlyList<string> BandwidthModeOptions { get; } = ["The game's own limits (default)", "My limits"];

    public int BandwidthModeIndex
    {
        get => _bandwidthModeIndex;
        set
        {
            if (value is < 0 or > 1 || value == _bandwidthModeIndex) return;
            _bandwidthModeIndex = value; _bandwidthEdited = true;
            RaisePropertyChanged(); RaisePropertyChanged(nameof(IsBandwidthCustom));
        }
    }
    public bool IsBandwidthCustom => BandwidthModeIndex == 1;
    public string PerPlayerMbpsText { get => _perPlayerMbpsText; set { if (SetField(ref _perPlayerMbpsText, value)) _bandwidthEdited = true; } }
    public string TickRateText { get => _tickRateText; set { if (SetField(ref _tickRateText, value)) _bandwidthEdited = true; } }
    public string UploadBudgetMbpsText { get => _uploadBudgetMbpsText; set { if (SetField(ref _uploadBudgetMbpsText, value)) _bandwidthEdited = true; } }
    public string BandwidthStatusText { get => _bandwidthStatusText; private set => SetField(ref _bandwidthStatusText, value); }

    public string BandwidthDefaultsText => _bandwidth is not { } b ? "—"
        : $"The game's own limits on this {b.Platform} server: {Mbps(b.GameDefaultPerPlayerMbps)} per player and {b.GameDefaultTickRate} network updates per second.";

    public string BandwidthNowText => _bandwidth is not { } b ? "—"
        : b.EngineMaxClientRate is null && b.EngineMaxInternetClientRate is null && b.EngineTickRate is null
            ? "Engine.ini sets no network limits: the game's own apply."
            : $"Engine.ini now: {Mbps(b.EffectivePerPlayerMbps)} per player and {b.EffectiveTickRate} network updates per second"
              + (b.Policy.Mode == "Custom" && b.PolicyInEngineIni ? " (MystTiq's limits)." : " (set outside MystTiq).");

    public string BandwidthWorstCaseText => _bandwidth is not { } b ? string.Empty
        : b.MaxPlayers is not { } players || b.WorstCaseUploadMbps is not { } worst
            ? "The maximum player count could not be read from PalWorldSettings.ini, so the most this server could send is not known."
            : $"With all {players} players at {Mbps(b.EffectivePerPlayerMbps)} each, the server could send up to {Mbps(worst)}."
              + (b.Policy.UploadBudgetMbps is { } up ? $" Your upload: {Mbps(up)}." : string.Empty);
    public bool BandwidthOverBudget => _bandwidth?.OverUploadBudget == true;
    public string BandwidthOverBudgetText =>
        "That is more than 80 % of your upload: a full server could fill it and everyone would lag. A lower limit per player keeps room.";
    public string BandwidthSuggestionText => _bandwidth?.SuggestedPerPlayerMbps is { } s && _bandwidth.MaxPlayers is { } p
        ? $"To fit {p} players into 80 % of your upload: {Mbps(s)} per player." : string.Empty;
    public bool HasBandwidthSuggestion => _bandwidth?.SuggestedPerPlayerMbps is not null;
    public bool BandwidthRestartNeeded => _bandwidth?.RestartNeeded == true;
    public string BandwidthRestartText => "Saved, but the server is running: it takes effect at the next start. Restart the server to apply it now.";

    private void InitializeBandwidthCommands()
    {
        SaveNetworkPolicyCommand = new AsyncCommand(SaveNetworkPolicyAsync, () => !Session.IsBusy);
        ApplyBandwidthSuggestionCommand = new RelayCommand(() =>
        {
            if (_bandwidth?.SuggestedPerPlayerMbps is not { } s) return;
            BandwidthModeIndex = 1;
            PerPlayerMbpsText = s.ToString("0.##", CultureInfo.InvariantCulture);
        });
    }

    private void RaiseBandwidthCommandStates() => (SaveNetworkPolicyCommand as AsyncCommand)?.RaiseCanExecuteChanged();

    private void ApplyBandwidthSnapshot(BandwidthSnapshotDto snapshot)
    {
        _bandwidth = snapshot;
        if (!_bandwidthEdited)
        {
            _bandwidthModeIndex = snapshot.Policy.Mode == "Custom" ? 1 : 0;
            _perPlayerMbpsText = snapshot.Policy.PerPlayerMbps.ToString("0.##", CultureInfo.InvariantCulture);
            _tickRateText = snapshot.Policy.TickRate.ToString(CultureInfo.InvariantCulture);
            _uploadBudgetMbpsText = snapshot.Policy.UploadBudgetMbps?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
            foreach (var name in new[] { nameof(BandwidthModeIndex), nameof(IsBandwidthCustom), nameof(PerPlayerMbpsText), nameof(TickRateText), nameof(UploadBudgetMbpsText) })
                RaisePropertyChanged(name);
        }
        foreach (var name in new[] { nameof(BandwidthDefaultsText), nameof(BandwidthNowText), nameof(BandwidthWorstCaseText), nameof(BandwidthOverBudget),
                     nameof(BandwidthSuggestionText), nameof(HasBandwidthSuggestion), nameof(BandwidthRestartNeeded) })
            RaisePropertyChanged(name);
    }

    // Both "2.5" and this computer's own decimal separator are accepted.
    public static bool TryReadNumber(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value);

    private async Task SaveNetworkPolicyAsync()
    {
        if (Session.SelectedProfile is null) return;
        if (!TryReadNumber(PerPlayerMbpsText, out var perPlayer) || perPlayer is < 0.25 or > 100)
        { BandwidthStatusText = "The limit per player must be a number from 0.25 to 100 Mbit/s."; return; }
        if (!int.TryParse(TickRateText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tick) || tick is < 10 or > 120)
        { BandwidthStatusText = "Network updates per second must be a whole number from 10 to 120."; return; }
        double? budget = null;
        if (!string.IsNullOrWhiteSpace(UploadBudgetMbpsText))
        {
            if (!TryReadNumber(UploadBudgetMbpsText, out var up) || up < 1) { BandwidthStatusText = "Your upload speed must be a number of at least 1 Mbit/s, or left empty."; return; }
            budget = up;
        }
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.SaveNetworkPolicyAsync(Session.SelectedProfile,
                new NetworkPolicyDto { Mode = IsBandwidthCustom ? "Custom" : "GameDefault", PerPlayerMbps = perPlayer, TickRate = tick, UploadBudgetMbps = budget },
                Session.BearerToken);
            BandwidthStatusText = result.Message;
            if (result.Success)
            {
                _bandwidthEdited = false;
                if (result.Snapshot is { } applied) ApplyBandwidthSnapshot(applied);
            }
        }
        catch (Exception ex) { BandwidthStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private static string Mbps(double value) => value.ToString(value >= 10 ? "0" : "0.##", CultureInfo.InvariantCulture) + " Mbit/s";
}
