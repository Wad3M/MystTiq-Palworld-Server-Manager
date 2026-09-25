using System.Collections.ObjectModel;
using System.Windows.Input;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.ViewModels;

// v0.8.17.0: the HOST tab -- the machine the connected server runs on (for a remote server, that machine, not this
// PC), and the server's process priority and eco mode. Kept in its own file; the rest of the ViewModel only routes to it.
public sealed partial class MainWindowViewModel
{
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

    public bool IsHostPage => SelectedPage == NavigationPage.Host;
    public bool IsV5HostCategory => SelectedPage == NavigationPage.Host;

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
        RefreshHostCommand = new AsyncCommand(() => RefreshHostAsync(silent: false), () => !IsBusy);
        SaveResourcePolicyCommand = new AsyncCommand(SaveResourcePolicyAsync, () => !IsBusy);
        InitializeBandwidthCommands();
    }

    private void RaiseHostCommandStates()
    {
        (RefreshHostCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (SaveResourcePolicyCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        RaiseBandwidthCommandStates();
    }

    // silent: the auto-refresh while the page is open -- no busy state, and errors only in the page's own status line.
    private async Task RefreshHostAsync(bool silent)
    {
        if (SelectedProfile is null || _hostRefreshRunning) return;
        _hostRefreshRunning = true;
        if (!silent) IsBusy = true;
        try
        {
            var snapshot = await _api.GetHostAsync(SelectedProfile, BearerToken);
            ApplyHostSnapshot(snapshot);
            // v0.8.20.0: the history with the first (not silent) load, then about once a minute.
            await MaybeRefreshHostHistoryAsync(force: !silent);
            HostStatusText = $"Read {snapshot.Host.ObservedAt.ToLocalTime():HH:mm:ss}. Updates every few seconds while this page is open.";
        }
        catch (Exception ex) { HostStatusText = "The machine could not be read: " + ex.Message; }
        finally
        {
            _hostRefreshRunning = false;
            if (!silent) IsBusy = false;
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
        if (SelectedProfile is null) return;
        if (!int.TryParse(EcoAfterEmptyMinutesText.Trim(), out var minutes) || minutes is < 1 or > 240)
        {
            ResourcePolicyStatusText = "Minutes before eco mode must be a whole number from 1 to 240.";
            return;
        }
        IsBusy = true;
        try
        {
            var result = await _api.SaveResourcePolicyAsync(SelectedProfile,
                new ResourcePolicyDto { Priority = PriorityKeys[SelectedPriorityIndex], EcoMode = EcoModeKeys[SelectedEcoModeIndex], EcoAfterEmptyMinutes = minutes,
                    Cores = string.IsNullOrWhiteSpace(CoresText) ? null : CoresText.Trim() },
                BearerToken);
            ResourcePolicyStatusText = result.Message;
            if (result.Success)
            {
                _resourcePolicyEdited = false;
                if (result.Snapshot is { } applied && _hostSnapshot is { } current)
                    ApplyHostSnapshot(new HostPageSnapshotDto { Host = current.Host, Resources = applied, Bandwidth = current.Bandwidth });
            }
        }
        catch (Exception ex) { ResourcePolicyStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    private static void Replace<T>(ObservableCollection<T> target, IReadOnlyList<T> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }
}
