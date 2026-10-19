// MystTiq v1.1.1.1: file reviewed for this release (2026-10-18).
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;
using MystTiq.Core.Services;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Windows.Input;

namespace MystTiq.Desktop.ViewModels;

/// <summary>
/// v1.0.8.8 (roadmap C-8): the Update Center, Server Doctor, the Crash Analyzer and the Diagnostics Center (network,
/// firewall, UPnP and this computer's findings) -- moved out of MainWindowViewModel unchanged. The window shows them as
/// vm.Diagnostics; the environment checklist and the fleet's Doctor All stay with the window.
/// </summary>
public sealed class DiagnosticsViewModel : PageViewModel
{
    private Action<string?> Navigate { get; }
    private ModsViewModel Mods { get; }
    private Func<Task> UpdatePalworldServerAsync { get; }
    private Func<Task> UpdatePipAsync { get; }
    private Func<Task> RefreshAllInstancesAsync { get; }
    private Func<string> DistributionDetail { get; }

    public DiagnosticsViewModel(SessionContext session, Action<string?> navigate, ModsViewModel mods, Func<Task> updatePalworldServer, Func<Task> updatePip, Func<Task> refreshAllInstances, Func<string> distributionDetail) : base(session, NavigationPage.DiagnosticsCenter)
    {
        Navigate = navigate;
        Mods = mods;
        UpdatePalworldServerAsync = updatePalworldServer;
        UpdatePipAsync = updatePip;
        RefreshAllInstancesAsync = refreshAllInstances;
        DistributionDetail = distributionDetail;
        RefreshAntiCheatCommand = new AsyncCommand(RefreshAntiCheatAsync, () => !Session.IsBusy);
        SaveAntiCheatRulesCommand = new AsyncCommand(SaveAntiCheatRulesAsync, () => !Session.IsBusy);
        AnalyzeCrashesCommand = new AsyncCommand(AnalyzeCrashesAsync, () => !Session.IsBusy && Session.IsConnected);
        RefreshCrashHistoryCommand = new AsyncCommand(RefreshCrashHistoryAsync, () => !Session.IsBusy);
        RunDoctorCommand = new AsyncCommand(RunDoctorAsync, () => !Session.IsBusy);
        RecheckDiagnosticCommand = new RelayCommand<DiagnosticFindingDto>(f => { if (!Session.IsBusy) Session.RunUi(() => RecheckDiagnosticAsync(f)); });
        FixDiagnosticCommand = new RelayCommand<DiagnosticFindingDto>(f => { if (!Session.IsBusy && f is { CanFix: true, FixConfirmed: true }) Session.RunUi(() => FixDiagnosticAsync(f)); });
        RunNetworkDiagnosticsCommand = new AsyncCommand(RunNetworkDiagnosticsAsync, () => !Session.IsBusy);
        RepairFirewallCommand = new AsyncCommand(RepairFirewallAsync, () => !Session.IsBusy);
        RunWanReachabilityCommand = new AsyncCommand(RunWanReachabilityAsync, () => !Session.IsBusy);
        RepairUpnpMappingCommand = new AsyncCommand(RepairUpnpMappingAsync, () => !Session.IsBusy);
        RestartFromDiagnosticsCommand = new AsyncCommand(RestartFromDiagnosticsAsync, () => !Session.IsBusy && Session.IsConnected && Session.ServerIsRunning);
        UpdateComponentCommand = new RelayCommand<ComponentVersionDto>(UpdateComponent, component => component?.CanUpdate == true);
        ExportDoctorCommand = new RelayCommand(ExportDoctorReport, () => DoctorChecks.Count > 0);
        ToggleWanDiagnosticsCommand = new RelayCommand(() => IsWanDiagnosticsExpanded = !IsWanDiagnosticsExpanded);
        ToggleLocalDiagnosticsCommand = new RelayCommand(() => IsLocalDiagnosticsExpanded = !IsLocalDiagnosticsExpanded);
        ToggleAntiCheatCommand = new RelayCommand(() => IsAntiCheatExpanded = !IsAntiCheatExpanded);
    }

    internal readonly LocalDiagnosticsService _localDiagnostics = new();

    private string _doctorStatus = "Not run";

    private string _doctorSummary = "Connect to a MystTiq service, then run Doctor.";

    private string _doctorCheckedAt = "Never";

    private string _doctorExportPath = string.Empty;

    private DiagnosticsReportDto? _latestDiagnosticsReport;

    private string _diagnosticsReportDetail = string.Empty;

    private string _componentVersionsCheckedAtText = string.Empty;

    private string _networkHealth = "Not run";

    private string _localDiagnosticsState = "Diagnose a connection profile to see staged DNS/TCP/TLS/HTTP results.";

    private string _networkRuntime = "Unknown";

    private string _networkPort = "—";

    private string _networkBinding = "—";

    private string _networkLanEndpoint = "—";

    private string _networkRecommendation = "Run Diagnostics";

    private string _firewallStatusText = string.Empty;

    private bool _firewallAllowed;

    private bool _firewallNeedsAction;

    private string _firewallCommandsText = string.Empty;

    private string _networkReportText = "";

    private string _wanUpnpState = "Not checked";

    private string _antiCheatState = "Open Alert Center to load anti-cheat rules.";

    private AntiCheatRuleSetDto _antiCheatRules = new();

    private string _crashAnalyzerState = "Run analysis to inspect bounded recent server-log evidence.";

    private bool _isWanDiagnosticsExpanded;

    private bool _isLocalDiagnosticsExpanded;

    private bool _isAntiCheatExpanded;

    public ObservableCollection<CrashFindingDto> CrashFindings { get; } = [];

    // v0.7.97.0: the finding whose cause, fixes and evidence are shown beside the list.
    private CrashFindingDto? _selectedCrashFinding;

    public CrashFindingDto? SelectedCrashFinding
    {
        get => _selectedCrashFinding;
        set { if (SetField(ref _selectedCrashFinding, value)) RaisePropertyChanged(nameof(HasSelectedCrashFinding)); }
    }

    public bool HasSelectedCrashFinding => _selectedCrashFinding is not null;

    public ObservableCollection<string> CrashIsolationPlan { get; } = [];

    public ObservableCollection<CrashAnalysisSnapshotDto> CrashHistory { get; } = [];

    public ObservableCollection<DoctorCheckDto> DoctorChecks { get; } = [];

    public ObservableCollection<DiagnosticFindingDto> DiagnosticFindings { get; } = [];

    public ObservableCollection<NetworkDiagnosticCheckDto> NetworkDiagnosticChecks { get; } = [];

    public ObservableCollection<DiagnosticFindingDto> LocalConnectionFindings { get; } = [];

    public ObservableCollection<DiagnosticFindingDto> LocalMachineFindings { get; } = [];

    public string AntiCheatState { get => _antiCheatState; private set => SetField(ref _antiCheatState, value); }

    public AntiCheatRuleSetDto AntiCheatRules { get => _antiCheatRules; set => SetField(ref _antiCheatRules, value ?? new()); }

    public ObservableCollection<AntiCheatFindingDto> AntiCheatFindings { get; } = [];

    public IReadOnlyList<string> AntiCheatResponses { get; } = ["Flag", "Kick", "Ban"];

    public string DoctorStatus { get => _doctorStatus; private set => SetField(ref _doctorStatus, value); }

    public string DoctorSummary { get => _doctorSummary; private set => SetField(ref _doctorSummary, value); }

    public string DoctorCheckedAt { get => _doctorCheckedAt; private set => SetField(ref _doctorCheckedAt, value); }

    public string DoctorExportPath { get => _doctorExportPath; private set => SetField(ref _doctorExportPath, value); }

    public DiagnosticsReportDto? LatestDiagnosticsReport { get => _latestDiagnosticsReport; internal set => SetField(ref _latestDiagnosticsReport, value); }

    public string DiagnosticsReportDetail { get => _diagnosticsReportDetail; internal set => SetField(ref _diagnosticsReportDetail, value); }

    // v0.7.45.0: Update Center Overhaul -- split by Group to match the two labeled sections
    // (Core Server / Save & Runtime Dependencies) the v0.2.16.4 reference used.
    public ObservableCollection<ComponentVersionDto> CoreServerComponents { get; } = [];

    public ObservableCollection<ComponentVersionDto> SaveRuntimeDependencyComponents { get; } = [];

    public bool HasComponentVersions => CoreServerComponents.Count > 0 || SaveRuntimeDependencyComponents.Count > 0;

    public string ComponentVersionsCheckedAtText { get => _componentVersionsCheckedAtText; private set => SetField(ref _componentVersionsCheckedAtText, value); }

    public ICommand RefreshAntiCheatCommand { get; }

    public ICommand SaveAntiCheatRulesCommand { get; }

    public ICommand AnalyzeCrashesCommand { get; }

    public ICommand RefreshCrashHistoryCommand { get; }

    public ICommand RunDoctorCommand { get; }

    public ICommand RecheckDiagnosticCommand { get; }

    public ICommand FixDiagnosticCommand { get; }

    public ICommand ExportDoctorCommand { get; }

    public ICommand ToggleWanDiagnosticsCommand { get; }

    public ICommand ToggleLocalDiagnosticsCommand { get; }

    public ICommand ToggleAntiCheatCommand { get; }

    public bool IsWanDiagnosticsExpanded { get => _isWanDiagnosticsExpanded; private set => SetField(ref _isWanDiagnosticsExpanded, value); }

    public bool IsLocalDiagnosticsExpanded { get => _isLocalDiagnosticsExpanded; private set => SetField(ref _isLocalDiagnosticsExpanded, value); }

    public bool IsAntiCheatExpanded { get => _isAntiCheatExpanded; private set => SetField(ref _isAntiCheatExpanded, value); }

    public string CrashAnalyzerState { get => _crashAnalyzerState; internal set => SetField(ref _crashAnalyzerState, value); }

    public string NetworkHealth { get => _networkHealth; private set => SetField(ref _networkHealth, value); }

    public string LocalDiagnosticsState { get => _localDiagnosticsState; internal set => SetField(ref _localDiagnosticsState, value); }

    public string NetworkRuntime { get => _networkRuntime; private set => SetField(ref _networkRuntime, value); }

    public string NetworkPort { get => _networkPort; private set => SetField(ref _networkPort, value); }

    public string NetworkBinding { get => _networkBinding; private set => SetField(ref _networkBinding, value); }

    public string NetworkLanEndpoint { get => _networkLanEndpoint; private set => SetField(ref _networkLanEndpoint, value); }

    public string NetworkRecommendation { get => _networkRecommendation; private set => SetField(ref _networkRecommendation, value); }

    // v0.9.6.0: whether this server's game port is allowed through the firewall (shown on Diagnostics and in Settings).
    public string FirewallStatusText { get => _firewallStatusText; private set { if (SetField(ref _firewallStatusText, value)) RaisePropertyChanged(nameof(HasFirewallStatus)); } }

    public bool HasFirewallStatus => !string.IsNullOrWhiteSpace(FirewallStatusText);

    public bool FirewallAllowed { get => _firewallAllowed; private set => SetField(ref _firewallAllowed, value); }

    public bool FirewallNeedsAction { get => _firewallNeedsAction; private set => SetField(ref _firewallNeedsAction, value); }

    // Linux: the commands that open the port (MystTiq does not change a Linux firewall).
    public string FirewallCommandsText { get => _firewallCommandsText; private set { if (SetField(ref _firewallCommandsText, value)) RaisePropertyChanged(nameof(HasFirewallCommands)); } }

    public bool HasFirewallCommands => !string.IsNullOrWhiteSpace(FirewallCommandsText);

    public string NetworkReportText { get => _networkReportText; private set => SetField(ref _networkReportText, value); }

    public string WanUpnpState { get => _wanUpnpState; private set => SetField(ref _wanUpnpState, value); }

    public ICommand RunNetworkDiagnosticsCommand { get; }

    public ICommand RepairFirewallCommand { get; }

    public ICommand RepairUpnpMappingCommand { get; }

    public ICommand RestartFromDiagnosticsCommand { get; }

    internal async Task RunNetworkDiagnosticsAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var report = await Session.Api.GetNetworkDiagnosticsAsync(Session.SelectedProfile, Session.BearerToken);
            NetworkHealth = report.NetworkHealthText;
            NetworkRuntime = report.RuntimeHealth;
            NetworkPort = $"UDP {report.EffectiveGamePort}";
            NetworkBinding = report.BindingDisplay ?? "Not listening";
            NetworkLanEndpoint = report.LanEndpoint ?? "—";
            NetworkRecommendation = string.IsNullOrWhiteSpace(report.RecommendedAction) ? "No action required." : report.RecommendedAction;
            NetworkDiagnosticChecks.Clear();
            foreach (var check in report.Checks) NetworkDiagnosticChecks.Add(check);
            NetworkReportText = string.Join(Environment.NewLine, new[] {
                "MystTiq Palworld Server Diagnostics", $"Timestamp: {report.CheckedAt:O}",
                $"Runtime: {report.RuntimeHealth}", $"Network: {report.NetworkHealthText}",
                $"Configured game port: UDP {report.EffectiveGamePort}", $"PID: {report.PalServerProcessId?.ToString() ?? "—"}",
                $"Process: {report.PalServerProcessName ?? "—"}", $"Binding: {report.BindingDisplay ?? "—"}",
                $"LAN endpoint: {report.LanEndpoint ?? "—"}", "",
                string.Join(Environment.NewLine, report.Checks.Select(c => $"{c.Test}: {c.StateText} — {c.Details}"))
            });
        }
        catch (Exception ex) { NetworkHealth = "ERROR"; NetworkRecommendation = ex.Message; }
        finally { Session.IsBusy = false; }
        await RefreshFirewallStatusAsync();
    }

    private async Task RestartFromDiagnosticsAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.RestartFromNetworkDiagnosticsAsync(Session.SelectedProfile, Session.BearerToken);
            NetworkRecommendation = result.Success
                ? "Controlled restart completed; network diagnostics were re-run automatically."
                : result.Restart.Message ?? "Controlled restart did not complete successfully.";
            await RunNetworkDiagnosticsAsync();
        }
        catch (Exception ex) { NetworkRecommendation = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // v0.9.6.0: allows this server's game port (the one it binds) and removes its rules for a port it no longer uses.
    // When Windows refuses the service (not an administrator) and the server is on this computer, the same change runs
    // through Windows' administrator prompt instead.
    private async Task RepairFirewallAsync()
    {
        if (FirewallProfile() is not { } profile) return;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.RepairNetworkFirewallAsync(profile, Session.BearerToken);
            var message = result.Message;
            if (!result.Success && result.NeedsElevation && !string.IsNullOrWhiteSpace(result.Script))
            {
                if (ElevatedFirewall.CanRun(profile))
                {
                    FirewallStatusText = "Waiting for Windows to allow administrator rights…";
                    message = (await ElevatedFirewall.RunAsync(result.Script)).Message;
                }
                else message += " Run MystTiq's service as an administrator on the server's computer, or add the rule there yourself.";
            }

            NetworkRecommendation = message;
            await RefreshFirewallStatusAsync();
        }
        catch (Exception ex) { NetworkRecommendation = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // The wizard has no saved profile yet; it talks to the server through the one its editor builds.
    private ConnectionProfile? FirewallProfile()
    {
        if (Session.SelectedProfile is not null) return Session.SelectedProfile;
        // No saved profile: the window is creating one, and its editor's fields are the profile.
        try { return Session.BuildProfile(); }
        catch { return null; }
    }

    internal async Task RefreshFirewallStatusAsync()
    {
        if (FirewallProfile() is not { } profile) return;
        try
        {
            var status = await Session.Api.GetNetworkFirewallAsync(profile, Session.BearerToken);
            FirewallAllowed = status.Allowed && status.StaleRules.Count == 0;
            FirewallNeedsAction = status.Supported && status.Error is null && !status.Blocked && (!status.Allowed || status.StaleRules.Count > 0);
            FirewallCommandsText = status.Commands is { Count: > 0 } commands ? string.Join(Environment.NewLine, commands) : string.Empty;
            FirewallStatusText = status.Summary;
        }
        catch (Exception ex)
        {
            FirewallAllowed = false;
            FirewallNeedsAction = false;
            FirewallStatusText = $"The firewall could not be read: {ex.Message}";
        }
    }

    private async Task RepairUpnpMappingAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.RepairUpnpMappingAsync(Session.SelectedProfile, Session.BearerToken);
            WanStatus = result.Message;
            await RunWanReachabilityAsync();
        }
        catch (Exception ex) { WanStatus = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    internal async Task RefreshAntiCheatAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            AntiCheatRules = await Session.Api.GetAntiCheatRulesAsync(Session.SelectedProfile, Session.BearerToken);
            var findings = await Session.Api.GetAntiCheatFindingsAsync(Session.SelectedProfile, Session.BearerToken);
            AntiCheatFindings.Clear();
            foreach (var finding in findings) AntiCheatFindings.Add(finding);
            AntiCheatState = $"Anti-cheat rules loaded. {findings.Count} recent finding(s).";
        }
        catch (Exception ex) { AntiCheatState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task SaveAntiCheatRulesAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            AntiCheatRules = await Session.Api.SaveAntiCheatRulesAsync(Session.SelectedProfile, AntiCheatRules, Session.BearerToken);
            AntiCheatState = "Anti-cheat rules saved.";
        }
        catch (Exception ex) { AntiCheatState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task AnalyzeCrashesAsync()
    {
        if (Session.SelectedProfile is null) return; Session.IsBusy = true; CrashAnalyzerState = "Analyzing bounded recent-log evidence…";
        try { ApplyCrashAnalysis(await Session.Api.AnalyzeCrashesAsync(Session.SelectedProfile, Session.BearerToken)); await LoadCrashHistoryCoreAsync(); }
        catch (Exception ex) { CrashAnalyzerState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    internal async Task RefreshCrashHistoryAsync()
    {
        if (Session.SelectedProfile is null) return; Session.IsBusy = true;
        try { await LoadCrashHistoryCoreAsync(); }
        catch (Exception ex) { CrashAnalyzerState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task LoadCrashHistoryCoreAsync()
    {
        var history = await Session.Api.GetCrashHistoryAsync(Session.SelectedProfile!, Session.BearerToken);
        CrashHistory.Clear(); foreach (var row in history) CrashHistory.Add(row);
        if (history.FirstOrDefault() is { } latest) ApplyCrashAnalysis(latest);
        else CrashAnalyzerState = "No persisted crash analyses yet. Run Analysis to create the first evidence report.";
    }

    private void ApplyCrashAnalysis(CrashAnalysisSnapshotDto report)
    {
        CrashFindings.Clear(); foreach (var finding in report.Findings) CrashFindings.Add(finding);
        SelectedCrashFinding = CrashFindings.FirstOrDefault();
        CrashIsolationPlan.Clear(); foreach (var step in report.IsolationPlan) CrashIsolationPlan.Add(step);
        CrashAnalyzerState = $"{report.Summary} Scanned {report.FilesScanned} file(s) and {report.LinesScanned} line(s).";
    }

    public string ExportCrashAnalysis() => JsonSerializer.Serialize(new { state = CrashAnalyzerState, findings = CrashFindings, isolationPlan = CrashIsolationPlan, history = CrashHistory }, new JsonSerializerOptions { WriteIndented = true });

    internal async Task RunDoctorAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { DoctorSummary = ex.Message; return; }
        Session.IsBusy = true; DoctorStatus = "Running…";
        try
        {
            var report = await Session.Api.RunDoctorAsync(profile, Session.BearerToken);
            DoctorChecks.Clear();
            foreach (var check in report.Checks) DoctorChecks.Add(check);
            DoctorCheckedAt = report.CheckedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
            (ExportDoctorCommand as RelayCommand)?.RaiseCanExecuteChanged();

            // v0.6.4.0: the unified report supersedes the raw /doctor-only status/summary text
            // (DoctorChecks above stays populated too, for the existing export format) -- this is
            // what makes the Doctor page and the Dashboard badge agree, since both now read from
            // the same HeadlessDiagnosticsService output.
            LatestDiagnosticsReport = await Session.Api.GetDiagnosticsReportAsync(profile, Session.BearerToken);
            DiagnosticFindings.Clear();
            // v1.0.7.0 (Q-1): a finding opened from a notification is listed first and marked, once.
            var focus = _focusFindingId;
            _focusFindingId = null;
            foreach (var finding in LatestDiagnosticsReport.Findings
                         .OrderByDescending(f => focus is not null && f.Id.Equals(focus, StringComparison.OrdinalIgnoreCase)))
            {
                finding.IsFocused = focus is not null && finding.Id.Equals(focus, StringComparison.OrdinalIgnoreCase);
                DiagnosticFindings.Add(finding);
            }
            DoctorStatus = LatestDiagnosticsReport.OverallHealthText;
            // v0.7.30.0 bug fix: was just the raw pass/warn/fail counts, which left "UNKNOWN" (the
            // legitimate result when 17/17 pass but the server simply isn't running -- see
            // OverallHealthText) completely unexplained. The backend already computes exactly this
            // explanation (OverallHealthDetail, e.g. "Server is not running; no health issues
            // detected."); it was just never read on the Desktop side.
            DoctorSummary = $"{LatestDiagnosticsReport.Passed} passed · {LatestDiagnosticsReport.Warnings} warning(s) · {LatestDiagnosticsReport.Failures} failure(s)"
                + (string.IsNullOrWhiteSpace(LatestDiagnosticsReport.OverallHealthDetail) ? "" : $" — {LatestDiagnosticsReport.OverallHealthDetail}");
        }
        catch (Exception ex) { DoctorStatus = "Unavailable"; DoctorSummary = ex.Message; }
        finally { Session.IsBusy = false; }
        // v0.7.44.0: Run Doctor is the natural moment to also refresh the machine-wide instance
        // list -- same page, same "what's actually running" concern. Best-effort: a failure here
        // must not blank out the diagnostics report that already succeeded above.
        await RefreshAllInstancesAsync();
    }

    private async Task RecheckDiagnosticAsync(DiagnosticFindingDto? finding)
    {
        if (finding is null) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { DiagnosticsReportDetail = ex.Message; return; }

        Session.IsBusy = true;
        try
        {
            var updated = await Session.Api.RecheckDiagnosticFindingAsync(profile, finding.Id, Session.BearerToken);
            if (updated is null) { DiagnosticsReportDetail = $"{finding.Component}: finding no longer exists."; return; }
            var index = DiagnosticFindings.ToList().FindIndex(f => f.Id == updated.Id);
            if (index >= 0) DiagnosticFindings[index] = updated;
            DiagnosticsReportDetail = $"{updated.Component}: {updated.StateText}.";
        }
        catch (Exception ex) { DiagnosticsReportDetail = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task FixDiagnosticAsync(DiagnosticFindingDto? finding)
    {
        if (finding is null || !finding.CanFix || !finding.FixConfirmed) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { DiagnosticsReportDetail = ex.Message; return; }

        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.FixDiagnosticFindingAsync(profile, finding.Id, Session.BearerToken);
            DiagnosticsReportDetail = result.Message;
            if (result.Success) await RecheckDiagnosticAsync(finding);
        }
        catch (Exception ex) { DiagnosticsReportDetail = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private void ExportDoctorReport()
    {
        if (DoctorChecks.Count == 0) return;
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MystTiqReports");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"MystTiq-Doctor-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var lines = new List<string> { $"MystTiq {MainWindowViewModel.AppVersionText} Production Doctor", $"Status: {DoctorStatus}", $"Checked: {DoctorCheckedAt}", DoctorSummary, "" };
            foreach (var c in DoctorChecks) { lines.Add($"[{c.State}] {c.Component}"); lines.Add($"Evidence: {c.Evidence}"); lines.Add($"Recommendation: {c.Recommendation}"); lines.Add(""); }
            File.WriteAllLines(path, lines); DoctorExportPath = path; DoctorSummary = $"Diagnostic report exported: {path}";
        }
        catch (Exception ex) { DoctorSummary = ex.Message; }
    }

    internal async Task RefreshComponentVersionsAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch { return; }
        try
        {
            var snapshot = await Session.Api.GetComponentVersionsAsync(profile, Session.BearerToken);
            CoreServerComponents.Clear();
            SaveRuntimeDependencyComponents.Clear();
            foreach (var component in snapshot.Components)
            {
                if (string.Equals(component.Group, "Core Server", StringComparison.OrdinalIgnoreCase))
                    CoreServerComponents.Add(component);
                else
                    SaveRuntimeDependencyComponents.Add(component);
            }
            RaisePropertyChanged(nameof(HasComponentVersions));
            ComponentVersionsCheckedAtText = MainWindowViewModel.FormatTime(snapshot.ObservedAt);
        }
        catch { /* best-effort -- the existing DistributionState/Detail already surface connection problems */ }
    }

    // v1.0.7.0 (Q-1): a finding opened from a notification is listed first on the next Run Doctor.
    private string? _focusFindingId;
    internal void FocusFinding(string findingId) => _focusFindingId = findingId;

    private string _wanPublicIpPort = "Not checked";

    private string _wanRouterDescription = "—";

    private string _wanStatus = "Run Reachability Check";

    public string WanPublicIpPort { get => _wanPublicIpPort; private set => SetField(ref _wanPublicIpPort, value); }

    public string WanRouterDescription { get => _wanRouterDescription; private set => SetField(ref _wanRouterDescription, value); }

    public string WanStatus { get => _wanStatus; internal set => SetField(ref _wanStatus, value); }

    public ObservableCollection<WanReachabilityCheckDto> WanReachabilityChecks { get; } = [];

    // v0.6.11.0: the genuine gap left after local firewall inspection (above) and v0.6.4.0's Local
    // Machine Diagnostics -- neither tests whether the internet, or even the router, can actually
    // reach the configured game port. Public IP + UPnP mapping are real, automated checks; true
    // unsolicited-inbound UDP confirmation needs infrastructure we don't own, so that piece is a
    // manual hand-off (Copy IP:Port / Open Port Checker) rather than a faked pass/fail.
    private async Task RunWanReachabilityAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var report = await Session.Api.GetWanReachabilityAsync(Session.SelectedProfile, Session.BearerToken);
            WanPublicIpPort = report.PublicIpPortText;
            WanUpnpState = report.UpnpStateText;
            WanRouterDescription = (report.RouterDescription ?? "—") + (string.IsNullOrWhiteSpace(report.RouterWanIPv4) ? string.Empty : $" · internet address {report.RouterWanIPv4}");
            WanReachabilityChecks.Clear();
            foreach (var check in report.Checks) WanReachabilityChecks.Add(check);
            WanStatus = "Checked " + report.CheckedAt.ToLocalTime().ToString("t");
        }
        catch (Exception ex) { WanStatus = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    public ICommand RunWanReachabilityCommand { get; }

    // v1.0.1.0 (asked 2026-10-05: "the update page should have a button to update every option. It can be greyed out if it is
    // self updating, but should always have the option to update"): the Update button on every Update Center row. What it does
    // is the row's ComponentVersionDto.UpdateMethod; each reuses the app's existing path where there is one (SteamCMD for the
    // server files, the UE4SS page's preview-then-apply, pip) and the new ones are PalDefender, Palworld Save Tools and MystTiq.
    public ICommand UpdateComponentCommand { get; }

    private string _componentUpdateMessage = string.Empty;
    // The last Update's outcome, shown at the top of the component list.
    public string ComponentUpdateMessage
    {
        get => _componentUpdateMessage;
        private set { if (SetField(ref _componentUpdateMessage, value)) RaisePropertyChanged(nameof(HasComponentUpdateMessage)); }
    }
    public bool HasComponentUpdateMessage => !string.IsNullOrEmpty(ComponentUpdateMessage);

    private void UpdateComponent(ComponentVersionDto? component)
    {
        if (component is null || !component.CanUpdate) return;
        Session.RunUi(() => UpdateComponentAsync(component));
    }

    public async Task UpdateComponentAsync(ComponentVersionDto component)
    {
        if (Session.IsBusy)
        {
            ComponentUpdateMessage = "Another operation is running; try Update again when it has finished.";
            return;
        }
        switch (component.UpdateMethod)
        {
            case ComponentUpdateMethod.ServerFiles:
                ComponentUpdateMessage = $"Updating {component.Component}…";
                await UpdatePalworldServerAsync();
                ComponentUpdateMessage = DistributionDetail();
                await RefreshComponentVersionsAsync();
                break;
            case ComponentUpdateMethod.Ue4ssInstall:
                Navigate(nameof(NavigationPage.Ue4ss));
                await Mods.SelectAndPreviewLatestUe4ssAsync();
                break;
            case ComponentUpdateMethod.Pip:
                await UpdatePipAsync();
                ComponentUpdateMessage = DistributionDetail();
                break;
            case ComponentUpdateMethod.PalDefender:
                await RunComponentUpdateAsync("PalDefender", (profile, bearer) => Session.Api.UpdatePalDefenderAsync(profile, bearer));
                break;
            case ComponentUpdateMethod.SaveTools:
                await RunComponentUpdateAsync("Palworld Save Tools", (profile, bearer) => Session.Api.UpdateSaveToolsAsync(profile, bearer));
                break;
            case ComponentUpdateMethod.DownloadRelease:
                await DownloadMystTiqReleaseAsync(component.LatestVersion);
                break;
            case ComponentUpdateMethod.OfficialPage:
                if (component.SourceUrl is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                {
                    try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); ComponentUpdateMessage = $"Opened {url}"; }
                    catch (Exception ex) { ComponentUpdateMessage = ex.Message; }
                }
                break;
        }
    }

    private async Task RunComponentUpdateAsync(string name, Func<ConnectionProfile, string?, Task<ComponentUpdateResultDto>> update)
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { ComponentUpdateMessage = ex.Message; return; }

        Session.IsBusy = true;
        ComponentUpdateMessage = $"Updating {name}…";
        try
        {
            var result = await update(profile, Session.BearerToken);
            ComponentUpdateMessage = result.Message;
        }
        catch (Exception ex) { ComponentUpdateMessage = ex.Message; }
        finally { Session.IsBusy = false; }
        await RefreshComponentVersionsAsync();
    }

    private async Task DownloadMystTiqReleaseAsync(string version)
    {
        Session.IsBusy = true;
        ComponentUpdateMessage = $"Downloading MystTiq v{version}…";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MystTiq-Palworld-Server-Manager");
            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MystTiq", "versions");
            var progress = new Progress<string>(text => ComponentUpdateMessage = text);
            var result = await new MystTiqSelfUpdate(http).DownloadAsync(version, AppContext.BaseDirectory, fallback, progress, CancellationToken.None);
            ComponentUpdateMessage = result.Message;
            if (result.Success && result.Folder is { } folder && OperatingSystem.IsWindows())
            {
                try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true }); } catch { }
            }
        }
        catch (Exception ex) { ComponentUpdateMessage = $"MystTiq v{version} could not be downloaded: {ex.Message}"; }
        finally { Session.IsBusy = false; }
    }

    // MystTiq v1.0.0.1: startup-window console capture controls.
    private string _consoleCaptureState = "Not checked";
    private string _consoleCaptureDetail = string.Empty;
    private bool _consoleCapturePackagedAvailable;
    private bool _consoleCaptureInstalled;
    private System.Windows.Input.ICommand? _refreshConsoleCaptureCommand;
    private System.Windows.Input.ICommand? _installConsoleCaptureCommand;
    private System.Windows.Input.ICommand? _uninstallConsoleCaptureCommand;

    public string ConsoleCaptureState { get => _consoleCaptureState; private set => SetField(ref _consoleCaptureState, value); }
    public string ConsoleCaptureDetail { get => _consoleCaptureDetail; private set => SetField(ref _consoleCaptureDetail, value); }
    public bool ConsoleCapturePackagedAvailable
    {
        get => _consoleCapturePackagedAvailable;
        private set
        {
            if (!SetField(ref _consoleCapturePackagedAvailable, value)) return;
            RaisePropertyChanged(nameof(CanInstallConsoleCapture));
        }
    }
    public bool ConsoleCaptureInstalled
    {
        get => _consoleCaptureInstalled;
        private set
        {
            if (!SetField(ref _consoleCaptureInstalled, value)) return;
            RaisePropertyChanged(nameof(CanInstallConsoleCapture));
            RaisePropertyChanged(nameof(CanRemoveConsoleCapture));
        }
    }

    public bool CanInstallConsoleCapture => ConsoleCapturePackagedAvailable && !ConsoleCaptureInstalled;
    public bool CanRemoveConsoleCapture => ConsoleCaptureInstalled;

    public System.Windows.Input.ICommand RefreshConsoleCaptureCommand =>
        _refreshConsoleCaptureCommand ??= new AsyncCommand(RefreshConsoleCaptureAsync);
    public System.Windows.Input.ICommand InstallConsoleCaptureCommand =>
        _installConsoleCaptureCommand ??= new AsyncCommand(InstallConsoleCaptureAsync);
    public System.Windows.Input.ICommand UninstallConsoleCaptureCommand =>
        _uninstallConsoleCaptureCommand ??= new AsyncCommand(UninstallConsoleCaptureAsync);

    private async Task RefreshConsoleCaptureAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { ConsoleCaptureState = "Unavailable"; ConsoleCaptureDetail = ex.Message; return; }

        try { await RefreshConsoleCaptureStatusCoreAsync(profile); }
        catch (Exception ex) { ConsoleCaptureState = "Unavailable"; ConsoleCaptureDetail = ex.Message; }
    }

    internal async Task RefreshConsoleCaptureStatusCoreAsync(ConnectionProfile profile)
    {
        var status = await Session.Api.GetConsoleCaptureStatusAsync(profile, Session.BearerToken);
        ConsoleCapturePackagedAvailable = status.PackagedProxyAvailable;
        ConsoleCaptureInstalled = status.Installed;
        ConsoleCaptureState = status.Installed
            ? "Enabled — startup-window text is being captured"
            : status.PackagedProxyAvailable
                ? "Available — not enabled"
                : "Unavailable in this build";
        ConsoleCaptureDetail = status.Detail;
    }

    private async Task InstallConsoleCaptureAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { ConsoleCaptureState = "Install failed"; ConsoleCaptureDetail = ex.Message; return; }

        ConsoleCaptureState = "Enabling startup-window capture…";
        try
        {
            var result = await Session.Api.InstallConsoleCaptureAsync(profile, Session.BearerToken);
            ConsoleCaptureDetail = result.Detail;
            await RefreshConsoleCaptureStatusCoreAsync(profile);
        }
        catch (Exception ex)
        {
            ConsoleCaptureState = "Install failed";
            ConsoleCaptureDetail = ex.Message;
        }
    }

    private async Task UninstallConsoleCaptureAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { ConsoleCaptureState = "Remove failed"; ConsoleCaptureDetail = ex.Message; return; }

        ConsoleCaptureState = "Removing startup-window capture…";
        try
        {
            var result = await Session.Api.UninstallConsoleCaptureAsync(profile, Session.BearerToken);
            ConsoleCaptureDetail = result.Detail;
            await RefreshConsoleCaptureStatusCoreAsync(profile);
        }
        catch (Exception ex)
        {
            ConsoleCaptureState = "Remove failed";
            ConsoleCaptureDetail = ex.Message;
        }
    }
}
