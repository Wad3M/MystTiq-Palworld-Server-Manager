// MystTiq v1.0.8.6: file reviewed for this release (2026-10-12).
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using MystTiq.Core.Services;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IMystTiqApiClient _api;
    private readonly IConnectionProfileStore _profileStore;
    private readonly CredentialStore _credentialStore;
    private readonly TabSessionStore _tabSessionStore;
    private readonly ILocalInstallationDiscoveryService _localDiscovery;
    private readonly IMystTiqServiceDiscoveryService _serviceDiscovery;
    private readonly ILocalManagementBootstrapper _localBootstrapper;
    private readonly LocalDiagnosticsService _localDiagnostics = new();
    // v0.8.16.0: density (every tab); the theme mode stays per tab, in ConnectionProfile.ThemeVariant.
    private readonly LocalDisplayPreferencesStore _displayPreferences;
    private string? _busyReason;
    // v0.7.43.0: findings-completeness fix (item 6) -- the footer used to show only a static
    // reason string with no elapsed time and no indication of which server it was for. Captured
    // together whenever a busy operation starts, cleared together when it ends.
    private DateTimeOffset? _busyStartedAt;
    private string? _busyServerName;
    private string _busyElapsedText = string.Empty;
    private readonly DispatcherTimer _busyElapsedTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    // v0.7.79.0: theme is now per-tab (read/written through ActiveTab.Profile.AccentTheme/
    // ThemeVariant -- see SelectedAccentTheme/IsLightMode below), so the app-wide
    // LocalThemePreferencesStore fields this class used to hold are gone. App.axaml.cs still loads
    // it once, before this class even exists, purely to paint something reasonable on the very
    // first frame before any profile's own theme is known a few milliseconds later.
    private TabSession? _activeTab;
    private bool _hasOverflowTabs;
    private double _tabStripWidth = 720;
    private bool _hasOverflowRibbonGroups;
    private double _ribbonWidth = 620;
    private List<RibbonGroupViewModel> _allRibbonGroups = [];

    private NavigationPage _selectedPage = NavigationPage.Dashboard;

    private string _serverState = "Unknown";
    private string _startupStageText = string.Empty;
    public string StartupStageText { get => _startupStageText; private set { if (SetField(ref _startupStageText, value)) RaisePropertyChanged(nameof(HasStartupStage)); } }
    public bool HasStartupStage => StartupStageText.Length > 0;
    private string _serviceState = "Unknown";
    private string _detail = "Choose a connection profile or configure a secured remote MystTiq service.";
    private string _nativePidText = "—";
    private string _listenerText = "—";
    private string _lastObservedText = "Never";
    private string _lastTransitionText = "Unknown";
    private string _uptimeText = "—";
    private bool _autoRefreshEnabled = true;
    private int _refreshTick;
    private string _playersState = "Not sampled";
    private string _onlinePlayerCountText = "—";
    private string _cpuText = "—";
    private string _serverFpsText = "—";
    private string _serverFrameTimeText = "—";
    private string _memoryText = "—";
    private string _threadCountText = "—";
    private string _monitoringDetail = "Connect to a MystTiq service to begin monitoring.";
    private string _logFileText = "No log selected";
    private string _rconState = "Not checked";
    private string _rconDetail = "RCON is deprecated by Palworld; use it only when required by a legacy workflow.";
    private string _rconCommandText = "Info";
    private string _selectedRconPreset = "Info";
    private bool _rconConnected;

    private string _doctorStatus = "Not run";
    private string _doctorSummary = "Connect to a MystTiq service, then run Doctor.";
    private string _doctorCheckedAt = "Never";
    private string _doctorExportPath = string.Empty;
    private DiagnosticsReportDto? _latestDiagnosticsReport;
    private string _diagnosticsReportDetail = string.Empty;
    private string _distributionState = "Not checked";
    private string _distributionDetail = "Connect to inspect Palworld server installation.";
    private string _componentVersionsCheckedAtText = string.Empty;
    private string _steamCmdState = "Unknown";
    private string _serverInstallState = "Unknown";
    private string _distributionPlatform = "—";
    private string _distributionPlanText = string.Empty;
    private string _distributionOutputText = string.Empty;
    private bool _validateServerFiles = true;
    private string _semanticStateText = "—";
    private string _semanticSourceText = "—";
    private PlayerSnapshotDto? _selectedOnlinePlayer;
    private ServerInstanceDto? _selectedInstance;
    private string _instanceTerminationResultText = string.Empty;
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
    private string _wanPublicIpPort = "Not checked";
    private string _wanUpnpState = "Not checked";
    private string _wanRouterDescription = "—";
    private string _wanStatus = "Run Reachability Check";
    private string _localServiceStatus = "Unknown";
    private string _localPalServerStatus = "Unknown";
    private string _localApiStatus = "Unknown";
    private string _localServerRootText = "Not discovered";
    private string _localConfigPathText = "Not discovered";
    private string _localDiscoverySource = "Not run";
    private string _localDiscoveryDetail = "Local installation discovery has not run.";
    private string _dashboardHealthText = "IDLE";
    private double _cpuPercentValue;
    private double _memoryMbValue;
    private double _memoryScaleValue;
    private string _statusBarText = "Ready";
    private string _statusBarObservedText = "Not sampled";
    private string _discoveryStateText = "LAN discovery has not run.";
    private string _discoveryRangesText = string.Empty;
    private double _discoveryProgressValue;
    private bool _isDiscovering;
    private CancellationTokenSource? _discoveryCancellation;
    private string _discoverySkippedText = string.Empty;
    private string _discoveryProblemsText = string.Empty;
    private bool _discoveryIncludeVirtualAdapters;
    private string _discoveryExtraRanges = string.Empty;
    private DiscoveredMystTiqService? _selectedDiscoveredService;
    private string _lifecycleStatusText = "No lifecycle operation has been requested.";
    private string _activityState = "Not loaded";
    private string _activityFileText = "No activity log selected";
    private string _activityDetail = "Open Activity & Audit to load persistent manager events.";
    private readonly List<string> _allActivityLines = [];
    private string _activitySearchText = string.Empty;
    private string _activitySeverityFilter = "All";
    private string _activityCategoryFilter = "All";
    private string _notificationState = "Open Notifications to load persistent messages.";
    private string _notificationSearchText = string.Empty;
    private string _notificationSeverityFilter = "All";
    private NotificationItemDto? _selectedNotification;
    private string _automationState = "Open Automation to load scheduled rules.";
    private AutomationRuleDto? _selectedAutomationRule;
    private string _newAutomationRuleName = string.Empty;
    private string _newAutomationTriggerKind = "DailyTime";
    private string _newAutomationTimeOfDayUtc = "03:00";
    private int _newAutomationIntervalMinutes = 60;
    private int _newAutomationIdleThresholdMinutes = 30;
    private string _newAutomationActionKind = "CreateBackup";
    private string _newAutomationNotificationTitle = string.Empty;
    private string _newAutomationNotificationMessage = string.Empty;
    private string _newAutomationRconCommand = string.Empty;
    private string _securityState = "Open Security to load API principals.";
    private MystTiqPrincipalDto? _selectedPrincipal;
    private MystTiqPrincipalDto? _currentPrincipal;
    private string _newPrincipalName = string.Empty;
    private string _newPrincipalRole = "Operator";
    private string? _lastCreatedPrincipalToken;
    private string _alertCenterState = "Open Alert Center to load threshold rules.";
    private AlertRuleSetDto _alertRules = new();
    private DiskSpacePredictionDto? _diskSpacePrediction;
    private string _discordBotState = "Open Alert Center to load Discord bot configuration.";
    private DiscordBotConfigurationDto _discordBotConfig = new();
    private bool _discordBotTokenConfigured;
    private string _discordBotConnectionState = "NotConfigured";
    private string _newRoleMappingDiscordRoleId = string.Empty;
    private string _newRoleMappingRole = "Viewer";
    private DiscordRoleMappingDto? _selectedRoleMapping;
    private string _antiCheatState = "Open Alert Center to load anti-cheat rules.";
    private AntiCheatRuleSetDto _antiCheatRules = new();
    private string _fleetState = "Open Fleet to list configured server profiles.";
    private double _serverSetupTableMaxHeight = 405;
    private string _cloneNewProfileId = string.Empty;
    private string _cloneNewProfileName = string.Empty;
    private string _cloneWorldStatusText = "Clones this connection's server (binaries + world) into a new profile. Requires the source server to be stopped.";
    private bool _cloneWorldNeedsRestart;
    private string? _cloneWorldNewProfileId;
    private int _cloneWorldPortOffset = 100;
    private bool _cloneWorldConfirmed;
    private string _crashAnalyzerState = "Run analysis to inspect bounded recent server-log evidence.";
    private bool _isWanDiagnosticsExpanded;
    private bool _isLocalDiagnosticsExpanded;
    private bool _isDiscordBotExpanded;
    private bool _isAntiCheatExpanded;
    private string _environmentHealthText = "Checking…";
    private string _setupOperationState = "IDLE";
    private string _setupOperationTitle = "No operation currently running";
    private string _setupOperationDetail = "Select an operation above or use an action in the component checklist.";
    private string _setupRecentActivity = "Recent activity: None during this session.";
    private double _setupOperationProgress;
    private string _setupServerName = "My Palworld Server";
    private string _setupServerDescription = "A Palworld server managed by MystTiq Palworld Server";
    private string _setupAdminPassword = string.Empty;
    private string _setupServerPassword = string.Empty;
    private string _setupMaximumPlayers = "32";
    private string _setupGamePort = "8211";
    private string _setupRestPort = "8212";
    private string _setupGamePortWarningText = string.Empty;
    private string _setupRestPortWarningText = string.Empty;
    private bool _setupCreateConfirmed;
    private string _selectedHistoryRange = "1 Hour";
    private string _historyCpuSummary = "CPU history collecting…";
    private string _historyMemorySummary = "Memory history collecting…";
    private string _historyFpsSummary = "FPS history collecting…";
    private string _historySampleSummary = "0 samples";
    private string _historyStatusText = "Collecting";
    private DateTimeOffset _lastHistoryRefresh = DateTimeOffset.MinValue;

    public MainWindowViewModel(IMystTiqApiClient api, IConnectionProfileStore profileStore, ILocalInstallationDiscoveryService localDiscovery, IMystTiqServiceDiscoveryService serviceDiscovery, ILocalManagementBootstrapper? localBootstrapper = null, LocalThemePreferencesStore? themeStore = null, CredentialStore? credentialStore = null, TabSessionStore? tabSessionStore = null, INexusModsClient? nexusClient = null, LocalDisplayPreferencesStore? displayPreferencesStore = null)
    {
        _api = api;
        // v1.0.8.0 (roadmap C-0): the shared session the page ViewModels use, and the pages moved out of this file so far.
        Session = new SessionContext(api, ActiveProfileOrNull, () => BearerToken, () => IsBusy, busy => IsBusy = busy, () => SelectedPage, RunUi, () => SelectedProfile, () => ActiveTab, () => BuildProfileFromEditor(SelectedProfile?.Id), () => ManagementApiConnected,
            () => BusyReason, reason => BusyReason = reason);
        // v1.0.8.6: the window's services first: a page may be handed one.
        _nexus = nexusClient ?? new NexusModsClient();
        _profileStore = profileStore;
        _localDiscovery = localDiscovery;
        _serviceDiscovery = serviceDiscovery;
        _localBootstrapper = localBootstrapper ?? new LocalManagementBootstrapper();
        _credentialStore = credentialStore ?? new CredentialStore();
        Logs = new ConsoleLogViewModel(Session);
        Host = new HostPageViewModel(Session);
        Dashboard = new DashboardViewModel(Session);
        Players = new PlayersViewModel(Session, RefreshActivityAsync, RefreshPlayersPageAsync);
        Map = new MapViewModel(Session, Players);
        World = new WorldViewModel(Session, Map, Players, RefreshOperationsAsync, () => Workspace!.ValidateActiveWorldAsync(), RefreshPalsAsync, RefreshPlayersPageAsync, RefreshWorldExplorerAsync, RefreshPlayerGuildExplorerAsync, OpenSelectedGuildLeaderAsync);
        Configuration = new ConfigurationViewModel(Session, state => Workspace!.WorkspaceState = state);
        Workspace = new WorkspaceViewModel(Session, Configuration, EnvironmentItems);
        Backups = new BackupsViewModel(Session);
        Mods = new ModsViewModel(Session, _nexus, _credentialStore, Navigate);
        _pages = [Logs, Host, Dashboard, Players, Map, World, Configuration, Workspace, Backups, Mods];
        // v1.0.8.6 (roadmap C-6): the Dashboard's stuck-start panel shows Safe Start while it runs and its result afterwards.
        Mods.SafeStartStatusChanged += () =>
        {
            RaisePropertyChanged(nameof(ShowStuckStartPanel));
            RaisePropertyChanged(nameof(IsModSafeStartRunning));
            RaisePropertyChanged(nameof(StuckStartStatusText));
            (BeginStuckTestLoadCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        };
        // v1.0.8.4 (roadmap C-4): the Workspace page and the wizard follow the Configuration page.
        Configuration.ConfigurationLoaded += () => { Workspace.ValidateWorkspacePaths(); Workspace.RaiseWorkspaceSummaryProperties(); };
        Configuration.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ConfigurationViewModel.PalworldConfigLoaded)) return;
            (GenerateServerNameCommand as RelayCommand)?.RaiseCanExecuteChanged();
            Workspace.RaiseWorkspaceSummaryProperties();
        };
        // The map's teleport buttons follow the Players page's selected player.
        Players.CommandStatesChanged += () =>
        {
            (Map.TeleportPlayerToMeCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Map.TeleportToPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        };
        _displayPreferences = displayPreferencesStore ?? new LocalDisplayPreferencesStore();
        ThemeApplier.ApplyDensity(_displayPreferences.LoadDensity());
        // v0.8.16.0: a tab set to follow the system changes with it (Windows' app mode, the Linux desktop's color scheme).
        if (Avalonia.Application.Current?.PlatformSettings is { } platformSettings)
            platformSettings.ColorValuesChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(OnSystemThemeChanged);
        // v1.0.5.0 (roadmap M-1): the MOD browser.
        // v1.0.6.0 (roadmap S-3): the Players page's Pal box.
        // v0.8.5.0: the saved display language, before anything is shown.
        Localizer.Instance.SetLanguage(_languageStore.Load());
        Localizer.Instance.LanguageChanged += OnLanguageChanged;
        InitializeUserAccountCommands();
        _tabSessionStore = tabSessionStore ?? new TabSessionStore();
        Tabs.CollectionChanged += (_, _) => RecomputeTabLayout();
        // v0.7.43.0: footer elapsed-time ticker (item 6) -- started/stopped from
        // RaiseIsBusyDependents, only runs while an operation is actually busy.
        _busyElapsedTimer.Tick += (_, _) =>
        {
            if (_busyStartedAt is not { } startedAt) return;
            var elapsed = DateTimeOffset.UtcNow - startedAt;
            BusyElapsedText = elapsed.TotalMinutes >= 1
                ? $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s"
                : $"{(int)elapsed.TotalSeconds}s";
        };

        foreach (var finding in _localDiagnostics.GetLocalMachineFindings())
            LocalMachineFindings.Add(finding);

        foreach (var profile in _profileStore.Load())
            Profiles.Add(profile);

        ActiveTab = CreateTab();
        SelectedProfile = Profiles.FirstOrDefault() ?? ConnectionProfile.LocalDefault;

        ConnectCommand = new AsyncCommand(RefreshAsync, () => !IsBusy);
        DiscoverServicesCommand = new AsyncCommand(DiscoverServicesAsync, () => !IsBusy && !IsDiscovering);
        CancelDiscoveryCommand = new RelayCommand(() => _discoveryCancellation?.Cancel());
        RefreshMonitoringCommand = new AsyncCommand(RefreshMonitoringAsync, () => !IsBusy);
        RefreshConsoleViewCommand = new AsyncCommand(RefreshMonitoringAsync, () => !IsBusy);
        RconDoctorCommand = new AsyncCommand(RunRconDoctorAsync, () => !IsBusy && ManagementApiConnected);
        RconConnectCommand = new AsyncCommand(ConnectRconAsync, () => !IsBusy && ManagementApiConnected);
        RconDisconnectCommand = new RelayCommand(DisconnectRcon);
        RconSendCommand = new AsyncCommand(SendRconCommandAsync, () => !IsBusy && ManagementApiConnected && !string.IsNullOrWhiteSpace(RconCommandText));
        RefreshActivityCommand = new AsyncCommand(RefreshActivityAsync, () => !IsBusy);
        RefreshNotificationsCommand = new AsyncCommand(RefreshNotificationsAsync, () => !IsBusy);
        NotificationSelfTestCommand = new AsyncCommand(RunNotificationSelfTestAsync, () => !IsBusy && ManagementApiConnected);
        MarkAllNotificationsReadCommand = new AsyncCommand(MarkAllNotificationsReadAsync, () => !IsBusy && ManagementApiConnected);
        ToggleNotificationReadCommand = new AsyncCommand(ToggleNotificationReadAsync, () => !IsBusy && SelectedNotification is not null);
        ToggleNotificationPinCommand = new AsyncCommand(ToggleNotificationPinAsync, () => !IsBusy && SelectedNotification is not null);
        DismissNotificationCommand = new AsyncCommand(DismissNotificationAsync, () => !IsBusy && SelectedNotification is not null);
        RefreshAutomationCommand = new AsyncCommand(RefreshAutomationAsync, () => !IsBusy);
        CreateAutomationRuleCommand = new AsyncCommand(CreateAutomationRuleAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(NewAutomationRuleName));
        DeleteAutomationRuleCommand = new AsyncCommand(DeleteAutomationRuleAsync, () => !IsBusy && SelectedAutomationRule is not null);
        ToggleAutomationRuleEnabledCommand = new AsyncCommand(ToggleAutomationRuleEnabledAsync, () => !IsBusy && SelectedAutomationRule is not null);
        RunAutomationRuleNowCommand = new AsyncCommand(RunAutomationRuleNowAsync, () => !IsBusy && SelectedAutomationRule is not null);
        RefreshSecurityCommand = new AsyncCommand(RefreshSecurityAsync, () => !IsBusy);
        CreatePrincipalCommand = new AsyncCommand(CreatePrincipalAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(NewPrincipalName));
        RevokePrincipalCommand = new AsyncCommand(RevokePrincipalAsync, () => !IsBusy && SelectedPrincipal is not null);
        RefreshAlertCenterCommand = new AsyncCommand(RefreshAlertCenterAsync, () => !IsBusy);
        SaveAlertRulesCommand = new AsyncCommand(SaveAlertRulesAsync, () => !IsBusy);
        // v0.7.111.0: CommandParameter is the mute length in minutes as text ("60", "480", "1440", "0" = unmute).
        MuteAlertsCommand = new RelayCommand<string>(m => { if (!IsBusy && int.TryParse(m, out var minutes)) RunUi(() => MuteAlertsAsync(minutes)); });
        // v0.8.4.0: same shape for pausing outside delivery ("0" resumes).
        PauseDeliveryCommand = new RelayCommand<string>(m => { if (!IsBusy && int.TryParse(m, out var minutes)) RunUi(() => PauseDeliveryAsync(minutes)); });
        SendTestNotificationCommand = new RelayCommand(() => { if (!IsBusy) RunUi(() => SendTestNotificationAsync()); });
        RefreshDiscordBotConfigCommand = new AsyncCommand(RefreshDiscordBotConfigAsync, () => !IsBusy);
        SaveDiscordBotConfigCommand = new AsyncCommand(SaveDiscordBotConfigAsync, () => !IsBusy);
        AddDiscordRoleMappingCommand = new RelayCommand(AddDiscordRoleMapping);
        RemoveDiscordRoleMappingCommand = new RelayCommand(RemoveDiscordRoleMapping);
        // v1.0.0.5: Bases and Guilds link to each other and to the map (MainWindowViewModel.WorldCards.cs).
        ShowSelectedBaseOnMapCommand = new RelayCommand(ShowSelectedBaseOnMap);
        OpenSelectedBaseGuildCommand = new RelayCommand(OpenSelectedBaseGuild);
        OpenGuildBaseCommand = new RelayCommand<string>(OpenGuildBase);
        // v1.0.0.4: the save-folder access fix (MainWindowViewModel.SaveAccess.cs).
        // v1.0.0.2: unique player names (MainWindowViewModel.NameGuard.cs).
        RefreshAntiCheatCommand = new AsyncCommand(RefreshAntiCheatAsync, () => !IsBusy);
        SaveAntiCheatRulesCommand = new AsyncCommand(SaveAntiCheatRulesAsync, () => !IsBusy);
        RefreshFleetCommand = new AsyncCommand(RefreshFleetAsync, () => !IsBusy);
        CloneWorldCommand = new AsyncCommand(CloneWorldAsync, () => !IsBusy && ManagementApiConnected && !string.IsNullOrWhiteSpace(CloneNewProfileId) && CloneWorldConfirmed);
        RestartAfterCloneCommand = new AsyncCommand(RestartAfterCloneAsync, () => !IsBusy && CloneWorldNeedsRestart);
        BackupAllCommand = new AsyncCommand(BackupAllAsync, () => !IsBusy && ManagementApiConnected);
        DoctorAllCommand = new AsyncCommand(DoctorAllAsync, () => !IsBusy && ManagementApiConnected);
        UpdateAllCommand = new AsyncCommand(UpdateAllAsync, () => !IsBusy && ManagementApiConnected);
        AnalyzeCrashesCommand = new AsyncCommand(AnalyzeCrashesAsync, () => !IsBusy && ManagementApiConnected);
        RefreshCrashHistoryCommand = new AsyncCommand(RefreshCrashHistoryAsync, () => !IsBusy);
        RefreshPlayersCommand = new AsyncCommand(RefreshPlayersPageAsync, () => !IsBusy);
        SaveWorldNowCommand = new AsyncCommand(SaveWorldNowAsync, () => !IsBusy);
        // v0.7.112.0: real now (PalDefender over RCON, the Starter Players.Kits provider), no longer hardcoded off.
        // v1.0.7.2 (roadmap Q-12): works while Verify All keeps the window busy.
        BootstrapLocalCommand = new AsyncCommand(BootstrapLocalAsync, () => !IsBusy && IsLocalProfile);
        GenerateServerNameCommand = new RelayCommand(GenerateServerName, () => Configuration.PalworldConfigLoaded && !IsBusy);
        GenerateSetupServerNameCommand = new RelayCommand(() => SetupServerName = GenerateRandomServerName());
        RunDoctorCommand = new AsyncCommand(RunDoctorAsync, () => !IsBusy);
        RecheckDiagnosticCommand = new RelayCommand<DiagnosticFindingDto>(f => { if (!IsBusy) RunUi(() => RecheckDiagnosticAsync(f)); });
        // v0.7.105.0: FixConfirmed gates this the same way Backups.BackupRestoreConfirmed gates Backups.RestoreBackupCommand --
        // checked here too, not just via the button's IsEnabled, so the command itself can never fire unconfirmed.
        FixDiagnosticCommand = new RelayCommand<DiagnosticFindingDto>(f => { if (!IsBusy && f is { CanFix: true, FixConfirmed: true }) RunUi(() => FixDiagnosticAsync(f)); });
        RunNetworkDiagnosticsCommand = new AsyncCommand(RunNetworkDiagnosticsAsync, () => !IsBusy);
        DiagnoseConnectionCommand = new AsyncCommand(DiagnoseConnectionAsync, () => !IsBusy && SelectedProfile is not null);
        RepairFirewallCommand = new AsyncCommand(RepairFirewallAsync, () => !IsBusy);
        RunWanReachabilityCommand = new AsyncCommand(RunWanReachabilityAsync, () => !IsBusy);
        RepairUpnpMappingCommand = new AsyncCommand(RepairUpnpMappingAsync, () => !IsBusy);
        OpenExternalPortCheckerCommand = new RelayCommand(OpenExternalPortChecker);
        RestartFromDiagnosticsCommand = new AsyncCommand(RestartFromDiagnosticsAsync, () => !IsBusy && ManagementApiConnected && ServerIsRunning);
        RefreshEnvironmentCommand = new AsyncCommand(RefreshEnvironmentAsync, () => !IsBusy);
        VerifyEnvironmentCommand = new AsyncCommand(VerifyEnvironmentAsync, () => !IsBusy);
        CreateDefaultServerSettingsCommand = new AsyncCommand(CreateDefaultServerSettingsAsync, () => !IsBusy && SetupCreateConfirmed);
        EnvironmentActionCommand = new RelayCommand<EnvironmentChecklistItemDto>(RunEnvironmentAction);
        RefreshDistributionCommand = new AsyncCommand(RefreshDistributionAsync, () => !IsBusy);
        PreviewDistributionPlanCommand = new AsyncCommand(PreviewDistributionPlanAsync, () => !IsBusy);
        UpdatePalworldServerCommand = new AsyncCommand(UpdatePalworldServerAsync, () => !IsBusy);
        InstallPalworldServerFromWizardCommand = new AsyncCommand(InstallPalworldServerFromWizardAsync, () => !IsBusy);
        InstallPalworldServerWithExtrasCommand = new AsyncCommand(InstallPalworldServerWithExtrasAsync, () => !IsBusy);
        // v1.0.1.0: Update on every Update Center row (MainWindowViewModel.ComponentUpdates.cs); pip's own command folded in.
        UpdateComponentCommand = new RelayCommand<ComponentVersionDto>(UpdateComponent, component => component?.CanUpdate == true);
        // v1.0.4.0 (roadmap S-1, S-2): the selected player's inventory (MainWindowViewModel.Inventory.cs).
        RefreshWorldExplorerCommand = new AsyncCommand(RefreshWorldExplorerAsync, () => !IsBusy);
        RefreshOperationsCommand = new AsyncCommand(RefreshOperationsAsync, () => !IsBusy && ManagementApiConnected);
        RefreshPlayerGuildExplorerCommand = new AsyncCommand(RefreshPlayerGuildExplorerAsync, () => !IsBusy);
        RefreshPalsCommand = new AsyncCommand(RefreshPalsAsync, () => !IsBusy);
        // The "nothing is scheduled" hint follows the collection, so it disappears the moment a rule is created.
        AutomationRules.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(HasNoAutomationRules));
        Map.LoadMapBackgroundPreference();
        Configuration.RebuildConfigPresetList();
        // v1.0.0.1: the stuck-start protocol's test load, and clearing a finished test from the Dashboard.
        BeginStuckTestLoadCommand = new AsyncCommand(() => Mods.BeginModSafeStartAsync(testLoad: true), () => !IsBusy && Mods.ModSafeStartStatus is not { IsRunning: true });
        ExportDoctorCommand = new RelayCommand(ExportDoctorReport, () => DoctorChecks.Count > 0);
        StartCommand = new AsyncCommand(StartServerAsync, () => !IsBusy && (ManagementApiConnected || LocalPalServerStatus == "Found") && !ServerIsRunning);
        StopCommand = new AsyncCommand(StopServerAsync, () => !IsBusy && ManagementApiConnected && ServerIsRunning);
        RestartCommand = new AsyncCommand(RestartServerAsync, () => !IsBusy && ManagementApiConnected && ServerIsRunning);
        ForceStopServerCommand = new AsyncCommand(ForceStopServerAsync, () => !IsBusy && ManagementApiConnected && ServerIsRunning);
        InstallMissingEnvironmentCommand = new AsyncCommand(InstallMissingEnvironmentAsync, () => !IsBusy);
        RefreshAllInstancesCommand = new AsyncCommand(RefreshAllInstancesAsync, () => !IsBusy && ManagementApiConnected);
        TerminateSelectedInstanceCommand = new AsyncCommand(TerminateSelectedInstanceAsync, () => !IsBusy && ManagementApiConnected && SelectedInstance is { ManagedByThisProfile: false });
        SetAccentThemeCommand = new RelayCommand<string>(theme => SelectedAccentTheme = theme ?? "Default");
        SaveProfileCommand = new RelayCommand(SaveProfile);
        SetUpNewServerTabCommand = new RelayCommand(OpenNewServerTab);
        ConnectLocalServerTabCommand = new RelayCommand(OpenConnectLocalServerTab);
        ConnectRemoteServerTabCommand = new RelayCommand(OpenConnectRemoteServerTab);
        WizardAdvanceCommand = new RelayCommand(AdvanceWizardStep);
        WizardBackCommand = new RelayCommand(GoBackWizardStep);
        ChooseLocalConnectionCommand = new RelayCommand(ChooseLocalConnection);
        ChooseRemoteConnectionCommand = new RelayCommand(ChooseRemoteConnection);
        DetectLocalServiceCommand = new AsyncCommand(DetectLocalServiceAsync, () => !IsBusy);
        ChooseCloneWorldSourceCommand = new RelayCommand(() => ChooseNewServerWorldSource("Clone"));
        ChooseNewWorldSourceCommand = new RelayCommand(() => ChooseNewServerWorldSource("NewWorld"));
        ChooseImportWorldSourceCommand = new RelayCommand(() => ChooseNewServerWorldSource("Import"));
        CloneIntoNewServerCommand = new AsyncCommand(CloneIntoNewServerAsync,
            () => !IsBusy && NewServerCloneSourceProfile is not null && !string.IsNullOrWhiteSpace(NewServerCloneTargetId));
        ChooseWorldSettingsPresetCommand = new AsyncCommand(ChooseWorldSettingsPresetAsync, () => !IsBusy);
        ChooseWorldSettingsCustomizeCommand = new AsyncCommand(ChooseWorldSettingsCustomizeAsync, () => !IsBusy);
        ContinueFromInstallDirectoryCommand = new AsyncCommand(ContinueFromInstallDirectoryAsync, () => !IsBusy);
        FinishInstallAndAdvanceCommand = new AsyncCommand(FinishInstallAndAdvanceAsync, () => !IsBusy);
        PrepareForWorldImportCommand = new AsyncCommand(PrepareForWorldImportAsync, () => !IsBusy);
        ConnectExistingProfileTabCommand = new RelayCommand<ConnectionProfile>(ConnectExistingProfileTab);
        CloseTabCommand = new RelayCommand<TabSession>(CloseTab, _ => Tabs.Count > 1);
        CloseActiveTabCommand = new RelayCommand(() => CloseTab(ActiveTab), () => ActiveTab is not null && Tabs.Count > 1);
        DeleteProfileCommand = new RelayCommand(DeleteSelectedProfile, () => SelectedProfile is not null && SelectedProfile.Id != ConnectionProfile.LocalDefault.Id);
        // CanExecute deliberately only checks SelectedProfile, not whether a token is currently
        // saved for it -- nothing re-evaluates this command's CanExecute when a save/forget happens
        // elsewhere, so a BearerToken-based condition would go stale (e.g. staying disabled after a
        // successful Connect just saved one). Delete-on-nothing-to-delete is a safe no-op either way.
        ForgetSavedTokenCommand = new RelayCommand(ForgetSavedToken, () => SelectedProfile is not null);
        NavigateCommand = new RelayCommand<string>(Navigate);
        OpenNotificationLinkCommand = new RelayCommand<NotificationItemDto>(OpenNotificationLink);
        ToggleWanDiagnosticsCommand = new RelayCommand(() => IsWanDiagnosticsExpanded = !IsWanDiagnosticsExpanded);
        ToggleLocalDiagnosticsCommand = new RelayCommand(() => IsLocalDiagnosticsExpanded = !IsLocalDiagnosticsExpanded);
        ToggleDiscordBotCommand = new RelayCommand(() => IsDiscordBotExpanded = !IsDiscordBotExpanded);
        ToggleAntiCheatCommand = new RelayCommand(() => IsAntiCheatExpanded = !IsAntiCheatExpanded);
        // No generic async command primitive exists in this codebase for a per-row action bound via
        // CommandParameter -- following the same RunUi(...) pattern (v1.0.7.0: was a fire-and-forget post)
        // already used by Players.ToggleWhitelistCommand/Players.ToggleBanListCommand above rather than adding one.
        // v0.8.23.0: every command exists now, so the role each needs can be attached.
        InstallCommandRoleGates();
        RebuildRibbonGroups();
        RunUi(async () =>
        {
            // v0.7.74.0: these two used to share one unguarded async continuation -- an unhandled
            // exception anywhere inside InitializeLocalDashboardAsync (e.g. local-installation
            // discovery failing for a reason specific to wherever this build happens to be running
            // from) silently aborted the whole continuation before RestoreTabSessionAsync ever ran,
            // dropping every remembered tab beyond the first with no error shown anywhere -- reported
            // live as only 1 of 3 saved tabs reopening. Isolating each phase means a failure in one
            // can no longer take the other down with it.
            try { await InitializeLocalDashboardAsync(); }
            catch (Exception ex) { StatusBarText = $"Local dashboard initialization failed: {ex.Message}"; }
            try { await RestoreTabSessionAsync(); }
            catch (Exception ex) { StatusBarText = $"Restoring saved tabs failed: {ex.Message}"; }
        });
    }

    public string ProductName => "MystTiq";
    public string Version => $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(4)}";
    public string PlatformText => OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : "Desktop";
    public string ProfileStorageText => $"Profiles: {_profileStore.StoragePath}";
    public string SecretPolicyText => "Bearer token is process-memory-only and is never written to the profile file.";
    public bool IsLocalProfile => SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id;
    // Gates the relocated "In-Game Server Defaults" card on the Settings page profile
    // editor -- only shown while setting up a brand-new connection (SelectedProfile is null, the
    // state BeginNewProfile() puts the app in), not while editing/reconnecting to a saved one.
    public bool IsCreatingNewProfile => SelectedProfile is null;

    // v0.7.3.0: the "Set Up New Server" wizard's current step. Only meaningful while
    // IsCreatingNewProfile -- editing an already-saved profile never shows or uses this, and its
    // Connection Details card/Save/Delete buttons are completely unaffected by the wizard. Reset to
    // 0 by BeginNewProfile() every time "Set Up New Server"/"Connect to Local or Remote Server" is
    // opened; step 0 itself renders nothing (see below) and is only ever a one-tick starting point
    // immediately advanced past by ChooseLocalConnection/ChooseRemoteConnection.
    // v0.7.6.0: delegates to ActiveTab.WizardStep (was a single shared field) so two tabs mid-setup
    // at once no longer corrupt each other's step -- see TabSession.WizardStep.
    // v0.7.82.0: raw step numbers are now REUSED across the two flows for different content --
    // IsNewServerSetupFlow (and IsWorldSourceClone, where relevant) disambiguate which content a
    // given number means, exactly like IsWizardStepSettings already did for step 4 pre-v0.7.82.0.
    // The install wizard's own steps: 1 Local Service (shared with Connect) -> 2 World Source ->
    // 3 Clone source picker (Clone) OR Identity & Ports (New World/Import) -> 4 World Settings choice
    // (New World/Import only) -> 5 Install Directory (New World/Import only) -> 6 Download & Install
    // (New World/Import only) -> 7 Confirm (Clone jumps here directly from 3; Connect's own Confirm
    // stays at its original step 5, untouched -- see IsWizardStepConfirm below).
    public int NewServerWizardStep
    {
        get => ActiveTab?.WizardStep ?? 0;
        private set
        {
            if (!SetActiveTabField(t => t.WizardStep, (t, v) => t.WizardStep = v, value)) return;
            RaisePropertyChanged(nameof(IsWizardStep1));
            RaisePropertyChanged(nameof(IsWizardStepWorldSource));
            RaisePropertyChanged(nameof(IsWizardStepCloneSource));
            RaisePropertyChanged(nameof(IsWizardStepIdentityPorts));
            RaisePropertyChanged(nameof(IsWizardStepWorldSettingsChoice));
            RaisePropertyChanged(nameof(IsWizardStepInstallDirectory));
            RaisePropertyChanged(nameof(IsWizardStepInstall));
            RaisePropertyChanged(nameof(IsWizardStepMods));
            RaisePropertyChanged(nameof(IsWizardStepSettings));
            RaisePropertyChanged(nameof(IsWizardStepConfirm));
        }
    }
    public bool IsWizardStep1 => IsCreatingNewProfile && NewServerWizardStep == 1;
    public bool IsWizardStepWorldSource => IsCreatingNewProfile && IsNewServerSetupFlow && NewServerWizardStep == 2;
    // v0.7.82.0: step 3 now branches by world source instead of housing the old Clone-or-Install
    // pairing -- Clone keeps its existing source-picker content unchanged; New World/Import get the
    // new Identity & Ports step here instead of the old Install sub-view (Install itself moved to
    // step 6, see IsWizardStepInstall).
    public bool IsWizardStepCloneSource => IsCreatingNewProfile && IsNewServerSetupFlow && IsWorldSourceClone && NewServerWizardStep == 3;
    public bool IsWizardStepIdentityPorts => IsCreatingNewProfile && IsNewServerSetupFlow && !IsWorldSourceClone && NewServerWizardStep == 3;
    public bool IsWizardStepWorldSettingsChoice => IsCreatingNewProfile && IsNewServerSetupFlow && !IsWorldSourceClone && NewServerWizardStep == 4;
    public bool IsWizardStepInstallDirectory => IsCreatingNewProfile && IsNewServerSetupFlow && !IsWorldSourceClone && NewServerWizardStep == 5;
    public bool IsWizardStepInstall => IsCreatingNewProfile && IsNewServerSetupFlow && !IsWorldSourceClone && NewServerWizardStep == 6;
    // v0.7.86.0: MODs step, install-wizard only, New World/Import only (Clone already has whatever
    // MODs the source server had, copied over as part of the clone itself). Reuses the exact same
    // Workshop-scan machinery (Mods.ScanWorkshopModsCommand/Mods.WorkshopItems/Mods.ImportSelectedWorkshopModCommand)
    // already built for the MOD Library page -- both already correctly use
    // BuildProfileFromEditor(SelectedProfile?.Id) rather than requiring SelectedProfile directly, so
    // (unlike a couple of other reused commands found this session) no wizard-usability bug to fix
    // here. Optional: the step's own "Next" just advances, whether or not any MOD was imported.
    public bool IsWizardStepMods => IsCreatingNewProfile && IsNewServerSetupFlow && !IsWorldSourceClone && NewServerWizardStep == 7;
    // v0.7.82.0: Connect's own Settings step, UNCHANGED in content/behavior from pre-v0.7.82.0 --
    // now explicitly gated to !IsNewServerSetupFlow since step 4 means "World Settings choice" for
    // the install wizard instead.
    public bool IsWizardStepSettings => IsCreatingNewProfile && !IsNewServerSetupFlow && NewServerWizardStep == 4;
    // v0.7.86.0: Confirm renumbered to step 8 for the install wizard (was 7 through v0.7.85.0) now
    // that MODs (7) sits before it; reached from 3 directly for Clone (still skips MODs/Install), or
    // from 7 for New World/Import. Stays step 5 for Connect, exactly as before -- the two flows never
    // collide since they're on entirely different step-number tracks from step 2 onward.
    public bool IsWizardStepConfirm => IsCreatingNewProfile && NewServerWizardStep == (IsNewServerSetupFlow ? 8 : 5);
    public ICommand WizardAdvanceCommand { get; }
    public ICommand WizardBackCommand { get; }

    // v0.7.13.0: the Step 0 choice, delegating to ActiveTab.ConnectionKind the same way
    // NewServerWizardStep delegates to ActiveTab.WizardStep, for the same reason (two tabs mid-setup
    // at once must not corrupt each other's choice).
    public string ConnectionKind
    {
        get => ActiveTab?.ConnectionKind ?? string.Empty;
        private set
        {
            if (!SetActiveTabField(t => t.ConnectionKind, (t, v) => t.ConnectionKind = v, value)) return;
            RaisePropertyChanged(nameof(IsLocalConnectionChoice));
            RaisePropertyChanged(nameof(IsRemoteConnectionChoice));
            RaisePropertyChanged(nameof(WizardStep1HeaderText));
            RaisePropertyChanged(nameof(WizardStep1InstructionText));
        }
    }
    public bool IsLocalConnectionChoice => ConnectionKind == "Local";
    public bool IsRemoteConnectionChoice => ConnectionKind == "Remote";
    // v0.7.81.0: step 1's "Local Service" label text/step-count differs by flow ("Step 1 of 5" for
    // the install wizard vs. the original "Step 1 of 3" for Connect to Local Server); Remote is
    // never part of the install wizard, so it keeps its single unconditional label.
    public bool IsWizardStep1LocalNewServer => IsLocalConnectionChoice && IsNewServerSetupFlow;
    public bool IsWizardStep1LocalConnect => IsLocalConnectionChoice && !IsNewServerSetupFlow;
    // v0.7.82.0 bug fix: replaces a 3-way IsVisible-toggled TextBlock row (Step 1's header silently
    // stayed blank in practice -- reproduced live: every child started invisible on the very first
    // layout pass since ConnectionKind/IsNewServerSetupFlow aren't set until just after this tab is
    // created, and the row never picked up later IsVisible changes, unlike WizardStep1NextButtonLabel
    // right below, a plain string binding on the exact same underlying flag that rendered correctly
    // the whole time). One TextBlock bound to a computed string sidesteps that class of issue
    // entirely rather than chasing the underlying layout quirk.
    public string WizardStep1HeaderText => IsRemoteConnectionChoice
        ? "Step 1 of 3 — Remote Connection Details"
        : IsNewServerSetupFlow ? "Step 1 of 8 — Preparing MystTiq" : "Step 1 of 3 — Local Service";
    public string WizardStep1InstructionText => IsNewServerSetupFlow
        ? "MystTiq is starting its own local management engine automatically -- this is the engine that will run your new server, not an existing one to connect to. This normally only takes a moment; if it's stuck, use Auto-Detect below or enter a different local address."
        : "MystTiq will look for its management service already running on this machine, or start one. If you already have another local MystTiq service running on a different port, enter its address below instead.";
    public string WizardStep1NextButtonLabel => IsNewServerSetupFlow ? "Next: Choose World Source →" : "Next: Server Defaults →";
    public string WizardStepSettingsLabel => "Step 2 of 3 — In-Game Server Defaults (new server only)";
    public string WizardStepConfirmLabel => IsNewServerSetupFlow ? "Step 8 of 8 — Confirm & Finish" : "Step 3 of 3 — Confirm & Finish";
    public ICommand ChooseLocalConnectionCommand { get; }
    public ICommand ChooseRemoteConnectionCommand { get; }
    public ICommand DetectLocalServiceCommand { get; }

    // v0.7.81.0: delegates to ActiveTab.IsNewServerSetupFlow/WorldSource the same way
    // ConnectionKind delegates to ActiveTab.ConnectionKind above, for the same per-tab reason.
    public bool IsNewServerSetupFlow
    {
        get => ActiveTab?.IsNewServerSetupFlow ?? false;
        set
        {
            if (!SetActiveTabField(t => t.IsNewServerSetupFlow, (t, v) => t.IsNewServerSetupFlow = v, value)) return;
            RaisePropertyChanged(nameof(IsWizardStep1LocalNewServer));
            RaisePropertyChanged(nameof(IsWizardStep1LocalConnect));
            RaisePropertyChanged(nameof(WizardStep1HeaderText));
            RaisePropertyChanged(nameof(WizardStep1InstructionText));
            RaisePropertyChanged(nameof(WizardStep1NextButtonLabel));
            RaisePropertyChanged(nameof(WizardStepSettingsLabel));
            RaisePropertyChanged(nameof(WizardStepConfirmLabel));
            RaisePropertyChanged(nameof(IsWizardStepWorldSource));
            RaisePropertyChanged(nameof(IsWizardStepCloneSource));
            RaisePropertyChanged(nameof(IsWizardStepIdentityPorts));
            RaisePropertyChanged(nameof(IsWizardStepWorldSettingsChoice));
            RaisePropertyChanged(nameof(IsWizardStepInstallDirectory));
            RaisePropertyChanged(nameof(IsWizardStepInstall));
            RaisePropertyChanged(nameof(IsWizardStepMods));
            RaisePropertyChanged(nameof(IsWizardStepSettings));
        }
    }
    public string NewServerWorldSource
    {
        get => ActiveTab?.WorldSource ?? string.Empty;
        set
        {
            if (!SetActiveTabField(t => t.WorldSource, (t, v) => t.WorldSource = v, value)) return;
            RaisePropertyChanged(nameof(IsWorldSourceClone));
            RaisePropertyChanged(nameof(IsWorldSourceNewWorld));
            RaisePropertyChanged(nameof(IsWorldSourceImport));
            RaisePropertyChanged(nameof(IsWizardStepCloneSource));
            RaisePropertyChanged(nameof(IsWizardStepIdentityPorts));
            RaisePropertyChanged(nameof(IsWizardStepWorldSettingsChoice));
            RaisePropertyChanged(nameof(IsWizardStepInstallDirectory));
            RaisePropertyChanged(nameof(IsWizardStepInstall));
            RaisePropertyChanged(nameof(IsWizardStepMods));
        }
    }
    public bool IsWorldSourceClone => NewServerWorldSource == "Clone";
    public bool IsWorldSourceNewWorld => NewServerWorldSource == "NewWorld";
    public bool IsWorldSourceImport => NewServerWorldSource == "Import";
    public ICommand ChooseCloneWorldSourceCommand { get; }
    public ICommand ChooseNewWorldSourceCommand { get; }
    public ICommand ChooseImportWorldSourceCommand { get; }
    public ICommand CloneIntoNewServerCommand { get; }

    // v0.7.82.0: World Settings choice step -- a preset applies automatically right after install
    // (real config doesn't exist to load/apply against before then, see ContinueFromInstallDirectoryAsync's
    // own comment); "Customize" can't show real slider values pre-install either, so it defers to the
    // existing, fully-live Configuration page after Finish rather than faking pre-install values.
    public string NewServerWorldSettingsChoice
    {
        get => _newServerWorldSettingsChoice;
        set
        {
            if (!SetField(ref _newServerWorldSettingsChoice, value)) return;
            RaisePropertyChanged(nameof(IsWorldSettingsChoicePreset));
            RaisePropertyChanged(nameof(IsWorldSettingsChoiceCustomize));
        }
    }
    private string _newServerWorldSettingsChoice = "Preset";
    public bool IsWorldSettingsChoicePreset => NewServerWorldSettingsChoice == "Preset";
    public bool IsWorldSettingsChoiceCustomize => NewServerWorldSettingsChoice == "Customize";
    public ICommand ChooseWorldSettingsPresetCommand { get; }
    public ICommand ChooseWorldSettingsCustomizeCommand { get; }

    // v0.7.82.0: Install Directory step -- computed when the step is entered (see
    // EvaluateNewServerInstallDirectory), not live-bound, since it involves a filesystem probe.
    public bool NewServerInstallDirectoryOccupied { get => _newServerInstallDirectoryOccupied; private set => SetField(ref _newServerInstallDirectoryOccupied, value); }
    private bool _newServerInstallDirectoryOccupied;
    // v0.7.88.0 bug fix: reported live -- the step showed only a static warning and a status
    // sentence, no actual field to see or change the target path. Setter made public (was
    // private-set-only, computed) so the wizard's own TextBox/Browse button can edit it directly;
    // ContinueFromInstallDirectoryAsync already just reads whatever value is here, so a manual edit
    // flows straight through with no further wiring needed.
    public string NewServerInstallDirectoryEffectivePath { get => _newServerInstallDirectoryEffectivePath; set => SetField(ref _newServerInstallDirectoryEffectivePath, value); }
    private string _newServerInstallDirectoryEffectivePath = string.Empty;
    public string NewServerInstallDirectoryStatusText { get => _newServerInstallDirectoryStatusText; private set => SetField(ref _newServerInstallDirectoryStatusText, value); }
    private string _newServerInstallDirectoryStatusText = string.Empty;
    public ICommand ContinueFromInstallDirectoryCommand { get; }
    public ICommand FinishInstallAndAdvanceCommand { get; }

    // v0.7.85.0: Import a World, Phase 2 -- confirmed live against a genuinely fresh install
    // ("MystTiqClaude") that PalServer.exe itself generates a real SaveGames/0/<32-hex-id>/Level.sav
    // world folder within a few minutes of its very first start. That means the already-existing,
    // already-proven World Transactions "world-import" flow (Analyze -> Apply, safety-backed atomic
    // swap) works completely unmodified once the fresh install has been started once -- no new
    // backend needed, unlike the originally-considered "pre-seed a folder before first launch"
    // approach, which would have relied on an unverified assumption about PalServer.exe's own
    // startup behavior. NewServerImportReady gates showing the embedded Analyze/Apply controls
    // (the same World.WorldTransactionMode/World.WorldTransactionState/World.WorldTransactionPlanSteps/
    // World.WorldTransactionConfirmed/World.ApplyWorldTransactionCommand bindings the World Transactions page
    // already uses, reused verbatim).
    public bool NewServerImportReady { get => _newServerImportReady; private set => SetField(ref _newServerImportReady, value); }
    private bool _newServerImportReady;
    public string NewServerImportPrepareStatusText { get => _newServerImportPrepareStatusText; private set => SetField(ref _newServerImportPrepareStatusText, value); }
    private string _newServerImportPrepareStatusText = "Starts the server once so Palworld can generate its initial world, then stops it so that placeholder world can be safely replaced with your own.";
    public ICommand PrepareForWorldImportCommand { get; }

    // v0.7.81.0: the profile picked as the clone source in the install wizard's Clone step -- any
    // already-saved profile. The brand-new, not-yet-saved profile being created can never appear
    // here on its own (it has no real Id yet, so it can't match anything in Profiles).
    //
    // v0.7.88.0 bug fix: reported live -- "Local MystTiq" (the always-present default profile,
    // near-permanently open in its own tab) was missing from this list entirely. Root cause: this
    // originally reused the same "not currently open in a tab" filter as the "+" menu's own "Connect
    // to {profile}" list (see that comment's own history), which makes sense THERE -- don't open a
    // second, redundant tab to an already-open profile -- but has no bearing on whether a profile is
    // valid to clone FROM. Cloning only requires the source's PalServer to be stopped (already stated
    // in this step's own instruction text), which has nothing to do with whether its tab happens to
    // be open.
    private ConnectionProfile? _newServerCloneSourceProfile;
    public ConnectionProfile? NewServerCloneSourceProfile
    {
        get => _newServerCloneSourceProfile;
        set { if (SetField(ref _newServerCloneSourceProfile, value)) (CloneIntoNewServerCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }
    public IEnumerable<ConnectionProfile> CloneableSourceProfiles => Profiles;
    private string _newServerCloneTargetId = string.Empty;
    public string NewServerCloneTargetId
    {
        get => _newServerCloneTargetId;
        set { if (SetField(ref _newServerCloneTargetId, value ?? string.Empty)) (CloneIntoNewServerCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }
    private string _newServerCloneStatusText = string.Empty;
    public string NewServerCloneStatusText { get => _newServerCloneStatusText; private set => SetField(ref _newServerCloneStatusText, value); }

    // The open-tab list backing true multi-tab connections. ActiveTab is what
    // SelectedProfile/BearerToken/ManagementApiConnected/ConnectionState/IsBusy/ServerIsRunning
    // now delegate through -- see TabSession.cs for why.
    public ObservableCollection<TabSession> Tabs { get; } = [];

    // v0.7.16.0: the tab strip's ListBox binds to VisibleTabs, not Tabs directly -- once more tabs
    // are open than fit the actual available window width, the overflow ones move here instead of
    // being silently clipped with no way to reach them. ActiveTab is always kept in VisibleTabs
    // (see RecomputeTabLayout) so switching to a tab never hides the one you're looking at.
    public ObservableCollection<TabSession> VisibleTabs { get; } = [];
    public ObservableCollection<TabSession> OverflowTabs { get; } = [];
    public bool HasOverflowTabs { get => _hasOverflowTabs; private set => SetField(ref _hasOverflowTabs, value); }

    // v0.7.25.0: same shrink-to-fit/overflow pattern as VisibleTabs/OverflowTabs above, applied to
    // the ribbon -- as more per-page ribbon groups get added in later versions, whichever ones don't
    // fit the actually-available width move here instead of being silently clipped. Whole groups
    // overflow together (not individual buttons within a still-visible group), since a group's
    // buttons are a semantic unit (e.g. "Server Control").
    public ObservableCollection<RibbonGroupViewModel> VisibleRibbonGroups { get; } = [];
    public ObservableCollection<RibbonGroupViewModel> OverflowRibbonGroups { get; } = [];
    public bool HasOverflowRibbonGroups { get => _hasOverflowRibbonGroups; private set => SetField(ref _hasOverflowRibbonGroups, value); }

    public TabSession? ActiveTab
    {
        get => _activeTab;
        set
        {
            if (ReferenceEquals(_activeTab, value)) return;
            _activeTab = value;
            // v0.7.79.0: theme is per-tab now (see SelectedAccentTheme/IsLightMode/ApplyProfileTheme)
            // -- switching tabs re-renders the WHOLE app in the newly active tab's own remembered
            // theme, not just its data. A wizard tab with no Profile yet (mid "Set Up New Server")
            // has no theme of its own to switch to, so the app simply keeps showing whatever was
            // last applied rather than snapping to some default. RefreshTabAccentVisuals below still
            // runs via RaiseActiveTabPropertiesChanged either way, since every open tab's identity
            // stripe/border needs to reflect whichever theme resources are now live, not just the
            // newly active tab's own.
            if (value?.Profile is { } activeProfile)
            {
                ThemeApplier.Apply(activeProfile.AccentTheme, activeProfile.ThemeVariant);
                RefreshTabAccentVisuals();
                RaisePropertyChanged(nameof(IsLightMode));
                RaisePropertyChanged(nameof(SelectedThemeModeIndex));
                RaisePropertyChanged(nameof(PageArtwork));
                RaisePropertyChanged(nameof(SetupArtwork));
            }
            // v0.7.6.0: OnlinePlayers/backups/mods/Pal lists are per-server data that isn't yet
            // itself tab-scoped (see RefreshActiveTabDataAsync below) -- until the newly active
            // tab's own data arrives, a leftover selection from the PREVIOUS tab must not still be
            // clickable, since e.g. a destructive action would otherwise fire against this tab's
            // connection using the previous tab's target.
            // v0.7.8.0 correction: v0.7.6.0 cleared SelectedOnlinePlayer here on the assumption
            // (from the bug scan that prompted it) that it was what Kick/Ban/Ban-list-driven actions
            // read -- direct inspection while adding Unban/Teleport here found that assumption was
            // wrong. Kick/Ban/Unban/Teleport all actually read Players.SelectedPlayerRecord, the Players
            // page's "Directory" selection (a completely different, and until now unaddressed,
            // property), which is populated only by RefreshPlayersPageAsync -- a page-navigation
            // trigger, not the 5-second timer -- so it was not only unfixed by v0.7.6.0 but has an
            // even wider staleness window (indefinite, not 5 seconds) if the user stays on the
            // Players page across a tab switch. Clearing it here and, if already on the Players
            // page, immediately re-running that page's own refresh for the newly active tab closes
            // the actual mechanism these player-targeted actions use, not just the one this class of
            // bug was first reported against.
            SelectedOnlinePlayer = null;
            Players.SelectedPlayerRecord = null;
            Backups.SelectedBackup = null;
            Mods.SelectedMod = null;
            World.SelectedExplorerPal = null;
            World.SelectedExplorerBase = null;
            // v0.7.92.0: base marker coordinates are per-server world data, same stale-carryover
            // class as the Dashboard's Active World card (v0.7.89.0): clear them so a different
            // server's bases are never drawn on this tab's map before its own arrive.
            // v1.0.8.3 (roadmap C-3): the map forgets the previous server's bases, players and Pals, and starts on the whole map.
            Map.ResetForNewServer();
            // v0.8.3.0: the Give Item picker's ids come from this server's own world save; another server's list
            // must not be offered here.
            Players.ClearGameIdCatalog();
            // v0.7.97.0: crash findings, their plan and history are per-server too, so another
            // server's causes and fixes must never sit under this tab's name until its own arrive.
            CrashFindings.Clear();
            CrashIsolationPlan.Clear();
            CrashHistory.Clear();
            SelectedCrashFinding = null;
            CrashAnalyzerState = string.Empty;
            // v0.7.74.0: reported live -- switching tabs previously left whatever page the PREVIOUS
            // tab was on displayed for the newly active one too, rather than returning to wherever
            // this tab itself was last left. Restoring before RaiseActiveTabPropertiesChanged/the
            // Players-page check below so both react to the correct, now-current page.
            SelectedPage = value?.LastPage ?? NavigationPage.Dashboard;
            RaiseActiveTabPropertiesChanged();
            RecomputeTabLayout();
            _ = RefreshActiveTabDataAsync();
            if (SelectedPage is NavigationPage.Players or NavigationPage.Map) _ = RefreshPlayersPageAsync();
            if (SelectedPage == NavigationPage.CrashAnalyzer) _ = RefreshCrashHistoryAsync();
        }
    }

    // v0.7.75.0: fixes a real bug found live -- MainWindow.axaml had several elements bound
    // directly to "ActiveTab.AccentBrush" (nav pane border, footer border, page-header border,
    // title-bar strip), expecting the per-tab identity color from v0.7.63.0/v0.7.74.0 to update on
    // every tab switch. It never did: ActiveTab's own setter above never raises PropertyChanged for
    // "ActiveTab" itself, only for the fixed list of pass-through properties
    // RaiseActiveTabPropertiesChanged() re-raises -- exactly why every other tab-scoped value in
    // this class (BearerToken, ServerUrl, etc.) is exposed as its own explicit pass-through property
    // rather than bound directly as "ActiveTab.X" in XAML. This follows that same established
    // pattern instead of the ad-hoc direct-path bindings that silently never refreshed.
    public IBrush ActiveTabAccentBrush => ActiveTab?.AccentBrush ?? Brushes.Transparent;
    // v0.7.77.0: BoxShadows companion to ActiveTabAccentBrush -- see TabSession.AccentGlowShadow's
    // own comment for why a plain {Binding} to a resolved value, not {DynamicResource}, is what
    // makes this able to bind at all.
    public BoxShadows ActiveTabAccentGlowShadow => ActiveTab?.AccentGlowShadow ?? default;

    private bool SetActiveTabField<T>(Func<TabSession, T> getter, Action<TabSession, T> setter, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (ActiveTab is null) return false;
        if (EqualityComparer<T>.Default.Equals(getter(ActiveTab), value)) return false;
        setter(ActiveTab, value);
        RaisePropertyChanged(propertyName);
        return true;
    }

    // Replays, for the newly active tab, exactly the notifications each delegated property's own
    // setter would have raised had its value changed just now -- so switching tabs updates every
    // bound control (status dot, Start/Stop/Restart buttons, glow states, page visibility) to the
    // newly active tab's real state, without re-running any value-dependent business logic (e.g.
    // SelectedProfile's "reset BearerToken/ConnectionState for a freshly chosen profile" logic,
    // which must NOT replay just because focus moved to an already-connected background tab).
    private void RaiseActiveTabPropertiesChanged()
    {
        RaisePropertyChanged(nameof(SelectedProfile));
        RaisePropertyChanged(nameof(ProfileName));
        RaisePropertyChanged(nameof(ServerUrl));
        RaisePropertyChanged(nameof(CertificateSha256));
        RaisePropertyChanged(nameof(BearerToken));
        RaisePropertyChanged(nameof(ManagementApiConnected));
        RaisePropertyChanged(nameof(ConnectionState));
        RaisePropertyChanged(nameof(IsBusy));
        RaisePropertyChanged(nameof(ServerIsRunning));
        RaisePropertyChanged(nameof(IsLocalProfile));
        RaisePropertyChanged(nameof(BrowserViewUrlVerbatim));
        RaisePropertyChanged(nameof(IsCreatingNewProfile));
        RaisePropertyChanged(nameof(IsWizardStep1));
        RaisePropertyChanged(nameof(IsWizardStepWorldSource));
        RaisePropertyChanged(nameof(IsWizardStepCloneSource));
        RaisePropertyChanged(nameof(IsWizardStepIdentityPorts));
        RaisePropertyChanged(nameof(IsWizardStepWorldSettingsChoice));
        RaisePropertyChanged(nameof(IsWizardStepInstallDirectory));
        RaisePropertyChanged(nameof(IsWizardStepInstall));
        RaisePropertyChanged(nameof(IsWizardStepMods));
        RaisePropertyChanged(nameof(IsWizardStepSettings));
        RaisePropertyChanged(nameof(IsWizardStepConfirm));
        RaisePropertyChanged(nameof(ConnectionKind));
        RaisePropertyChanged(nameof(IsLocalConnectionChoice));
        RaisePropertyChanged(nameof(IsRemoteConnectionChoice));
        RaisePropertyChanged(nameof(IsNewServerSetupFlow));
        RaisePropertyChanged(nameof(IsWizardStep1LocalNewServer));
        RaisePropertyChanged(nameof(IsWizardStep1LocalConnect));
        RaisePropertyChanged(nameof(WizardStep1HeaderText));
        RaisePropertyChanged(nameof(WizardStep1InstructionText));
        RaisePropertyChanged(nameof(NewServerWorldSource));
        RaisePropertyChanged(nameof(IsWorldSourceClone));
        RaisePropertyChanged(nameof(IsWorldSourceNewWorld));
        RaisePropertyChanged(nameof(IsWorldSourceImport));
        RaisePropertyChanged(nameof(ActiveTabAccentBrush));
        RaisePropertyChanged(nameof(ActiveTabAccentGlowShadow));
        Workspace.RaiseWorkspaceSummaryProperties();
        RaiseManagementApiConnectedDependents();
        RaiseConnectionStateDependents();
        RaiseServerIsRunningDependents();
        RaiseIsBusyDependents(IsBusy);
        (DeleteProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (BootstrapLocalCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (DiagnoseConnectionCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (CloseActiveTabCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    // v0.7.79.0: Appearance is now per-tab -- requested directly ("the colour mode selected should
    // only apply to the tab, not the other tabs"). Reads/writes through to ActiveTab.Profile's own
    // AccentTheme/ThemeVariant (see ConnectionProfile.cs) instead of one shared app-wide field.
    // AccentThemeOptions drives the Settings page's swatch-button row.
    public IReadOnlyList<string> AccentThemeOptions { get; } = ThemeCatalog.AccentThemes;
    public string SelectedAccentTheme
    {
        get => ActiveTab?.Profile?.AccentTheme ?? "Default";
        set
        {
            if (ActiveTab?.Profile is not { } profile || profile.AccentTheme == value) return;
            ApplyProfileTheme(profile with { AccentTheme = value });
        }
    }
    // v0.8.16.0: "the Light palette is showing" (Light, or Follow the system on a light system) -- the page art reads it.
    // The Settings checkbox it used to back is now the mode picker below.
    public bool IsLightMode
    {
        get => ActiveTab?.Profile is { } profile
            ? ThemeApplier.ResolveVariant(profile.ThemeVariant) == "Light"
            : Avalonia.Application.Current?.ActualThemeVariant == Avalonia.Styling.ThemeVariant.Light;
        set
        {
            if (ActiveTab?.Profile is not { } profile) return;
            var variant = value ? "Light" : "Dark";
            if (profile.ThemeVariant == variant) return;
            ApplyProfileTheme(profile with { ThemeVariant = variant });
        }
    }

    // v0.8.16.0: the mode picker (Dark, Light, Midnight, High contrast, Follow the system), per tab like the accent theme.
    public IReadOnlyList<string> ThemeModeLabels { get; } = ThemeCatalog.ModeLabels;
    public int SelectedThemeModeIndex
    {
        get => Array.IndexOf(ThemeCatalog.Modes, ThemeCatalog.NormalizeMode(ActiveTab?.Profile?.ThemeVariant ?? ThemeApplier.CurrentMode));
        set
        {
            // -1 comes from the ComboBox while its items are replaced; it is not a choice.
            if (value < 0 || value >= ThemeCatalog.Modes.Length || ActiveTab?.Profile is not { } profile) return;
            var mode = ThemeCatalog.Modes[value];
            if (profile.ThemeVariant == mode) return;
            ApplyProfileTheme(profile with { ThemeVariant = mode });
        }
    }

    // v0.8.16.0: density applies to every tab and is remembered on this computer.
    public IReadOnlyList<string> DensityLabels { get; } = ThemeCatalog.Densities;
    private string _displayPreferencesStatusText = string.Empty;
    public string DisplayPreferencesStatusText { get => _displayPreferencesStatusText; private set => SetField(ref _displayPreferencesStatusText, value); }
    public int SelectedDensityIndex
    {
        get => Array.IndexOf(ThemeCatalog.Densities, ThemeApplier.CurrentDensity);
        set
        {
            if (value < 0 || value >= ThemeCatalog.Densities.Length || ThemeCatalog.Densities[value] == ThemeApplier.CurrentDensity) return;
            ThemeApplier.ApplyDensity(ThemeCatalog.Densities[value]);
            try { _displayPreferences.SaveDensity(ThemeApplier.CurrentDensity); }
            catch (Exception ex) { DisplayPreferencesStatusText = "Density applied, but it could not be remembered: " + ex.Message; }
            RaisePropertyChanged();
        }
    }

    // v0.8.16.0: the operating system switched light/dark. Only a tab set to follow the system changes.
    public void OnSystemThemeChanged()
    {
        // v0.8.25.0: High contrast follows too, since a Windows contrast theme being switched on, off or changed changes it.
        if (ActiveTab?.Profile is not { } profile || ThemeCatalog.NormalizeMode(profile.ThemeVariant) is not ("System" or "HighContrast")) return;
        ThemeApplier.Apply(profile.AccentTheme, profile.ThemeVariant);
        RefreshTabAccentVisuals();
        RaisePropertyChanged(nameof(IsLightMode));
        RaisePropertyChanged(nameof(SelectedThemeModeIndex));
        RaisePropertyChanged(nameof(PageArtwork));
        RaisePropertyChanged(nameof(SetupArtwork));
    }

    // v0.7.79.0: shared by both setters above -- persists the updated profile (this tab's and the
    // saved-profile-list's copies both need to point at the same new record), re-renders the whole
    // app in its theme, and refreshes every open tab's per-tab-color visuals (see
    // RefreshTabAccentVisuals's own comment for why that nudge is needed independent of the theme
    // switch itself).
    private void ApplyProfileTheme(ConnectionProfile updated)
    {
        if (ActiveTab is not { } tab) return;
        tab.Profile = updated;
        var index = Profiles.ToList().FindIndex(p => p.Id == updated.Id);
        if (index >= 0) Profiles[index] = updated;
        _profileStore.Save(Profiles);

        // v0.7.80.0 bugfix: reported live -- clicking any theme button dropped the active tab into
        // "Set Up New Server" mode. Root cause: Profiles[index] = updated above replaces the item
        // with a new object (ConnectionProfile is an immutable record), which desyncs the Settings
        // page's ComboBox -- it's two-way bound to SelectedProfile via SelectedItem, so when Avalonia
        // notices the object it had selected is no longer present in the collection, it clears its
        // own selection, propagating back through the two-way binding as SelectedProfile = null.
        // That flips IsCreatingNewProfile true for the instant before this line runs. Restoring
        // tab.Profile here (a plain reassignment -- unlike SelectedProfile's own setter, it doesn't
        // reset BearerToken/ConnectionState) and re-raising every property that transient null
        // touched puts the tab back exactly where it was, just with the new theme.
        tab.Profile = updated;
        RaisePropertyChanged(nameof(SelectedProfile));
        RaisePropertyChanged(nameof(IsLocalProfile));
        RaisePropertyChanged(nameof(BrowserViewUrlVerbatim));
        RaisePropertyChanged(nameof(IsCreatingNewProfile));
        RaisePropertyChanged(nameof(IsWizardStep1));
        RaisePropertyChanged(nameof(IsWizardStepWorldSource));
        RaisePropertyChanged(nameof(IsWizardStepCloneSource));
        RaisePropertyChanged(nameof(IsWizardStepIdentityPorts));
        RaisePropertyChanged(nameof(IsWizardStepWorldSettingsChoice));
        RaisePropertyChanged(nameof(IsWizardStepInstallDirectory));
        RaisePropertyChanged(nameof(IsWizardStepInstall));
        RaisePropertyChanged(nameof(IsWizardStepMods));
        RaisePropertyChanged(nameof(IsWizardStepSettings));
        RaisePropertyChanged(nameof(IsWizardStepConfirm));

        ThemeApplier.Apply(updated.AccentTheme, updated.ThemeVariant);
        RefreshTabAccentVisuals();
        RaisePropertyChanged(nameof(SelectedAccentTheme));
        RaisePropertyChanged(nameof(IsLightMode));
        RaisePropertyChanged(nameof(SelectedThemeModeIndex));
        RaisePropertyChanged(nameof(PageArtwork));
        RaisePropertyChanged(nameof(SetupArtwork));
    }

    // v0.7.52.0: TabSession.AccentBrush is a plain bound value, not a {DynamicResource}, so it does
    // not auto-refresh when ThemeApplier rewrites the underlying resource on a theme switch -- every
    // open tab needs an explicit nudge afterward. v0.7.63.0: HealthStateColorKey has the exact same
    // problem (its own value doesn't change on a theme switch, but the brush SemanticStatusColorConverter
    // resolves it to does) -- nudged here too rather than a separate method, since both fire from the
    // same two call sites (SelectedAccentTheme/IsLightMode setters).
    private void RefreshTabAccentVisuals()
    {
        foreach (var tab in Tabs) tab.RefreshAccentVisual();
        RaisePropertyChanged(nameof(HealthStateColorKey));
    }

    // v0.7.52.0: Per-Tab Color Coding (item 2) -- resolves the AccentColorKey a profile should use
    // when (re)building it from the editor/tab fields. Reuses an already-known profile's own color
    // (looked up by id, since BuildProfileFromEditor/BuildProfileFromTab construct a brand-new
    // ConnectionProfile record on every save/reconnect) so it survives reconnects and edits rather
    // than being silently reassigned; only a genuinely new profile gets a fresh one, round-robin
    // over ThemeCatalog.TabIdentityColorNames keyed off how many profiles already exist.
    private string ResolveAccentColorKey(string? existingId, ConnectionProfile? tabProfile)
    {
        if (tabProfile is { AccentColorKey.Length: > 0 }) return tabProfile.AccentColorKey;
        var known = !string.IsNullOrWhiteSpace(existingId) ? Profiles.FirstOrDefault(p => p.Id == existingId) : null;
        if (known is not null) return known.AccentColorKey;
        var pool = ThemeCatalog.TabIdentityColorNames;
        return pool[Profiles.Count % pool.Length];
    }

    public ObservableCollection<ConnectionProfile> Profiles { get; } = [];
    public ObservableCollection<DiscoveredMystTiqService> DiscoveredServices { get; } = [];
    public ObservableCollection<PlayerSnapshotDto> OnlinePlayers { get; } = [];
    public ObservableCollection<string> RconOutputLines { get; } = [];
    public IReadOnlyList<string> RconPresetOptions { get; } = ["Info", "ShowPlayers", "Save", "Broadcast Server maintenance message", "Shutdown 60 Server restarting"];
    public ObservableCollection<string> ActivityLines { get; } = [];
    public ObservableCollection<NotificationItemDto> Notifications { get; } = [];
    public ObservableCollection<NotificationItemDto> FilteredNotifications { get; } = [];
    public ObservableCollection<AutomationRuleDto> AutomationRules { get; } = [];
    // v0.7.102.0: with no rules at all nothing is scheduled, including backups. The page says so and points at
    // the New Rule form below, whose defaults are already a nightly 03:00 backup.
    public bool HasNoAutomationRules => AutomationRules.Count == 0;
    public ObservableCollection<AutomationRunRecordDto> AutomationRuns { get; } = [];
    public ObservableCollection<MystTiqPrincipalDto> Principals { get; } = [];
    public ObservableCollection<ServerProfileSummaryDto> FleetServers { get; } = [];
    public ObservableCollection<FleetActionResultDto> FleetActionResults { get; } = [];
    public IReadOnlyList<string> AutomationTriggerKinds { get; } = ["DailyTime", "Interval", "IdleEmpty"];
    public IReadOnlyList<string> AutomationActionKinds { get; } = ["CreateBackup", "StartServer", "StopServer", "RestartServer", "SendNotification", "SendRconCommand"];
    public IReadOnlyList<string> MystTiqRoles { get; } = ["Viewer", "Operator", "Admin", "Owner"];
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
    public ObservableCollection<MetricHistoryPoint> MetricHistory { get; } = [];
    public ObservableCollection<HistoricalMetricPointDto> ResourceHistory { get; } = [];
    public IReadOnlyList<string> HistoryRanges { get; } = ["1 Hour", "6 Hours", "24 Hours", "7 Days", "30 Days"];
    public ObservableCollection<EnvironmentChecklistItemDto> EnvironmentItems { get; } = [];
    public ObservableCollection<OperationRecordDto> RecentOperations { get; } = [];
    public ObservableCollection<DoctorCheckDto> DoctorChecks { get; } = [];
    public ObservableCollection<DiagnosticFindingDto> DiagnosticFindings { get; } = [];
    public ObservableCollection<NetworkDiagnosticCheckDto> NetworkDiagnosticChecks { get; } = [];
    public ObservableCollection<WanReachabilityCheckDto> WanReachabilityChecks { get; } = [];
    public ObservableCollection<DiagnosticFindingDto> LocalConnectionFindings { get; } = [];
    public ObservableCollection<DiagnosticFindingDto> LocalMachineFindings { get; } = [];

    public PlayerSnapshotDto? SelectedOnlinePlayer
    {
        get => _selectedOnlinePlayer;
        set
        {
            if (!SetField(ref _selectedOnlinePlayer, value)) return;
            (Players.KickSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.BanSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.WhisperSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.PromoteSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.GiveItemSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }


    public IReadOnlyList<string> CharacterDispositionOptions { get; } = ["Keep", "Archive", "Delete", "Reset (not yet available)"];

    // Backed by whichever TabSession is ActiveTab rather than a private field, so every
    // existing call site keeps compiling and behaving as before, now scoped to the active tab
    // instead of the whole app -- see SetActiveTabField/RaiseActiveTabPropertiesChanged.
    public ConnectionProfile? SelectedProfile
    {
        get => ActiveTab?.Profile;
        set
        {
            if (!SetActiveTabField(t => t.Profile, (t, v) => t.Profile = v, value))
                return;

            // Raised for both the null and non-null cases -- BeginNewProfile() sets this to null,
            // and IsCreatingNewProfile (which gates the relocated "In-Game Server Defaults" card)
            // depends on exactly that transition, not just a real profile being selected.
            RaisePropertyChanged(nameof(IsLocalProfile));
            RaisePropertyChanged(nameof(BrowserViewUrlVerbatim));
            RaisePropertyChanged(nameof(IsCreatingNewProfile));
            RaisePropertyChanged(nameof(IsWizardStep1));
            RaisePropertyChanged(nameof(IsWizardStepWorldSource));
            RaisePropertyChanged(nameof(IsWizardStepCloneSource));
            RaisePropertyChanged(nameof(IsWizardStepIdentityPorts));
            RaisePropertyChanged(nameof(IsWizardStepWorldSettingsChoice));
            RaisePropertyChanged(nameof(IsWizardStepInstallDirectory));
            RaisePropertyChanged(nameof(IsWizardStepInstall));
            RaisePropertyChanged(nameof(IsWizardStepMods));
            RaisePropertyChanged(nameof(IsWizardStepSettings));
            RaisePropertyChanged(nameof(IsWizardStepConfirm));
            if (value is null)
            {
                // Wizard tabs retain the currently applied theme, including its artwork.
                RaisePropertyChanged(nameof(IsLightMode));
                RaisePropertyChanged(nameof(SelectedThemeModeIndex));
                RaisePropertyChanged(nameof(PageArtwork));
                RaisePropertyChanged(nameof(SetupArtwork));
                return;
            }

            ProfileName = value.Name;
            ServerUrl = value.BaseAddress.ToString().TrimEnd('/');
            CertificateSha256 = value.ServerCertificateSha256 ?? string.Empty;
            TargetServerId = value.ServerId ?? string.Empty;
            // v0.7.71.0: previously always blanked, requiring a fresh paste on every reconnect (the
            // ConnectionProfile model itself never persisted tokens -- see its own docstring). Now
            // tries CredentialStore first, which is a no-op returning null on non-Windows builds or
            // when nothing was ever saved for this profile Id, in which case this still falls back
            // to empty exactly as before.
            BearerToken = _credentialStore.TryLoad(value.Id) ?? string.Empty;
            ManagementApiConnected = false;
            ConnectionState = "Not connected";
            // v0.7.79.0: this profile just became the active tab's own profile (startup's first
            // tab, ConnectExistingProfileTab, or the "Set Up New Server" wizard finishing) --
            // ActiveTab's own setter only applies a theme when the tab ALREADY has a Profile at the
            // moment it becomes active, which isn't true for any of those three cases (this is
            // exactly the call that gives the tab its first Profile), so this is the other of the
            // two places a per-tab theme switch needs to fire.
            ThemeApplier.Apply(value.AccentTheme, value.ThemeVariant);
            RefreshTabAccentVisuals();
            RaisePropertyChanged(nameof(SelectedAccentTheme));
            RaisePropertyChanged(nameof(IsLightMode));
            RaisePropertyChanged(nameof(SelectedThemeModeIndex));
            RaisePropertyChanged(nameof(PageArtwork));
            RaisePropertyChanged(nameof(SetupArtwork));
            Workspace.WorkspaceState = IsLocalProfile
                ? "Local profile: browse and open actions use this computer."
                : "Remote profile: paths belong to the managed server; local browse/open actions are disabled.";
            Workspace.RaiseWorkspaceSummaryProperties();
            (DeleteProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (BootstrapLocalCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string ProfileName
    {
        get => ActiveTab?.ProfileName ?? string.Empty;
        set => SetActiveTabField(t => t.ProfileName, (t, v) => t.ProfileName = v, value);
    }
    public string ServerUrl
    {
        get => ActiveTab?.ServerUrl ?? string.Empty;
        set => SetActiveTabField(t => t.ServerUrl, (t, v) => t.ServerUrl = v, value);
    }
    public string BearerToken
    {
        get => ActiveTab?.BearerToken ?? string.Empty;
        set => SetActiveTabField(t => t.BearerToken, (t, v) => t.BearerToken = v, value);
    }
    // v0.7.64.0: reveal/hide toggle for the Bearer token field(s) -- requested live after a pasted
    // token came out masked with no way to visually verify it before hitting Connect. Deliberately a
    // plain ViewModel-level flag rather than per-tab state: the Settings editor and the new-server
    // wizard's Bearer token fields are never visible at the same time, so one shared flag is enough
    // and avoids adding a field to TabSession for something that isn't really per-connection state.
    private bool _showBearerToken;
    public bool ShowBearerToken { get => _showBearerToken; set => SetField(ref _showBearerToken, value); }
    public string CertificateSha256
    {
        get => ActiveTab?.CertificateSha256 ?? string.Empty;
        set => SetActiveTabField(t => t.CertificateSha256, (t, v) => t.CertificateSha256 = v, value);
    }
    public string TargetServerId
    {
        get => ActiveTab?.TargetServerId ?? string.Empty;
        set => SetActiveTabField(t => t.TargetServerId, (t, v) => t.TargetServerId = v, value);
    }
    public string DiscoveryStateText { get => _discoveryStateText; private set => SetField(ref _discoveryStateText, value); }
    // v0.9.6.0: the address ranges the search covers ("127.0.0.1 · 192.168.1.0/24 (Ethernet)"), its progress, and Cancel.
    public string DiscoveryRangesText { get => _discoveryRangesText; private set { if (SetField(ref _discoveryRangesText, value)) RaisePropertyChanged(nameof(HasDiscoveryRanges)); } }
    public bool HasDiscoveryRanges => !string.IsNullOrWhiteSpace(DiscoveryRangesText);
    public double DiscoveryProgressValue { get => _discoveryProgressValue; private set => SetField(ref _discoveryProgressValue, value); }
    public bool IsDiscovering
    {
        get => _isDiscovering;
        private set
        {
            if (!SetField(ref _isDiscovering, value)) return;
            (DiscoverServicesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }
    public ICommand CancelDiscoveryCommand { get; }
    // Virtual adapters' ranges left out (WSL, Hyper-V, Docker…), extra ranges typed in, and entries that could not be read.
    public string DiscoverySkippedText { get => _discoverySkippedText; private set { if (SetField(ref _discoverySkippedText, value)) RaisePropertyChanged(nameof(HasDiscoverySkipped)); } }
    public bool HasDiscoverySkipped => !string.IsNullOrWhiteSpace(DiscoverySkippedText);
    public string DiscoveryProblemsText { get => _discoveryProblemsText; private set { if (SetField(ref _discoveryProblemsText, value)) RaisePropertyChanged(nameof(HasDiscoveryProblems)); } }
    public bool HasDiscoveryProblems => !string.IsNullOrWhiteSpace(DiscoveryProblemsText);
    public bool DiscoveryIncludeVirtualAdapters { get => _discoveryIncludeVirtualAdapters; set => SetField(ref _discoveryIncludeVirtualAdapters, value); }
    public string DiscoveryExtraRanges { get => _discoveryExtraRanges; set => SetField(ref _discoveryExtraRanges, value ?? string.Empty); }
    public DiscoveredMystTiqService? SelectedDiscoveredService
    {
        get => _selectedDiscoveredService;
        set
        {
            if (!SetField(ref _selectedDiscoveredService, value) || value is null)
                return;
            ServerUrl = value.BaseAddress.ToString().TrimEnd('/');
            if (SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id)
                ProfileName = value.Address == "127.0.0.1" ? "Local MystTiq" : $"MystTiq {value.Address}";
            ConnectionState = "Not connected";
            var isLoopback = System.Net.IPAddress.TryParse(value.Address, out var discoveredAddress) && System.Net.IPAddress.IsLoopback(discoveredAddress);
            Detail = isLoopback && !value.AuthenticationEnabled
                ? $"Discovered local MystTiq service at {value.BaseAddress}. Local loopback service does not require remote credentials; connecting automatically."
                : $"Discovered MystTiq service at {value.BaseAddress}. Enter the bearer token/certificate pin if the remote service requires them, then Connect.";
        }
    }

    public string LocalServiceStatus { get => _localServiceStatus; private set => SetField(ref _localServiceStatus, value); }
    public string LocalPalServerStatus
    {
        get => _localPalServerStatus;
        private set
        {
            if (!SetField(ref _localPalServerStatus, value)) return;
            (StartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }
    public string LocalApiStatus { get => _localApiStatus; private set => SetField(ref _localApiStatus, value); }
    public string LocalServerRootText { get => _localServerRootText; private set => SetField(ref _localServerRootText, value); }
    public string LocalConfigPathText { get => _localConfigPathText; private set => SetField(ref _localConfigPathText, value); }
    public string LocalDiscoverySource { get => _localDiscoverySource; private set => SetField(ref _localDiscoverySource, value); }
    public string LocalDiscoveryDetail { get => _localDiscoveryDetail; private set => SetField(ref _localDiscoveryDetail, value); }
    public string DashboardHealthText
    {
        get => _dashboardHealthText;
        private set
        {
            if (!SetField(ref _dashboardHealthText, value)) return;
            RaisePropertyChanged(nameof(IsHealthGlowGreen));
            RaisePropertyChanged(nameof(IsHealthGlowRed));
            RaisePropertyChanged(nameof(IsHealthGlowAmber));
            RaisePropertyChanged(nameof(IsHealthGlowNeutral));
            RaisePropertyChanged(nameof(HealthStateColorKey));
        }
    }
    public bool IsHealthGlowAmber => IsServerTransitioning || (!IsServerTransitioning && DashboardHealthText is "DEGRADED" or "STARTING");
    public bool IsHealthGlowRed => !IsServerTransitioning && DashboardHealthText == "ATTENTION";
    public bool IsHealthGlowGreen => !IsServerTransitioning && !IsHealthGlowRed && DashboardHealthText == "READY";
    public bool IsHealthGlowNeutral => !IsHealthGlowAmber && !IsHealthGlowRed && !IsHealthGlowGreen;
    // Text color follows the same state the card's glow follows, so the label never reads
    // green on a red card (or vice versa) -- matches whichever glow is currently active. A semantic
    // KEY, not a hex literal -- resolved to the live theme brush by SemanticStatusColorConverter so
    // it follows accent-theme/Light-Dark switches (v0.7.63.0 theme-system bugfix; every setter that
    // raises this must also re-raise it after ThemeApplier.Apply, since the key itself doesn't
    // change on a theme switch but the brush it resolves to does -- see SelectedAccentTheme/IsLightMode).
    public string HealthStateColorKey => IsHealthGlowRed ? "Red" : IsHealthGlowAmber ? "Amber" : IsHealthGlowGreen ? "Green" : "Muted";
    public double CpuPercentValue { get => _cpuPercentValue; private set => SetField(ref _cpuPercentValue, value); }
    public double MemoryMbValue { get => _memoryMbValue; private set => SetField(ref _memoryMbValue, value); }
    public double MemoryScaleValue { get => _memoryScaleValue; private set => SetField(ref _memoryScaleValue, value); }
    public string SelectedHistoryRange
    {
        get => _selectedHistoryRange;
        set
        {
            if (!SetField(ref _selectedHistoryRange, value)) return;
            RunUi(() => RefreshHistoricalMetricsAsync());
        }
    }
    public string HistoryCpuSummary { get => _historyCpuSummary; private set => SetField(ref _historyCpuSummary, value); }
    public string HistoryMemorySummary { get => _historyMemorySummary; private set => SetField(ref _historyMemorySummary, value); }
    public string HistoryFpsSummary { get => _historyFpsSummary; private set => SetField(ref _historyFpsSummary, value); }
    public string HistorySampleSummary { get => _historySampleSummary; private set => SetField(ref _historySampleSummary, value); }
    public string HistoryStatusText { get => _historyStatusText; private set => SetField(ref _historyStatusText, value); }

    public bool ManagementApiConnected
    {
        get => ActiveTab?.ManagementApiConnected ?? false;
        private set
        {
            if (!SetActiveTabField(t => t.ManagementApiConnected, (t, v) => t.ManagementApiConnected = v, value)) return;
            RaiseManagementApiConnectedDependents();
        }
    }
    private void RaiseManagementApiConnectedDependents()
    {
        (Backups.CreateBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (StartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (StopCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RestartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RestartFromDiagnosticsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (ForceStopServerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RefreshAllInstancesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (TerminateSelectedInstanceCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        // v0.8.14.0: the Fleet Actions buttons also depend on the connection, but were never re-evaluated, so they
        // stayed greyed out after connecting (found in the live check for this version).
        (BackupAllCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (DoctorAllCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (UpdateAllCommand as AsyncCommand)?.RaiseCanExecuteChanged();
    }
    // Set from the real status poll's ServerStatusDto.Ready (see ApplyStatus), not
    // parsed from ServerState's display text -- Start greys out while already running, Stop/
    // Restart grey out while already stopped, instead of staying clickable regardless of the
    // server's actual lifecycle phase.
    public bool ServerIsRunning
    {
        get => ActiveTab?.ServerIsRunning ?? false;
        private set
        {
            if (!SetActiveTabField(t => t.ServerIsRunning, (t, v) => t.ServerIsRunning = v, value)) return;
            RaiseServerIsRunningDependents();
        }
    }
    private void RaiseServerIsRunningDependents()
    {
        (StartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (StopCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RestartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RestartFromDiagnosticsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (ForceStopServerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
    }
    public string LifecycleStatusText { get => _lifecycleStatusText; private set => SetField(ref _lifecycleStatusText, value); }
    public string ActivityState { get => _activityState; private set => SetField(ref _activityState, value); }
    public IReadOnlyList<string> ActivitySeverityOptions { get; } = ["All", "Information", "Warning", "Error"];
    public IReadOnlyList<string> ActivityCategoryOptions { get; } = ["All", "API", "Backups", "Players", "MODs", "World Transaction", "Notifications"];
    public string ActivitySearchText { get => _activitySearchText; set { if (SetField(ref _activitySearchText, value ?? string.Empty)) ApplyActivityFilters(); } }
    public string ActivitySeverityFilter { get => _activitySeverityFilter; set { if (SetField(ref _activitySeverityFilter, value ?? "All")) ApplyActivityFilters(); } }
    public string ActivityCategoryFilter { get => _activityCategoryFilter; set { if (SetField(ref _activityCategoryFilter, value ?? "All")) ApplyActivityFilters(); } }
    public string NotificationState { get => _notificationState; private set => SetField(ref _notificationState, value); }
    public IReadOnlyList<string> NotificationSeverityOptions { get; } = ["All", "Information", "Success", "Warning", "Critical"];
    public string NotificationSearchText { get => _notificationSearchText; set { if (SetField(ref _notificationSearchText, value ?? string.Empty)) ApplyNotificationFilters(); } }
    public string NotificationSeverityFilter { get => _notificationSeverityFilter; set { if (SetField(ref _notificationSeverityFilter, value ?? "All")) ApplyNotificationFilters(); } }
    public NotificationItemDto? SelectedNotification { get => _selectedNotification; set { if (SetField(ref _selectedNotification, value)) { (ToggleNotificationReadCommand as AsyncCommand)?.RaiseCanExecuteChanged(); (ToggleNotificationPinCommand as AsyncCommand)?.RaiseCanExecuteChanged(); (DismissNotificationCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } } }
    public string NotificationBadgeText => Notifications.Count(x => !x.Read).ToString();

    public string AutomationState { get => _automationState; private set => SetField(ref _automationState, value); }
    public AutomationRuleDto? SelectedAutomationRule
    {
        get => _selectedAutomationRule;
        set { if (SetField(ref _selectedAutomationRule, value)) { (DeleteAutomationRuleCommand as AsyncCommand)?.RaiseCanExecuteChanged(); (ToggleAutomationRuleEnabledCommand as AsyncCommand)?.RaiseCanExecuteChanged(); (RunAutomationRuleNowCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    }
    public string NewAutomationRuleName { get => _newAutomationRuleName; set { if (SetField(ref _newAutomationRuleName, value ?? string.Empty)) (CreateAutomationRuleCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public string NewAutomationTriggerKind { get => _newAutomationTriggerKind; set => SetField(ref _newAutomationTriggerKind, value ?? "DailyTime"); }
    public string NewAutomationTimeOfDayUtc { get => _newAutomationTimeOfDayUtc; set => SetField(ref _newAutomationTimeOfDayUtc, value ?? "03:00"); }
    public int NewAutomationIntervalMinutes { get => _newAutomationIntervalMinutes; set => SetField(ref _newAutomationIntervalMinutes, value); }
    public int NewAutomationIdleThresholdMinutes { get => _newAutomationIdleThresholdMinutes; set => SetField(ref _newAutomationIdleThresholdMinutes, value); }
    public string NewAutomationActionKind { get => _newAutomationActionKind; set => SetField(ref _newAutomationActionKind, value ?? "CreateBackup"); }
    public string NewAutomationNotificationTitle { get => _newAutomationNotificationTitle; set => SetField(ref _newAutomationNotificationTitle, value ?? string.Empty); }
    public string NewAutomationNotificationMessage { get => _newAutomationNotificationMessage; set => SetField(ref _newAutomationNotificationMessage, value ?? string.Empty); }
    public string NewAutomationRconCommand { get => _newAutomationRconCommand; set => SetField(ref _newAutomationRconCommand, value ?? string.Empty); }

    public string SecurityState { get => _securityState; private set => SetField(ref _securityState, value); }
    public MystTiqPrincipalDto? SelectedPrincipal { get => _selectedPrincipal; set => SetField(ref _selectedPrincipal, value); }
    public MystTiqPrincipalDto? CurrentPrincipal { get => _currentPrincipal; private set => SetField(ref _currentPrincipal, value); }
    public string NewPrincipalName { get => _newPrincipalName; set { if (SetField(ref _newPrincipalName, value ?? string.Empty)) (CreatePrincipalCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public string NewPrincipalRole { get => _newPrincipalRole; set => SetField(ref _newPrincipalRole, value ?? "Operator"); }
    public string? LastCreatedPrincipalToken { get => _lastCreatedPrincipalToken; private set { if (SetField(ref _lastCreatedPrincipalToken, value)) RaisePropertyChanged(nameof(HasLastCreatedPrincipalToken)); } }
    public bool HasLastCreatedPrincipalToken => !string.IsNullOrEmpty(LastCreatedPrincipalToken);
    // The visible client-side payoff of RBAC: buttons a Viewer/Operator principal cannot use are disabled.
    // v0.8.14.0: one rule for every card (RoleAccess): hidden when the role cannot use it at all, read-only when it can
    // only read. CanOperate is new; Fleet Actions (Operator on the server) was wrongly gated at Admin.
    public bool CanOperate => RoleAccess.Allows(CurrentPrincipal is not null, CurrentPrincipal?.Role, RoleAccess.Operator);
    public bool CanManageAdmin => RoleAccess.Allows(CurrentPrincipal is not null, CurrentPrincipal?.Role, RoleAccess.Admin);
    public bool CanManagePrincipals => RoleAccess.Allows(CurrentPrincipal is not null, CurrentPrincipal?.Role, RoleAccess.Owner);
    public string RoleHiddenNotice => RoleAccess.HiddenNotice(CurrentPrincipal is not null, CurrentPrincipal?.Role);
    public bool HasRoleHiddenNotice => RoleHiddenNotice.Length > 0;

    public string AlertCenterState { get => _alertCenterState; private set => SetField(ref _alertCenterState, value); }
    public AlertRuleSetDto AlertRules { get => _alertRules; set => SetField(ref _alertRules, value ?? new()); }
    public DiskSpacePredictionDto? DiskSpacePrediction { get => _diskSpacePrediction; private set => SetField(ref _diskSpacePrediction, value); }

    public string DiscordBotState { get => _discordBotState; private set => SetField(ref _discordBotState, value); }
    public DiscordBotConfigurationDto DiscordBotConfig { get => _discordBotConfig; set => SetField(ref _discordBotConfig, value ?? new()); }
    public bool DiscordBotTokenConfigured { get => _discordBotTokenConfigured; private set { if (SetField(ref _discordBotTokenConfigured, value)) RaisePropertyChanged(nameof(DiscordBotTokenWatermark)); } }
    public string DiscordBotTokenWatermark => DiscordBotTokenConfigured ? "Token configured -- leave blank to keep it" : "Paste bot token from the Discord Developer Portal";
    public string DiscordBotConnectionState { get => _discordBotConnectionState; private set => SetField(ref _discordBotConnectionState, value); }
    public ObservableCollection<DiscordRoleMappingDto> DiscordRoleMappings { get; } = [];
    public string NewRoleMappingDiscordRoleId { get => _newRoleMappingDiscordRoleId; set => SetField(ref _newRoleMappingDiscordRoleId, value ?? string.Empty); }
    public string NewRoleMappingRole { get => _newRoleMappingRole; set => SetField(ref _newRoleMappingRole, value ?? "Viewer"); }
    public DiscordRoleMappingDto? SelectedRoleMapping { get => _selectedRoleMapping; set => SetField(ref _selectedRoleMapping, value); }


    public string AntiCheatState { get => _antiCheatState; private set => SetField(ref _antiCheatState, value); }
    public AntiCheatRuleSetDto AntiCheatRules { get => _antiCheatRules; set => SetField(ref _antiCheatRules, value ?? new()); }
    public ObservableCollection<AntiCheatFindingDto> AntiCheatFindings { get; } = [];
    public IReadOnlyList<string> AntiCheatResponses { get; } = ["Flag", "Kick", "Ban"];
    public string FleetState { get => _fleetState; private set => SetField(ref _fleetState, value); }
    public string CloneNewProfileId { get => _cloneNewProfileId; set { if (SetField(ref _cloneNewProfileId, value ?? string.Empty)) (CloneWorldCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public string CloneNewProfileName { get => _cloneNewProfileName; set => SetField(ref _cloneNewProfileName, value ?? string.Empty); }
    public string CloneWorldStatusText { get => _cloneWorldStatusText; private set => SetField(ref _cloneWorldStatusText, value); }
    // v0.7.84.0: a successful clone here previously left the "needs a MystTiq service restart to
    // come online" text as the only signal, with no in-app way to act on it (the same
    // previously-undisclosed gap closed for the wizard's own Clone path in v0.7.83.0). Unlike the
    // wizard's brand-new, not-yet-in-use tab, this page's connection is already actively in use, so
    // the restart is offered as an explicit button rather than fired automatically -- the user
    // decides when it's convenient to take the whole local service down briefly.
    public bool CloneWorldNeedsRestart
    {
        get => _cloneWorldNeedsRestart;
        private set
        {
            if (!SetField(ref _cloneWorldNeedsRestart, value)) return;
            (RestartAfterCloneCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }
    public string? CloneWorldNewProfileId { get => _cloneWorldNewProfileId; private set => SetField(ref _cloneWorldNewProfileId, value); }
    public ICommand RestartAfterCloneCommand { get; }
    // v0.7.81.0: Clone World turned into an explicit step-by-step workflow (direct request: "should
    // be a workflow with drop down options to clarify what it is doing"). The source was always
    // implicitly "whichever profile this tab is connected to" (SelectedProfile) -- now labeled
    // explicitly rather than left unstated. PortOffset already existed on WorldCloneRequestDto but
    // was never exposed in the UI at all (the server just used its own default); now a real dropdown.
    public IReadOnlyList<int> CloneWorldPortOffsetOptions { get; } = [100, 200, 300, 400];
    public int CloneWorldPortOffset { get => _cloneWorldPortOffset; set => SetField(ref _cloneWorldPortOffset, value); }
    public bool CloneWorldConfirmed { get => _cloneWorldConfirmed; set { if (SetField(ref _cloneWorldConfirmed, value)) (CloneWorldCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public string ActivityFileText { get => _activityFileText; private set => SetField(ref _activityFileText, value); }
    public string ActivityDetail { get => _activityDetail; private set => SetField(ref _activityDetail, value); }

    public string ConnectionState
    {
        get => ActiveTab?.ConnectionState ?? "Not connected";
        private set
        {
            if (!SetActiveTabField(t => t.ConnectionState, (t, v) => t.ConnectionState = v, value)) return;
            RaiseConnectionStateDependents();
        }
    }
    private void RaiseConnectionStateDependents()
    {
        (Backups.CreateBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (StartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (StopCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RestartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RestartFromDiagnosticsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (ForceStopServerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        RaisePropertyChanged(nameof(IsServerGlowGreen));
        RaisePropertyChanged(nameof(IsServerGlowRed));
        RaisePropertyChanged(nameof(IsServerGlowAmber));
        RaisePropertyChanged(nameof(IsHealthGlowGreen));
        RaisePropertyChanged(nameof(IsHealthGlowRed));
        RaisePropertyChanged(nameof(IsHealthGlowAmber));
        RaisePropertyChanged(nameof(IsHealthGlowNeutral));
        RaisePropertyChanged(nameof(HealthStateColorKey));
    }
    public bool IsServerTransitioning => ConnectionState is "Starting…" or "Stopping…" or "Restarting…";
    public string ServerState
    {
        get => _serverState;
        private set
        {
            if (!SetField(ref _serverState, value)) return;
            RaisePropertyChanged(nameof(IsServerGlowGreen));
            RaisePropertyChanged(nameof(IsServerGlowRed));
            RaisePropertyChanged(nameof(IsServerGlowAmber));
        }
    }
    // Traffic-light dashboard glow, checked in priority order: a lifecycle operation in flight
    // (Starting/Stopping/Restarting) always reads as amber "in progress" regardless of the last
    // known ServerState, Running is green, and everything else (Stopped, Crash, Unknown,
    // Checking, ...) reads as red -- the server is not currently up.
    // v0.7.82.0: a detected-but-not-yet-ready native process (ServerState "Starting / Not Ready")
    // used to fall through to red here, the same "genuinely stopped" color as an actual stop --
    // matches Overall Health's own amber treatment of that same in-between state.
    public bool IsServerGlowAmber => IsServerTransitioning || ServerState.Contains("Starting", StringComparison.OrdinalIgnoreCase);
    public bool IsServerGlowGreen => !IsServerTransitioning && ServerState.Contains("Running", StringComparison.OrdinalIgnoreCase);
    public bool IsServerGlowRed => !IsServerTransitioning && !IsServerGlowGreen && !IsServerGlowAmber;
    public string ServiceState { get => _serviceState; private set => SetField(ref _serviceState, value); }
    public string Detail { get => _detail; private set => SetField(ref _detail, value); }
    public string NativePidText { get => _nativePidText; private set => SetField(ref _nativePidText, value); }

    // v0.7.28.0: ServerStatusDto.Processes already flowed end-to-end from the headless host's
    // FindManagedServerProcesses (server-side) through ServerLifecycleSnapshot -- nothing on the
    // Desktop side ever read it before this. Populated in ApplyStatus.
    public ObservableCollection<ServerProcessDto> ManagedProcesses { get; } = [];
    public bool HasManagedProcesses => ManagedProcesses.Count > 0;

    // v0.7.44.0: machine-wide Palworld instance list (Doctor page), independent of the
    // per-profile ManagedProcesses list above -- this can include instances belonging to a
    // different install/profile entirely. Populated by RefreshAllInstancesAsync.
    public ObservableCollection<ServerInstanceDto> AllInstances { get; } = [];
    public bool HasAllInstances => AllInstances.Count > 0;
    public string InstanceTerminationResultText
    {
        get => _instanceTerminationResultText;
        private set { if (SetField(ref _instanceTerminationResultText, value)) RaisePropertyChanged(nameof(HasInstanceTerminationResult)); }
    }
    public bool HasInstanceTerminationResult => !string.IsNullOrWhiteSpace(InstanceTerminationResultText);
    public ServerInstanceDto? SelectedInstance
    {
        get => _selectedInstance;
        set
        {
            if (SetField(ref _selectedInstance, value))
            {
                RaisePropertyChanged(nameof(HasSelectedInstance));
                RaisePropertyChanged(nameof(IsSelectedInstanceManaged));
                InstanceTerminationResultText = string.Empty;
                (TerminateSelectedInstanceCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            }
        }
    }
    public bool HasSelectedInstance => SelectedInstance is not null;
    // A raw kill is only ever offered for an instance NOT confirmed as this profile's own managed
    // process -- see ServerInstanceInfo's ManagedByThisProfile doc comment for why that flag is
    // deliberately conservative. The managed case reuses ForceStopServerCommand instead (safe,
    // crash-recovery-aware stop through this profile's own Lifecycle service).
    public bool IsSelectedInstanceManaged => SelectedInstance?.ManagedByThisProfile ?? false;
    public string ListenerText { get => _listenerText; private set => SetField(ref _listenerText, value); }
    public string LastObservedText { get => _lastObservedText; private set => SetField(ref _lastObservedText, value); }
    public string LastTransitionText { get => _lastTransitionText; private set => SetField(ref _lastTransitionText, value); }
    public string UptimeText { get => _uptimeText; private set => SetField(ref _uptimeText, value); }
    public string PlayersState { get => _playersState; private set => SetField(ref _playersState, value); }
    public string OnlinePlayerCountText { get => _onlinePlayerCountText; private set => SetField(ref _onlinePlayerCountText, value); }







    public string CpuText { get => _cpuText; private set => SetField(ref _cpuText, value); }
    // v0.7.9.0: real in-game simulation FPS/frame time from Palworld's own REST /metrics endpoint --
    // independent of the host-level CpuText above, and "—" whenever the Palworld REST API itself
    // (not just the managed process) is unavailable, not tied to the managed-process Available flag.
    public string ServerFpsText { get => _serverFpsText; private set => SetField(ref _serverFpsText, value); }
    public string ServerFrameTimeText { get => _serverFrameTimeText; private set => SetField(ref _serverFrameTimeText, value); }
    public string MemoryText { get => _memoryText; private set => SetField(ref _memoryText, value); }
    public string ThreadCountText { get => _threadCountText; private set => SetField(ref _threadCountText, value); }
    public string MonitoringDetail { get => _monitoringDetail; private set => SetField(ref _monitoringDetail, value); }
    public string LogFileText { get => _logFileText; private set => SetField(ref _logFileText, value); }
    public string RconState { get => _rconState; private set => SetField(ref _rconState, value); }
    public string RconDetail { get => _rconDetail; private set => SetField(ref _rconDetail, value); }
    public bool RconConnected { get => _rconConnected; private set => SetField(ref _rconConnected, value); }
    public string RconCommandText { get => _rconCommandText; set { if (SetField(ref _rconCommandText, value ?? string.Empty)) (RconSendCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public string SelectedRconPreset { get => _selectedRconPreset; set { if (SetField(ref _selectedRconPreset, value ?? "Info")) RconCommandText = _selectedRconPreset; } }

    public string DoctorStatus { get => _doctorStatus; private set => SetField(ref _doctorStatus, value); }
    public string DoctorSummary { get => _doctorSummary; private set => SetField(ref _doctorSummary, value); }
    public string DoctorCheckedAt { get => _doctorCheckedAt; private set => SetField(ref _doctorCheckedAt, value); }
    public string DoctorExportPath { get => _doctorExportPath; private set => SetField(ref _doctorExportPath, value); }
    public DiagnosticsReportDto? LatestDiagnosticsReport { get => _latestDiagnosticsReport; private set => SetField(ref _latestDiagnosticsReport, value); }
    public string DiagnosticsReportDetail { get => _diagnosticsReportDetail; private set => SetField(ref _diagnosticsReportDetail, value); }
    public string DistributionState { get => _distributionState; private set => SetField(ref _distributionState, value); }
    public string DistributionDetail { get => _distributionDetail; private set => SetField(ref _distributionDetail, value); }

    // v0.7.45.0: Update Center Overhaul -- split by Group to match the two labeled sections
    // (Core Server / Save & Runtime Dependencies) the v0.2.16.4 reference used.
    public ObservableCollection<ComponentVersionDto> CoreServerComponents { get; } = [];
    public ObservableCollection<ComponentVersionDto> SaveRuntimeDependencyComponents { get; } = [];
    public bool HasComponentVersions => CoreServerComponents.Count > 0 || SaveRuntimeDependencyComponents.Count > 0;
    public string ComponentVersionsCheckedAtText { get => _componentVersionsCheckedAtText; private set => SetField(ref _componentVersionsCheckedAtText, value); }
    public string SteamCmdState { get => _steamCmdState; private set => SetField(ref _steamCmdState, value); }
    public string ServerInstallState { get => _serverInstallState; private set => SetField(ref _serverInstallState, value); }
    public string DistributionPlatform { get => _distributionPlatform; private set => SetField(ref _distributionPlatform, value); }
    public string DistributionPlanText { get => _distributionPlanText; private set => SetField(ref _distributionPlanText, value); }
    public string DistributionOutputText
    {
        get => _distributionOutputText;
        private set { if (SetField(ref _distributionOutputText, value)) RaisePropertyChanged(nameof(HasDistributionOutput)); }
    }
    public bool HasDistributionOutput => !string.IsNullOrEmpty(DistributionOutputText);
    public bool ValidateServerFiles { get => _validateServerFiles; set => SetField(ref _validateServerFiles, value); }
    public string SemanticStateText { get => _semanticStateText; private set => SetField(ref _semanticStateText, value); }
    public string SemanticSourceText { get => _semanticSourceText; private set => SetField(ref _semanticSourceText, value); }
    public IReadOnlyList<string> PalGenderOptions { get; } = ["Male", "Female"];




    public string StatusBarText { get => _statusBarText; private set => SetField(ref _statusBarText, value); }
    public string StatusBarObservedText { get => _statusBarObservedText; private set => SetField(ref _statusBarObservedText, value); }

    public bool AutoRefreshEnabled
    {
        get => _autoRefreshEnabled;
        set => SetField(ref _autoRefreshEnabled, value);
    }

    public bool IsBusy
    {
        get => ActiveTab?.IsBusy ?? false;
        private set
        {
            if (!SetActiveTabField(t => t.IsBusy, (t, v) => t.IsBusy = v, value))
                return;

            RaiseIsBusyDependents(value);
        }
    }
    // v0.7.22.0: requested directly -- the footer's IsBusy indicator previously said only
    // "Working…" no matter which of the many possible operations was actually running. Threaded
    // through the highest-value operations first (server lifecycle, backup create/restore, world
    // transaction apply) rather than attempting blanket coverage of every IsBusy = true site --
    // an operation that doesn't set this falls back to the same generic "Working…" as before, a
    // disclosed gap rather than a regression.
    public string? BusyReason { get => _busyReason; set => SetField(ref _busyReason, value); }
    // v0.7.43.0: findings-completeness fix (item 6). Elapsed time and which server the busy
    // operation belongs to, both genuinely missing before -- the reason text alone gave no sense
    // of how long an operation had been running, and BusyReason itself is a flat field (not
    // per-tab), so a stale reason from a different tab could otherwise linger visually after
    // switching tabs mid-operation. Real sub-step/percent-complete progress remains a disclosed
    // gap (most operations here are all-or-nothing REST calls, not streamed progress -- would
    // need backend changes too, per the original finding's own note).
    public string BusyElapsedText { get => _busyElapsedText; private set => SetField(ref _busyElapsedText, value); }

    private void RaiseIsBusyDependents(bool value)
    {
            SignalBusy(value);
            if (value)
            {
                _busyStartedAt = DateTimeOffset.UtcNow;
                _busyServerName = ActiveTab?.ProfileName;
                BusyElapsedText = "0s";
                _busyElapsedTimer.Start();
            }
            else
            {
                _busyElapsedTimer.Stop();
                _busyStartedAt = null;
                _busyServerName = null;
                BusyElapsedText = string.Empty;
            }

            var reasonWithServer = value
                ? (BusyReason ?? "Working…") + (string.IsNullOrWhiteSpace(_busyServerName) ? string.Empty : $" ({_busyServerName})")
                : string.Empty;
            StatusBarText = value ? reasonWithServer : (ConnectionState == "Connected" ? $"Connected — {ServerState}" : ConnectionState);

            RaiseAllAsyncCommandStates();
            Mods.RaiseNexusCommandStates();
            Players.RaiseKitCommandStates();
            Host.RaiseHostCommandStates();
            Players._loadGameIdsCommand?.RaiseCanExecuteChanged();
            (ConnectCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DetectLocalServiceCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DiscoverServicesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RefreshMonitoringCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RefreshPlayersCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RefreshActivityCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RefreshNotificationsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (NotificationSelfTestCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (MarkAllNotificationsReadCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ToggleNotificationReadCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ToggleNotificationPinCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DismissNotificationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RefreshEnvironmentCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (VerifyEnvironmentCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (CreateDefaultServerSettingsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.KickSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.BanSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.UnbanSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Map.TeleportPlayerToMeCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Map.TeleportToPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (SaveWorldNowCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.RefreshBanListCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.RefreshTemporaryBansCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.CreateTemporaryBanCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.WhisperSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.PromoteSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.GiveItemSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.LoadPlayerMetadataCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.SavePlayerNotesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.AddPlayerWarningCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (World.OpenSelectedGuildLeaderCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Backups.RefreshBackupsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Backups.CreateBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Backups.DeleteBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Backups.RestoreBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Backups.VerifySelectedBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Backups.VerifyAllBackupsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Backups.PreviewBackupRetentionCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Backups.ApplyBackupRetentionCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Mods.DeleteSelectedModCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Mods.RollbackSelectedModCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Mods.EnableAllModsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Mods.DisableAllModsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Mods.RepairModsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Mods.PreviewUe4ssInstallCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Mods.ApplyUe4ssInstallCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Mods.RollbackUe4ssInstallCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Workspace.ValidateActiveWorldCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (World.ApplyWorldTransactionCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Configuration.LoadConfigurationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Configuration.SaveConfigurationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Configuration.SavePalworldConfigurationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            // v0.7.82.0 bug fix: both also gate on !IsBusy but were never re-queried here, only from
            // the Configuration.PalworldConfigLoaded setter -- which fires mid-load, while IsBusy is still true, so
            // CanExecute evaluated false right then and stayed stuck disabled for the rest of the
            // session even after IsBusy went back to false (nothing else ever re-raised these two).
            (GenerateServerNameCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (Configuration.SaveCurrentAsPresetCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RestartAfterCloneCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (StartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (StopCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RestartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RestartFromDiagnosticsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ForceStopServerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            // v0.8.14.0: the Fleet Actions buttons gate on !IsBusy too but were never re-queried, so they stayed disabled.
            (BackupAllCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DoctorAllCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (UpdateAllCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (InstallMissingEnvironmentCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RefreshAllInstancesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (TerminateSelectedInstanceCommand as AsyncCommand)?.RaiseCanExecuteChanged();
    }

    public NavigationPage SelectedPage
    {
        get => _selectedPage;
        private set
        {
            if (!SetField(ref _selectedPage, value))
                return;

            // v0.7.74.0: kept in sync with whichever tab is active so ActiveTab's setter can
            // restore it on the way back -- see TabSession.LastPage's own comment. Written here
            // (the single setter every navigation already funnels through) rather than at each
            // call site, so no future navigation call site can forget it.
            if (ActiveTab is not null) ActiveTab.LastPage = value;
            // v1.0.8.0 (roadmap C-0): each page ViewModel is told when its page is shown (the Console then brings its
            // filtered view up to date, v1.0.7.3).
            foreach (var page in _pages) if (page.Page == value) page.OnShown();

            RaisePropertyChanged(nameof(PageTitle));
            RaisePropertyChanged(nameof(PageSubtitle));
            RaisePageVisibility();

            // v0.7.78.0: MOD Library's own local Steam Workshop scan used to require a manual
            // "Refresh" click every time the page was opened, even on a fresh navigation -- direct
            // report that it should just load automatically, matching how the Players page already
            // auto-refreshes on navigation (see ActiveTab's own setter). Only fires once per tab
            // (Mods.WorkshopItems.Count == 0 guards it) rather than re-scanning every single time the
            // page is revisited, since Explorer's own Refresh button already exists for that.
            if (value == NavigationPage.ModLibrary && Mods.WorkshopItems.Count == 0)
                _ = Mods.ScanWorkshopModsAsync();
        }
    }

    // v0.8.5.0: page headers come from the translation files (Assets/i18n); English, the fallback, holds exactly the
    // text these used to hardcode.
    public string PageTitle => Localizer.Instance[$"page.{SelectedPage}.title"];
    public string PageSubtitle => Localizer.Instance[$"page.{SelectedPage}.subtitle"];

    // v0.8.5.0: display language, a Desktop-local preference (Settings > Appearance). Applies at once.
    private readonly LocalLanguagePreferenceStore _languageStore = new();
    public IReadOnlyList<UiLanguage> UiLanguages => Localizer.Languages;
    public UiLanguage SelectedUiLanguage
    {
        get => Localizer.Languages.FirstOrDefault(l => l.Code == Localizer.Instance.LanguageCode) ?? Localizer.Languages[0];
        set
        {
            if (value is null || value.Code == Localizer.Instance.LanguageCode) return;
            Localizer.Instance.SetLanguage(value.Code);
            _languageStore.Save(value.Code);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RebuildRibbonGroups();
        RaisePropertyChanged(nameof(PageTitle));
        RaisePropertyChanged(nameof(PageSubtitle));
        RaisePropertyChanged(nameof(SelectedUiLanguage));
        // v0.9.2.0: item and Pal names come in the display language; a loaded picker is read again (the map refreshes itself).
        Players.OnLanguageChanged();
    }

    public bool ShowGlobalPageHeader => true;
    public bool IsDashboardPage => SelectedPage == NavigationPage.Dashboard;
    public bool IsServerPage => SelectedPage == NavigationPage.Configuration;
    public bool IsSetupUpdatePage => SelectedPage is NavigationPage.ServerSetup or NavigationPage.UpdateCenter;
    public bool IsServerSetupPage => SelectedPage == NavigationPage.ServerSetup;
    public bool IsUpdateCenterPage => SelectedPage == NavigationPage.UpdateCenter;
    public bool IsWorldExplorerPage => SelectedPage == NavigationPage.Inspector;
    public bool IsWorldTransactionsPage => SelectedPage == NavigationPage.WorldTransactions;
    public bool IsPlayerGuildExplorerPage => SelectedPage is NavigationPage.Bases or NavigationPage.Guilds;
    public bool IsPlayersPage => SelectedPage == NavigationPage.Players;
    public bool IsBasesPage => SelectedPage == NavigationPage.Bases;
    public bool IsGuildsPage => SelectedPage == NavigationPage.Guilds;
    public bool IsMapPage => SelectedPage == NavigationPage.Map;
    public bool IsMonitoringPage => SelectedPage is NavigationPage.Console or NavigationPage.ActivityAudit;
    public bool IsConsolePage => SelectedPage == NavigationPage.Console;
    public bool IsActivityAuditPage => SelectedPage == NavigationPage.ActivityAudit;
    public bool IsNotificationsPage => SelectedPage == NavigationPage.Notifications;
    public bool IsAutomationPage => SelectedPage == NavigationPage.Automation;
    public bool IsSecurityPage => SelectedPage == NavigationPage.Security;
    public bool IsAlertCenterPage => SelectedPage == NavigationPage.AlertCenter;
    public bool IsFleetPage => SelectedPage == NavigationPage.Fleet;
    public bool IsBackupsPage => SelectedPage == NavigationPage.Backups;
    public bool IsWorkspacePage => SelectedPage == NavigationPage.Workspace;
    public bool IsLauncherPage => SelectedPage == NavigationPage.Launcher;
    public bool IsModsPage => SelectedPage is NavigationPage.ModDashboard or NavigationPage.ModLibrary or NavigationPage.Ue4ss;
    public bool IsModDashboardPage => SelectedPage == NavigationPage.ModDashboard;
    public bool IsModLibraryPage => SelectedPage == NavigationPage.ModLibrary;
    public bool IsUe4ssPage => SelectedPage == NavigationPage.Ue4ss;
    public bool IsModInventoryPage => SelectedPage is NavigationPage.ModDashboard or NavigationPage.ModLibrary;
    public bool IsDoctorPage => SelectedPage == NavigationPage.Doctor;
    public bool IsDiagnosticsPage => SelectedPage == NavigationPage.DiagnosticsCenter;
    public bool IsSettingsPage => SelectedPage == NavigationPage.Settings;
    public bool IsPlaceholderPage => false;
    public bool IsCrashAnalyzerPage => SelectedPage == NavigationPage.CrashAnalyzer;
    public bool IsSaveToolsPage => SelectedPage == NavigationPage.SaveTools;
    public bool IsHomeGroupSelected => SelectedPage == NavigationPage.Dashboard;
    public bool IsServerGroupSelected => SelectedPage is NavigationPage.ServerSetup or NavigationPage.Configuration or NavigationPage.Backups or NavigationPage.Console or NavigationPage.Workspace or NavigationPage.Launcher;
    public bool IsWorldGroupSelected => SelectedPage is NavigationPage.Inspector or NavigationPage.WorldTransactions or NavigationPage.Players or NavigationPage.Bases or NavigationPage.Guilds or NavigationPage.Map;
    public bool IsModsGroupSelected => SelectedPage is NavigationPage.ModDashboard or NavigationPage.ModLibrary or NavigationPage.Ue4ss;
    public bool IsToolsGroupSelected => SelectedPage is NavigationPage.UpdateCenter or NavigationPage.Doctor or NavigationPage.CrashAnalyzer or NavigationPage.SaveTools or NavigationPage.DiagnosticsCenter;
    public bool IsSystemGroupSelected => SelectedPage is NavigationPage.Settings or NavigationPage.Notifications or NavigationPage.ActivityAudit or NavigationPage.Automation or NavigationPage.Security or NavigationPage.AlertCenter or NavigationPage.Fleet;
    public bool IsV5HomeCategory => SelectedPage == NavigationPage.Dashboard;
    public bool IsV5ServerCategory => SelectedPage is NavigationPage.ServerSetup or NavigationPage.Configuration or NavigationPage.Console or NavigationPage.Workspace or NavigationPage.Launcher;
    public bool IsV5WorldCategory => SelectedPage is NavigationPage.Inspector or NavigationPage.WorldTransactions or NavigationPage.Players or NavigationPage.Bases or NavigationPage.Guilds or NavigationPage.Map;
    public bool IsV5BackupsCategory => SelectedPage == NavigationPage.Backups;
    public bool IsV5ModsCategory => IsModsGroupSelected;
    public bool IsV5ToolsCategory => IsToolsGroupSelected;
    public bool IsV5SystemCategory => SelectedPage is NavigationPage.Settings or NavigationPage.ActivityAudit or NavigationPage.Notifications or NavigationPage.Automation or NavigationPage.Security or NavigationPage.AlertCenter or NavigationPage.Fleet;

    // v0.7.110.1: every page has category artwork and a real daytime variant.
    public Bitmap PageArtwork => ArtworkCatalog.Page(SelectedPage, IsLightMode);
    public Bitmap SetupArtwork => ArtworkCatalog.Page(NavigationPage.ServerSetup, IsLightMode);

    public ICommand ConnectCommand { get; }
    public ICommand DiscoverServicesCommand { get; }
    public ICommand RefreshMonitoringCommand { get; }
    public ICommand RefreshConsoleViewCommand { get; }
    public ICommand RconDoctorCommand { get; }
    public ICommand RconConnectCommand { get; }
    public ICommand RconDisconnectCommand { get; }
    public ICommand RconSendCommand { get; }
    public ICommand RefreshActivityCommand { get; }
    public ICommand RefreshNotificationsCommand { get; }
    public ICommand NotificationSelfTestCommand { get; }
    public ICommand MarkAllNotificationsReadCommand { get; }
    public ICommand ToggleNotificationReadCommand { get; }
    public ICommand ToggleNotificationPinCommand { get; }
    public ICommand DismissNotificationCommand { get; }
    public ICommand RefreshAutomationCommand { get; }
    public ICommand CreateAutomationRuleCommand { get; }
    public ICommand DeleteAutomationRuleCommand { get; }
    public ICommand ToggleAutomationRuleEnabledCommand { get; }
    public ICommand RunAutomationRuleNowCommand { get; }
    public ICommand RefreshSecurityCommand { get; }
    public ICommand CreatePrincipalCommand { get; }
    public ICommand RevokePrincipalCommand { get; }
    public ICommand RefreshAlertCenterCommand { get; }
    public ICommand SaveAlertRulesCommand { get; }
    public ICommand MuteAlertsCommand { get; }
    public ICommand PauseDeliveryCommand { get; }
    public ICommand SendTestNotificationCommand { get; }
    private NotificationDeliveryStateDto _notificationDelivery = new();
    public NotificationDeliveryStateDto NotificationDelivery { get => _notificationDelivery; private set => SetField(ref _notificationDelivery, value); }
    public ICommand RefreshDiscordBotConfigCommand { get; }
    public ICommand SaveDiscordBotConfigCommand { get; }
    public ICommand AddDiscordRoleMappingCommand { get; }
    public ICommand RemoveDiscordRoleMappingCommand { get; }
    public ICommand RefreshAntiCheatCommand { get; }
    public ICommand SaveAntiCheatRulesCommand { get; }
    public ICommand RefreshFleetCommand { get; }
    public ICommand CloneWorldCommand { get; }
    public ICommand BackupAllCommand { get; }
    public ICommand DoctorAllCommand { get; }
    public ICommand UpdateAllCommand { get; }
    public ICommand AnalyzeCrashesCommand { get; }
    public ICommand RefreshCrashHistoryCommand { get; }
    public ICommand RefreshPlayersCommand { get; }
    public ICommand SaveWorldNowCommand { get; }



    public ICommand GenerateServerNameCommand { get; }
    public ICommand GenerateSetupServerNameCommand { get; }
    public ICommand RunDoctorCommand { get; }
    public ICommand RecheckDiagnosticCommand { get; }
    public ICommand FixDiagnosticCommand { get; }
    public ICommand RefreshEnvironmentCommand { get; }
    public ICommand VerifyEnvironmentCommand { get; }
    public ICommand CreateDefaultServerSettingsCommand { get; }
    public ICommand EnvironmentActionCommand { get; }
    public ICommand RefreshDistributionCommand { get; }
    public ICommand PreviewDistributionPlanCommand { get; }
    public ICommand UpdatePalworldServerCommand { get; }
    public ICommand InstallPalworldServerFromWizardCommand { get; }
    public ICommand InstallPalworldServerWithExtrasCommand { get; }
    public ICommand RefreshWorldExplorerCommand { get; }
    public ICommand RefreshOperationsCommand { get; }
    public ICommand RefreshPlayerGuildExplorerCommand { get; }
    public ICommand RefreshPalsCommand { get; }
    public ICommand BeginStuckTestLoadCommand { get; }
    public ICommand ExportDoctorCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RestartCommand { get; }
    // v0.7.28.0: ForceStopServerAsync (MystTiqApiClient) already existed and was already reachable
    // from ShutdownForExitAsync(force: true) -- this is the first time it's exposed as a
    // user-facing command rather than only firing internally on app exit.
    public ICommand ForceStopServerCommand { get; }
    public ICommand InstallMissingEnvironmentCommand { get; }
    public ICommand RefreshAllInstancesCommand { get; }
    public ICommand TerminateSelectedInstanceCommand { get; }
    public ICommand SetAccentThemeCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand SetUpNewServerTabCommand { get; }
    public ICommand ConnectLocalServerTabCommand { get; }
    public ICommand ConnectRemoteServerTabCommand { get; }
    public ICommand ConnectExistingProfileTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand CloseActiveTabCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand ForgetSavedTokenCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand OpenNotificationLinkCommand { get; }
    private string? _focusFindingId;

    // v1.0.7.0 (roadmap Q-1, owner decision D-10): a saved list MystTiq could not read is alerted with a link to its
    // repair; the button opens Server Doctor with that finding first.
    private void OpenNotificationLink(NotificationItemDto? notification)
    {
        if (notification is not { HasLink: true, Link: { } link }) return;
        _focusFindingId = link[NotificationItemDto.DiagnosticsLinkPrefix.Length..];
        Navigate(nameof(NavigationPage.Doctor));
    }
    public ICommand ToggleWanDiagnosticsCommand { get; }
    public ICommand ToggleLocalDiagnosticsCommand { get; }
    public ICommand ToggleDiscordBotCommand { get; }
    public ICommand ToggleAntiCheatCommand { get; }

    public bool IsWanDiagnosticsExpanded { get => _isWanDiagnosticsExpanded; private set => SetField(ref _isWanDiagnosticsExpanded, value); }
    public bool IsLocalDiagnosticsExpanded { get => _isLocalDiagnosticsExpanded; private set => SetField(ref _isLocalDiagnosticsExpanded, value); }
    public bool IsDiscordBotExpanded { get => _isDiscordBotExpanded; private set => SetField(ref _isDiscordBotExpanded, value); }
    public bool IsAntiCheatExpanded { get => _isAntiCheatExpanded; private set => SetField(ref _isAntiCheatExpanded, value); }

    public string CrashAnalyzerState { get => _crashAnalyzerState; private set => SetField(ref _crashAnalyzerState, value); }



    public string EnvironmentHealthText { get => _environmentHealthText; private set => SetField(ref _environmentHealthText, value); }
    public string SetupOperationState { get => _setupOperationState; private set => SetField(ref _setupOperationState, value); }
    public string SetupOperationTitle { get => _setupOperationTitle; private set => SetField(ref _setupOperationTitle, value); }
    public string SetupOperationDetail { get => _setupOperationDetail; private set => SetField(ref _setupOperationDetail, value); }
    public string SetupRecentActivity { get => _setupRecentActivity; private set => SetField(ref _setupRecentActivity, value); }
    public double SetupOperationProgress { get => _setupOperationProgress; private set { if (SetField(ref _setupOperationProgress, value)) RaisePropertyChanged(nameof(SetupOperationProgressText)); } }
    public string SetupOperationProgressText => $"{SetupOperationProgress:0}%";
    // v0.7.82.0 bug fix: reported live -- the Confirm step's summary card ("Server: {ProfileName}")
    // showed the generic "New Server" placeholder even after the user had typed a real name here,
    // since ProfileName (this MystTiq connection's own label) and SetupServerName (the Palworld
    // server's in-game name) were always two entirely separate fields with nothing syncing them --
    // making it look like nothing had actually been configured, and the saved profile itself would
    // keep the generic placeholder name too. Only auto-follows while ProfileName is still exactly the
    // BeginNewProfile default (or blank); once the user edits the connection name directly at Step 1,
    // their choice is never overwritten.
    public string SetupServerName
    {
        get => _setupServerName;
        set
        {
            if (!SetField(ref _setupServerName, value ?? string.Empty)) return;
            if (IsNewServerSetupFlow && !string.IsNullOrWhiteSpace(_setupServerName) &&
                (string.IsNullOrWhiteSpace(ProfileName) || ProfileName == "New Server"))
                ProfileName = _setupServerName;
        }
    }
    public string SetupServerDescription { get => _setupServerDescription; set => SetField(ref _setupServerDescription, value ?? string.Empty); }
    public string SetupAdminPassword { get => _setupAdminPassword; set => SetField(ref _setupAdminPassword, value ?? string.Empty); }
    public string SetupServerPassword { get => _setupServerPassword; set => SetField(ref _setupServerPassword, value ?? string.Empty); }
    public string SetupMaximumPlayers { get => _setupMaximumPlayers; set => SetField(ref _setupMaximumPlayers, value ?? string.Empty); }
    public string SetupGamePort
    {
        get => _setupGamePort;
        set { if (SetField(ref _setupGamePort, value ?? string.Empty)) CheckSetupPortAsync(_setupGamePort, "UDP", v => SetupGamePortWarningText = v); }
    }
    public string SetupRestPort
    {
        get => _setupRestPort;
        set { if (SetField(ref _setupRestPort, value ?? string.Empty)) CheckSetupPortAsync(_setupRestPort, "TCP", v => SetupRestPortWarningText = v); }
    }
    public string SetupGamePortWarningText
    {
        get => _setupGamePortWarningText;
        private set { if (SetField(ref _setupGamePortWarningText, value)) RaisePropertyChanged(nameof(HasSetupGamePortWarning)); }
    }
    public string SetupRestPortWarningText
    {
        get => _setupRestPortWarningText;
        private set { if (SetField(ref _setupRestPortWarningText, value)) RaisePropertyChanged(nameof(HasSetupRestPortWarning)); }
    }
    public bool HasSetupGamePortWarning => SetupGamePortWarningText.Length > 0;
    public bool HasSetupRestPortWarning => SetupRestPortWarningText.Length > 0;

    // v0.7.2.0: not a hard block (a stale/zombie listener shouldn't be able to prevent recovery) --
    // a live, best-effort warning shown while the user is still typing a candidate port, so a
    // conflict is visible before they click Create rather than discovered only after the fact.
    private async void CheckSetupPortAsync(string portText, string protocol, Action<string> setWarning)
    {
        setWarning(string.Empty);
        if (SelectedProfile is null || !int.TryParse(portText, out var port) || port is < 1 or > 65535)
            return;
        try
        {
            var profile = BuildProfileFromEditor(SelectedProfile.Id);
            var result = await _api.CheckPortAsync(profile, port, protocol, BearerToken);
            if (result.InUse)
                setWarning(result.ProcessName is null
                    ? $"Port {port} ({protocol}) already appears to be in use."
                    : $"Port {port} ({protocol}) is already in use by {result.ProcessName} (PID {result.OwningProcessId?.ToString() ?? "unknown"}).");
        }
        catch { /* best-effort warning only -- a failed check must not block editing the form */ }
    }
    public bool SetupCreateConfirmed
    {
        get => _setupCreateConfirmed;
        set
        {
            if (!SetField(ref _setupCreateConfirmed, value)) return;
            (CreateDefaultServerSettingsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }
    public string SetupReadyCountText => $"{EnvironmentItems.Count(x => x.IsReady)} ready";
    public string SetupAttentionCountText => $"{EnvironmentItems.Count(x => !x.IsReady)} need attention";
    public string SetupComponentCountText => $"{EnvironmentItems.Count} components checked";




    public string NetworkHealth { get => _networkHealth; private set => SetField(ref _networkHealth, value); }
    public string LocalDiagnosticsState { get => _localDiagnosticsState; private set => SetField(ref _localDiagnosticsState, value); }
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
    public string WanPublicIpPort { get => _wanPublicIpPort; private set => SetField(ref _wanPublicIpPort, value); }
    public string WanUpnpState { get => _wanUpnpState; private set => SetField(ref _wanUpnpState, value); }
    public string WanRouterDescription { get => _wanRouterDescription; private set => SetField(ref _wanRouterDescription, value); }
    public string WanStatus { get => _wanStatus; private set => SetField(ref _wanStatus, value); }
    public ICommand RunNetworkDiagnosticsCommand { get; }
    public ICommand DiagnoseConnectionCommand { get; }
    public ICommand RepairFirewallCommand { get; }
    public ICommand RunWanReachabilityCommand { get; }
    public ICommand RepairUpnpMappingCommand { get; }
    public ICommand OpenExternalPortCheckerCommand { get; }
    public ICommand RestartFromDiagnosticsCommand { get; }
    public ICommand BootstrapLocalCommand { get; }



    public string BuildRedactedSupportReport()
    {
        var endpoint = "Not configured";
        if (Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri))
            endpoint = new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty, Query = string.Empty, Fragment = string.Empty }.Uri.ToString().TrimEnd('/');
        return string.Join(Environment.NewLine, new[]
        {
            "MystTiq Support Package", $"Generated: {DateTimeOffset.UtcNow:O}", $"Desktop: {Version}", $"Platform: {PlatformText}",
            $"Profile: {ProfileName}", $"Endpoint: {endpoint}", $"Connection: {ConnectionState}",
            $"Authentication supplied: {(!string.IsNullOrWhiteSpace(BearerToken) ? "yes (redacted)" : "no")}",
            $"TLS pin supplied: {(!string.IsNullOrWhiteSpace(CertificateSha256) ? "yes (redacted)" : "no")}", "",
            NetworkReportText, "", "Workspace", $"State: {Workspace.WorkspaceState}",
            $"Server root: {Configuration.ConfigServerRoot}", $"SteamCMD: {Configuration.ConfigSteamCmdPath}", $"Backup root: {Configuration.ConfigBackupRoot}", $"Runtime root: {Configuration.ConfigRuntimeRoot}"
        });
    }



    private async Task BootstrapLocalAsync()
    {
        if (!IsLocalProfile) return;
        IsBusy = true;
        try
        {
            var snapshot = await _localDiscovery.DiscoverAsync();
            var result = await _localBootstrapper.EnsureAvailableAsync(snapshot);
            ServerUrl = result.Endpoint;
            ConnectionState = result.Available ? "Local backend ready" : "Local backend unavailable";
            Detail = result.Detail;
            if (result.Available) await RefreshAsync(silent: true);
        }
        catch (Exception ex) { ConnectionState = "Local bootstrap failed"; Detail = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RunNetworkDiagnosticsAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var report = await _api.GetNetworkDiagnosticsAsync(SelectedProfile, BearerToken);
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
        finally { IsBusy = false; }
        await RefreshFirewallStatusAsync();
    }

    // v0.6.4.0 local-PC diagnostics: runs entirely client-side against SelectedProfile, unlike
    // every other diagnostic command on this page which asks the server about itself. This is what
    // replaces RefreshAsync's raw, uninterpreted exception message with a real staged DNS/TCP/TLS/
    // HTTP diagnosis when a connection profile can't be reached.
    private async Task DiagnoseConnectionAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        LocalDiagnosticsState = "Diagnosing…";
        try
        {
            var findings = await _localDiagnostics.DiagnoseConnectionAsync(SelectedProfile, CancellationToken.None);
            LocalConnectionFindings.Clear();
            foreach (var finding in findings) LocalConnectionFindings.Add(finding);
            var failed = findings.FirstOrDefault(f => f.State == (int)MystTiq.Core.Models.DiagnosticState.Fail);
            LocalDiagnosticsState = failed is not null
                ? $"Failed at {failed.Component}: {failed.Evidence}"
                : "All stages passed.";
        }
        catch (Exception ex) { LocalDiagnosticsState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RestartFromDiagnosticsAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var result = await _api.RestartFromNetworkDiagnosticsAsync(SelectedProfile, BearerToken);
            NetworkRecommendation = result.Success
                ? "Controlled restart completed; network diagnostics were re-run automatically."
                : result.Restart.Message ?? "Controlled restart did not complete successfully.";
            await RunNetworkDiagnosticsAsync();
        }
        catch (Exception ex) { NetworkRecommendation = ex.Message; }
        finally { IsBusy = false; }
    }

    // v0.9.6.0: allows this server's game port (the one it binds) and removes its rules for a port it no longer uses.
    // When Windows refuses the service (not an administrator) and the server is on this computer, the same change runs
    // through Windows' administrator prompt instead.
    private async Task RepairFirewallAsync()
    {
        if (FirewallProfile() is not { } profile) return;
        IsBusy = true;
        try
        {
            var result = await _api.RepairNetworkFirewallAsync(profile, BearerToken);
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
        finally { IsBusy = false; }
    }

    // The wizard has no saved profile yet; it talks to the server through the one its editor builds.
    private ConnectionProfile? FirewallProfile()
    {
        if (SelectedProfile is not null) return SelectedProfile;
        if (!IsCreatingNewProfile) return null;
        try { return BuildProfileFromEditor(null); }
        catch { return null; }
    }

    private async Task RefreshFirewallStatusAsync()
    {
        if (FirewallProfile() is not { } profile) return;
        try
        {
            var status = await _api.GetNetworkFirewallAsync(profile, BearerToken);
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

    // v0.6.11.0: the genuine gap left after local firewall inspection (above) and v0.6.4.0's Local
    // Machine Diagnostics -- neither tests whether the internet, or even the router, can actually
    // reach the configured game port. Public IP + UPnP mapping are real, automated checks; true
    // unsolicited-inbound UDP confirmation needs infrastructure we don't own, so that piece is a
    // manual hand-off (Copy IP:Port / Open Port Checker) rather than a faked pass/fail.
    private async Task RunWanReachabilityAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var report = await _api.GetWanReachabilityAsync(SelectedProfile, BearerToken);
            WanPublicIpPort = report.PublicIpPortText;
            WanUpnpState = report.UpnpStateText;
            WanRouterDescription = (report.RouterDescription ?? "—") + (string.IsNullOrWhiteSpace(report.RouterWanIPv4) ? string.Empty : $" · internet address {report.RouterWanIPv4}");
            WanReachabilityChecks.Clear();
            foreach (var check in report.Checks) WanReachabilityChecks.Add(check);
            WanStatus = "Checked " + report.CheckedAt.ToLocalTime().ToString("t");
        }
        catch (Exception ex) { WanStatus = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RepairUpnpMappingAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var result = await _api.RepairUpnpMappingAsync(SelectedProfile, BearerToken);
            WanStatus = result.Message;
            await RunWanReachabilityAsync();
        }
        catch (Exception ex) { WanStatus = ex.Message; }
        finally { IsBusy = false; }
    }

    private void OpenExternalPortChecker()
    {
        try
        {
            var start = new ProcessStartInfo("https://canyouseeme.org/") { UseShellExecute = true };
            Process.Start(start);
        }
        catch (Exception ex) { WanStatus = "Unable to open the port checker: " + ex.Message; }
    }

    // v0.7.87.0: direct live feedback -- leaving Configuration with unsaved changes previously just
    // discarded them silently. Navigate() is the single funnel every nav button/link already goes
    // through (confirmed: dozens of XAML call sites), so the guard lives here rather than at each
    // call site. The ViewModel has no Window reference for the confirm dialog (same constraint as
    // the Close Tab/Delete Player flows), so it raises an event for MainWindow's code-behind to show
    // the dialog and call back into one of the three continuation methods below.
    private NavigationPage? _pendingNavigationAfterConfigPrompt;
    public event Action? ConfigurationUnsavedChangesNavigationBlocked;

    private void Navigate(string? pageName)
    {
        if (!Enum.TryParse<NavigationPage>(pageName, ignoreCase: true, out var page))
            return;

        if (SelectedPage == NavigationPage.Configuration && page != NavigationPage.Configuration && Configuration.PalworldConfigIsDirty)
        {
            _pendingNavigationAfterConfigPrompt = page;
            ConfigurationUnsavedChangesNavigationBlocked?.Invoke();
            return;
        }

        PerformNavigation(page);
    }

    private void PerformNavigation(NavigationPage page)
    {
        CancelPageWait();
        SelectedPage = page;
        RunUi(() => RefreshPageForNavigationAsync(page));
    }

    public async Task ContinuePendingNavigationSavingConfigurationAsync()
    {
        var target = _pendingNavigationAfterConfigPrompt;
        _pendingNavigationAfterConfigPrompt = null;
        if (target is null) return;
        await Configuration.SavePalworldConfigurationAsync();
        PerformNavigation(target.Value);
    }

    public void ContinuePendingNavigationDiscardingConfigurationChanges()
    {
        var target = _pendingNavigationAfterConfigPrompt;
        _pendingNavigationAfterConfigPrompt = null;
        if (target is null) return;
        Configuration.ResetPalworldConfigurationChanges();
        PerformNavigation(target.Value);
    }

    public void CancelPendingConfigurationNavigation()
    {
        _pendingNavigationAfterConfigPrompt = null;
    }

    private async Task RefreshPageForNavigationAsync(NavigationPage page)
    {
        if (IsBusy)
        {
            // v0.7.100.0: opening a page while the app was still busy (start-up work, another refresh)
            // used to skip its load silently, so the Map and Players pages stayed empty until Refresh
            // was pressed. These two show live data, so they wait for the busy state to clear (up to a
            // minute) and load if the user is still on the page. Other pages keep the old behaviour.
            if (page is not (NavigationPage.Map or NavigationPage.Players)) return;
            // v1.0.7.2 (roadmap Q-13): a signal and a message instead of polling; leaving the page ends the wait.
            if (!await WaitUntilNotBusyAsync(page)) return;
        }
        switch (page)
        {
            case NavigationPage.Dashboard:
                await RefreshAsync(silent: false);
                break;
            case NavigationPage.ServerSetup:
                await RefreshEnvironmentAsync();
                if (!IsBusy) await RefreshDistributionAsync();
                break;
            case NavigationPage.UpdateCenter:
                await RefreshDistributionAsync();
                break;
            case NavigationPage.Configuration:
                // v0.7.87.0: direct live feedback -- Configuration should always start on Server
                // Settings, not silently remember Advanced from a previous visit this session.
                Configuration.SetConfigurationView(true);
                await Configuration.LoadConfigurationAsync();
                if (!IsBusy) await Configuration.LoadPalworldConfigurationAsync();
                break;
            case NavigationPage.Workspace:
                await Configuration.LoadConfigurationAsync();
                break;
            case NavigationPage.Launcher:
                await Configuration.LoadConfigurationAsync();
                break;
            case NavigationPage.Backups:
                await Backups.RefreshBackupsAsync();
                break;
            case NavigationPage.Console:
                await RefreshMonitoringAsync();
                break;
            case NavigationPage.Players:
                await RefreshPlayersPageAsync();
                break;
            case NavigationPage.ActivityAudit:
                await RefreshActivityAsync();
                break;
            case NavigationPage.Notifications:
                await RefreshNotificationsAsync();
                break;
            case NavigationPage.Automation:
                await RefreshAutomationAsync();
                break;
            case NavigationPage.Security:
                await RefreshSecurityAsync();
                break;
            case NavigationPage.AlertCenter:
                await RefreshAlertCenterAsync();
                await RefreshDiscordBotConfigAsync();
                await RefreshAntiCheatAsync();
                break;
            case NavigationPage.Fleet:
                await RefreshFleetAsync();
                break;
            case NavigationPage.Host:
                await Host.RefreshHostAsync(silent: false);
                break;
            case NavigationPage.Inspector:
                await RefreshWorldExplorerAsync();
                break;
            case NavigationPage.WorldTransactions:
                await World.RefreshWorldTransactionsAsync();
                break;
            case NavigationPage.Bases:
            case NavigationPage.Guilds:
                await RefreshPlayerGuildExplorerAsync();
                break;
            case NavigationPage.Map:
                // The Map page reads the same live players and base locations as the Players page.
                await RefreshPlayersPageAsync();
                if (!IsBusy) await Map.RefreshTeleportAsync();
                break;
            case NavigationPage.ModDashboard:
            case NavigationPage.ModLibrary:
            case NavigationPage.Ue4ss:
                await Mods.RefreshModsAsync();
                break;
            case NavigationPage.Doctor:
                await RunDoctorAsync();
                break;
            case NavigationPage.DiagnosticsCenter:
                await RunNetworkDiagnosticsAsync();
                break;
            case NavigationPage.Settings:
                // v0.9.6.0: the firewall state for the port the launch arguments name.
                if (SelectedProfile is not null) await RefreshFirewallStatusAsync();
                break;
            case NavigationPage.CrashAnalyzer:
                await RefreshCrashHistoryAsync();
                break;
            case NavigationPage.SaveTools:
                await Backups.RefreshSaveToolsAsync(false);
                break;
        }
    }

    private void SaveProfile()
    {
        try
        {
            var profile = BuildProfileFromEditor(SelectedProfile?.Id);

            var existing = Profiles.FirstOrDefault(p => p.Id == profile.Id);
            if (existing is not null)
            {
                var index = Profiles.IndexOf(existing);
                Profiles[index] = profile;
            }
            else
            {
                Profiles.Add(profile);
            }

            _profileStore.Save(Profiles);
            SelectedProfile = profile;

            // v0.7.6.0: any OTHER open tab already pointed at this same profile Id previously kept
            // its own stale copy (old name/URL/cert pin) forever -- a rename or address change here
            // silently would not reach it. Only the profile reference/display fields are refreshed;
            // an already-connected background tab's live BearerToken/ConnectionState are left alone
            // rather than force-disconnecting it just because its profile was edited elsewhere.
            foreach (var tab in Tabs)
            {
                if (ReferenceEquals(tab, ActiveTab) || tab.Profile?.Id != profile.Id) continue;
                tab.Profile = profile;
                tab.ProfileName = profile.Name;
                tab.ServerUrl = profile.BaseAddress.ToString().TrimEnd('/');
                tab.CertificateSha256 = profile.ServerCertificateSha256 ?? string.Empty;
                tab.TargetServerId = profile.ServerId ?? string.Empty;
            }

            SaveTabSession();
            Detail = $"Connection profile '{profile.Name}' saved.";
        }
        catch (Exception ex)
        {
            Detail = ex.Message;
        }
    }

    private void BeginNewProfile()
    {
        SelectedProfile = null;
        ProfileName = "New Server";
        ServerUrl = "http://127.0.0.1:8213";
        BearerToken = string.Empty;
        CertificateSha256 = string.Empty;
        TargetServerId = string.Empty;
        ManagementApiConnected = false;
        ConnectionState = "Not connected";
        Detail = "Choose whether this server runs on this machine or elsewhere.";
        ConnectionKind = string.Empty;
        NewServerWizardStep = 0;
        RaisePropertyChanged(nameof(IsLocalProfile));
        RaisePropertyChanged(nameof(BrowserViewUrlVerbatim));
        (DeleteProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    // v0.7.13.0: Step 0 -- picking Local or Remote pre-fills the draft ServerUrl appropriately and
    // advances straight to Connection Details (step 1), which then shows the matching sub-view.
    private void ChooseLocalConnection()
    {
        ConnectionKind = "Local";
        if (string.IsNullOrWhiteSpace(ServerUrl) || ServerUrl == "http://127.0.0.1:8213")
            ServerUrl = "http://127.0.0.1:8213";
        AdvanceWizardStep();
    }
    private void ChooseRemoteConnection()
    {
        ConnectionKind = "Remote";
        if (ServerUrl == "http://127.0.0.1:8213")
            ServerUrl = string.Empty;
        AdvanceWizardStep();
    }

    // v0.7.13.0: the Local branch's "just find it for me" action. Unlike BootstrapLocalCommand
    // (which requires an already-saved SelectedProfile == LocalDefault), this works during profile
    // creation -- SelectedProfile is still null at this point -- by writing straight into the draft
    // ServerUrl/Detail fields and then attempting a normal connect, exactly the way BootstrapLocalAsync
    // does for the one already-saved local profile.
    private async Task DetectLocalServiceAsync()
    {
        IsBusy = true;
        Detail = "Looking for a MystTiq service on this machine…";
        try
        {
            var snapshot = await _localDiscovery.DiscoverAsync();
            var result = await _localBootstrapper.EnsureAvailableAsync(snapshot);
            ServerUrl = result.Endpoint;
            ConnectionState = result.Available ? "Local backend ready" : "Local backend unavailable";
            Detail = result.Detail;
            if (result.Available) await RefreshAsync(silent: true);
        }
        catch (Exception ex) { ConnectionState = "Local detection failed"; Detail = ex.Message; }
        finally { IsBusy = false; }
    }

    // v0.7.81.0: step machine now branches by flow/world-source instead of a blind +1/-1 -- see the
    // IsWizardStep* properties' own comments for the full step map. IsCreatingNewProfile already
    // gates the whole flow, so once a profile is saved (or the wizard is closed by switching to an
    // existing profile) these become no-ops rather than needing their own guard.
    private void AdvanceWizardStep()
    {
        NewServerWizardStep = NewServerWizardStep switch
        {
            0 => 1,
            1 => IsNewServerSetupFlow ? 2 : 4,
            2 => 3,
            // v0.7.85.0: World Settings choice (4) doesn't apply to Import -- the placeholder world
            // it would apply a preset/customize to is about to be entirely replaced by the imported
            // archive, so Import skips straight from Identity & Ports to Install Directory.
            3 => IsWorldSourceImport ? 5 : 4,
            4 => 5,
            5 => IsNewServerSetupFlow ? 6 : 5,
            // v0.7.86.0: MODs (7) inserted before Confirm (now 8, was 7 through v0.7.85.0).
            6 => 7,
            7 => 8,
            _ => NewServerWizardStep
        };
        // v0.9.6.0: the last step offers to allow the new server's game port through the firewall.
        if (IsWizardStepConfirm && IsNewServerSetupFlow) _ = RefreshFirewallStatusAsync();
    }
    private void GoBackWizardStep()
    {
        NewServerWizardStep = NewServerWizardStep switch
        {
            8 => IsWorldSourceClone ? 3 : 7,
            7 => 6,
            6 => 5,
            5 => IsWorldSourceImport ? 3 : 4,
            4 => IsNewServerSetupFlow ? 3 : 1,
            3 => 2,
            2 => 1,
            // Step 1 (and the now-unreachable step 0) have no earlier step to return to -- the
            // wizard's own Cancel button at step 1 closes the tab instead of going "back".
            _ => NewServerWizardStep
        };
    }

    // v0.7.81.0: sets the World Source choice and advances past it in one step, used by the three
    // choice cards (Clone/New World/Import) at IsWizardStepWorldSource.
    private void ChooseNewServerWorldSource(string source)
    {
        NewServerWorldSource = source;
        if (source == "Import")
        {
            // v0.7.85.0: reset stale state from a prior World Transactions page visit -- these are
            // the same shared properties, not wizard-scoped, so a leftover "player-recovery" mode or
            // already-set preview token from earlier in the session must not leak into a fresh
            // install's own import flow.
            NewServerImportReady = false;
            World.WorldTransactionMode = "world-import";
            World.WorldTransactionConfirmed = false;
            World.WorldPreviewToken = string.Empty;
            World.WorldTransactionPlanSteps.Clear();
            World.WorldTransactionState = string.Empty;
        }
        AdvanceWizardStep();
    }

    // v0.7.82.0: World Settings choice step -- see NewServerWorldSettingsChoice's own comment for
    // why "Customize" can't show real values yet. Both re-evaluate the Install Directory step's
    // occupancy probe on the way in, since SetupServerName (used to derive a sibling folder name if
    // needed) may have just been edited at the previous step.
    private async Task ChooseWorldSettingsPresetAsync()
    {
        NewServerWorldSettingsChoice = "Preset";
        AdvanceWizardStep();
        await EvaluateNewServerInstallDirectoryAsync();
    }
    private async Task ChooseWorldSettingsCustomizeAsync()
    {
        NewServerWorldSettingsChoice = "Customize";
        AdvanceWizardStep();
        await EvaluateNewServerInstallDirectoryAsync();
    }

    private async Task EvaluateNewServerInstallDirectoryAsync()
    {
        IsBusy = true;
        NewServerInstallDirectoryStatusText = "Checking the configured server directory…";
        try
        {
            await Configuration.LoadConfigurationAsync();
            var root = Configuration.ConfigServerRoot;
            NewServerInstallDirectoryOccupied = !string.IsNullOrWhiteSpace(root) && Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any();
            NewServerInstallDirectoryEffectivePath = NewServerInstallDirectoryOccupied
                ? DeriveAvailableSiblingServerRoot(root, string.IsNullOrWhiteSpace(SetupServerName) ? "server" : SetupServerName)
                : root;
            NewServerInstallDirectoryStatusText = NewServerInstallDirectoryOccupied
                ? $"{root} already has a server installed. Continuing will create a second, separate server at: {NewServerInstallDirectoryEffectivePath}"
                : $"Files will install to: {root}";
        }
        catch (Exception ex) { NewServerInstallDirectoryStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    private static string SlugifyServerName(string name)
    {
        var slug = new string(name.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "server" : slug;
    }

    // v0.7.82.0: mirrors HeadlessWorldCloneService.CloneAsync's own sibling-directory pattern
    // (<parent>\<name>-clone-<id>) -- here <parent>\<slug>, collision-suffixed -- since AddServerAsync
    // itself does no path derivation (confirmed: it only validates/persists whatever ServerRoot the
    // caller supplies).
    private static string DeriveAvailableSiblingServerRoot(string existingServerRoot, string desiredName)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(existingServerRoot)) ?? existingServerRoot;
        var slug = SlugifyServerName(desiredName);
        var candidate = Path.Combine(parent, slug);
        var suffix = 2;
        while (Directory.Exists(candidate) && Directory.EnumerateFileSystemEntries(candidate).Any())
        {
            candidate = Path.Combine(parent, $"{slug}-{suffix}");
            suffix++;
        }
        return candidate;
    }

    // v0.7.82.0: ALWAYS registers a genuinely new fleet profile here, whether or not the default
    // directory happens to be occupied -- "Local MystTiq" (the always-present built-in default
    // profile) permanently owns the default ServerId, so a brand-new server can never legitimately
    // reuse it (confirmed live: BuildProfileFromEditor's own duplicate-connection guard would reject
    // saving it at Confirm). NewServerInstallDirectoryEffectivePath already resolved to the right
    // path for either case (typical root if free, a derived sibling if occupied, see
    // EvaluateNewServerInstallDirectoryAsync) -- only the ServerRoot varies, registration always
    // happens. Registers via POST /api/v1/servers (no file copying -- AddServerAsync itself does
    // none), restarts this Desktop instance's own owned sidecar so the new profile actually comes
    // online (confirmed pre-existing constraint: a newly added profile is invisible to the running
    // process until it restarts), then reconnects this wizard's in-progress connection to the new
    // profile's scoped routes before advancing. Only ever offered for local desktop-sidecar
    // connections (this flow is always local, see OpenNewServerTab) and declines outright if a real
    // Windows Service install is detected, since the Desktop app has no code path to restart that
    // (confirmed: IWindowsServiceManager is never referenced from MystTiq.Desktop).
    private async Task ContinueFromInstallDirectoryAsync()
    {
        IsBusy = true;
        NewServerInstallDirectoryStatusText = "Registering a new server profile…";
        try
        {
            var snapshot = await _localDiscovery.DiscoverAsync();
            if (snapshot.ServiceInstalled)
            {
                NewServerInstallDirectoryStatusText = "This machine runs MystTiq as an installed Windows Service, which the desktop app can't restart on its own. Add a second server profile from Fleet instead, restart the service manually, then connect to it.";
                return;
            }

            ConnectionProfile profile;
            try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
            catch (Exception ex) { NewServerInstallDirectoryStatusText = ex.Message; return; }

            var existingProfiles = await _api.GetServerProfilesAsync(profile, BearerToken);
            var defaultProfile = existingProfiles.FirstOrDefault(p => p.Id == "default");
            var runtime = defaultProfile?.Runtime is { Length: > 0 } r ? r : "WindowsNative";

            var slug = SlugifyServerName(string.IsNullOrWhiteSpace(SetupServerName) ? "server" : SetupServerName);
            var candidateId = slug;
            var suffix = 2;
            while (existingProfiles.Any(p => p.Id.Equals(candidateId, StringComparison.OrdinalIgnoreCase)))
            {
                candidateId = $"{slug}-{suffix}";
                suffix++;
            }

            var newServerRoot = NewServerInstallDirectoryEffectivePath;
            var request = new AddFleetProfileRequestDto
            {
                Id = candidateId,
                Name = string.IsNullOrWhiteSpace(SetupServerName) ? candidateId : SetupServerName.Trim(),
                ServerRoot = newServerRoot,
                SteamCmdPath = Configuration.ConfigSteamCmdPath,
                BackupRoot = Path.Combine(newServerRoot, "Backups"),
                RuntimeRoot = Path.Combine(Path.GetDirectoryName(Configuration.ConfigRuntimeRoot) ?? Configuration.ConfigRuntimeRoot, candidateId),
                LaunchArguments = LaunchArgumentsText.WithPort(Configuration.ConfigLaunchArguments, SetupGamePort),
                Runtime = runtime
            };

            var addResult = await _api.AddFleetProfileAsync(profile, request, BearerToken);
            if (!addResult.Success)
            {
                NewServerInstallDirectoryStatusText = addResult.Message;
                return;
            }

            NewServerInstallDirectoryStatusText = "Profile registered. Restarting the local MystTiq service to bring it online…";
            var restartResult = await _localBootstrapper.RestartOwnedSidecarAsync(snapshot, candidateId);
            if (!restartResult.Available)
            {
                NewServerInstallDirectoryStatusText = $"Profile registered, but the automatic restart failed: {restartResult.Detail}";
                return;
            }

            ServerUrl = restartResult.Endpoint;
            TargetServerId = candidateId;
            await RefreshAsync();
            if (!ManagementApiConnected)
            {
                NewServerInstallDirectoryStatusText = "Profile registered and restarted, but reconnecting to it failed. Try again from this step.";
                return;
            }

            NewServerInstallDirectoryStatusText = $"Connected to the new profile '{candidateId}'.";
            AdvanceWizardStep();
            _ = RefreshEnvironmentAsync();
        }
        catch (Exception ex) { NewServerInstallDirectoryStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    // v0.7.82.0: replaces the plain WizardAdvanceCommand on the Install step's own "Next" button --
    // advances to Confirm (7), then automatically applies the Identity & Ports values collected back
    // at step 3 (CreateDefaultServerSettingsAsync, reused as-is) and, if a preset was chosen at step
    // 4, applies it too (Configuration.ApplyStarterPresetAsync, reused as-is). Both already require the
    // just-installed PalWorldSettings.ini to exist, which is exactly why this runs after Install
    // rather than before.
    private async Task FinishInstallAndAdvanceAsync()
    {
        AdvanceWizardStep();
        if (!IsNewServerSetupFlow || IsWorldSourceClone) return;

        SetupCreateConfirmed = true;
        await CreateDefaultServerSettingsAsync();
        if (IsWorldSettingsChoicePreset && Configuration.SelectedConfigPreset is not null)
            await Configuration.ApplyStarterPresetAsync();

        // v0.7.89.0: direct live feedback -- "step 7 should automatically search for workshop
        // mods," instead of requiring a manual "Scan Workshop Mods" click on arrival. Mirrors the
        // MOD Library page's own v0.7.78.0 auto-scan-on-arrival behavior verbatim (same
        // Mods.WorkshopItems.Count == 0 guard, so it only fires once per tab).
        if (IsWizardStepMods && Mods.WorkshopItems.Count == 0)
            _ = Mods.ScanWorkshopModsAsync();
    }

    // v0.7.85.0: "Import a World" Phase 2 -- starts the freshly-installed server once so PalServer.exe
    // generates its own initial world (empirically confirmed: a real SaveGames/0/<32-hex-id>/Level.sav
    // appears within a few minutes of first start), then stops it so that placeholder world can be
    // safely replaced via the already-existing, already-proven World Transactions "world-import"
    // Analyze/Apply flow (embedded directly in this same wizard step once ready). World save content
    // is independent of which ports the server happened to be running under, so this has no ordering
    // dependency on Identity & Ports/CreateDefaultServerSettingsAsync -- either can happen first.
    private async Task PrepareForWorldImportAsync()
    {
        IsBusy = true;
        NewServerImportReady = false;
        NewServerImportPrepareStatusText = "Starting the server once to generate its initial world…";
        try
        {
            await StartServerAsync();

            ConnectionProfile profile;
            try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
            catch (Exception ex) { NewServerImportPrepareStatusText = ex.Message; return; }

            var deadline = DateTimeOffset.UtcNow.AddMinutes(5);
            var available = false;
            while (DateTimeOffset.UtcNow < deadline)
            {
                try
                {
                    var snapshot = await _api.GetWorldExplorerAsync(profile, BearerToken);
                    if (snapshot.Available) { available = true; break; }
                }
                catch { /* server may still be mid-startup; keep polling until the deadline */ }
                NewServerImportPrepareStatusText = "Waiting for the initial world to finish generating…";
                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            NewServerImportPrepareStatusText = "Stopping the server so its world can be safely replaced…";
            await StopServerAsync();

            NewServerImportReady = available;
            NewServerImportPrepareStatusText = available
                ? "Ready. Choose your world archive below, Analyze it, then Apply to replace this placeholder world."
                : "The server didn't generate its initial world within 5 minutes. Try again, or check Doctor for issues.";
        }
        catch (Exception ex) { NewServerImportPrepareStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    // Opens a new tab and puts it in the same "creating a new connection" state BeginNewProfile()
    // has always put the (single, shared) connection in -- now scoped to whichever tab was just
    // opened instead of replacing the app's one-and-only connection.
    // v0.7.81.0: direct live feedback ("it should not be trying to connect to a previously installed
    // version as we already have an option to connect to a local server or remote server from the
    // plus... It should always be a local install"). Previously landed on the same Local/Remote
    // choice + "find or start a service" screens as Connect to Local/Remote Server, with no real
    // distinction beyond framing text (see the git-history comment this replaced, and the
    // architecture doc for the full before/after). Now skips straight to the local connect step,
    // same as OpenConnectLocalServerTab below, and sets IsNewServerSetupFlow so the wizard shows the
    // new World Source/Clone/Install steps afterward instead of jumping straight to Settings.
    private void OpenNewServerTab()
    {
        ActiveTab = CreateTab();
        BeginNewProfile();
        IsNewServerSetupFlow = true;
        ChooseLocalConnection();
        Detail = "Setting up a new local Palworld server installation.";
        // v0.7.82.0: direct live feedback ("this should not be the first screen -- I am setting up a
        // new one, not connecting to an existing service"). Step 1 is really just "make sure
        // MystTiq's own local management engine is reachable" (it's the engine that will run the new
        // server, not an existing Palworld server to find), so auto-run the exact same detection the
        // "Auto-Detect Local Service" button does instead of making the user click it manually. See
        // ApplyStatus's own comment for the matching auto-advance once this connects.
        _ = DetectLocalServiceAsync();
    }

    // v0.7.52.0 "+" Flow Restructure (item 53): open a new tab and jump straight past the (now
    // fully removed) Local/Remote choice step, reusing ChooseLocalConnection/ChooseRemoteConnection
    // unchanged. IsNewServerSetupFlow stays false (TabSession's own default) so these two still land
    // directly on Settings/Confirm after connecting, exactly as before v0.7.81.0.
    private void OpenConnectLocalServerTab()
    {
        ActiveTab = CreateTab();
        BeginNewProfile();
        ChooseLocalConnection();
        Detail = "Connecting to an already-running local server.";
    }

    private void OpenConnectRemoteServerTab()
    {
        ActiveTab = CreateTab();
        BeginNewProfile();
        ChooseRemoteConnection();
        Detail = "Connecting to a remote MystTiq server.";
    }

    // The "+" button's "Connect to Existing Server" entries call this. The duplicate-connection
    // guard lives here, at the single place a tab can be opened, rather than scattered across
    // the codebase: a profile already open in another tab is filtered out of that list before this
    // is ever invoked (see MainWindow.axaml.cs's flyout builder), and this is a second, defensive
    // check against the same race.
    private void ConnectExistingProfileTab(ConnectionProfile? profile)
    {
        if (profile is null || Tabs.Any(t => t.Profile?.Id == profile.Id))
            return;

        ActiveTab = CreateTab();
        SelectedProfile = profile;
        SaveTabSession();
        RunUi(() => RefreshAsync(silent: false));
    }

    // v0.7.72.0: which profiles are currently open as tabs, so they can reopen automatically next
    // launch (see TabSessionStore and RestoreTabSessionAsync). Called at every point a tab's set of
    // real (non-null-Profile) entries changes: opening a saved profile, closing a tab, and finishing
    // SaveProfileCommand (which covers both the Settings-page edit path and the "Set Up New Server"
    // wizard's last step, either of which can give the active tab a real Profile for the first time).
    private void SaveTabSession() =>
        _tabSessionStore.Save(Tabs.Select(t => t.Profile?.Id).Where(id => !string.IsNullOrWhiteSpace(id))!);

    private void CloseTab(TabSession? tab)
    {
        if (tab is null || Tabs.Count <= 1)
            return;

        tab.Timer?.Stop();
        tab.Timer = null;
        var wasActive = ReferenceEquals(tab, ActiveTab);
        Tabs.Remove(tab);
        if (wasActive)
            ActiveTab = Tabs.FirstOrDefault();
        (CloseTabCommand as RelayCommand<TabSession>)?.RaiseCanExecuteChanged();
        (CloseActiveTabCommand as RelayCommand)?.RaiseCanExecuteChanged();
        RecomputeDuplicateInstallWarnings();
        SaveTabSession();
    }

    // v0.7.31.0: identity is the install location, not host:port -- two tabs can legitimately share
    // a port (different remote hosts), and a ConnectionProfile alone can't tell two installs apart
    // before connecting. Recomputed from scratch on every call (not incrementally) so a resolved
    // duplicate (a tab closed, or a later poll found the roots actually differ) clears correctly
    // without needing to track which specific pair caused the warning.
    private void RecomputeDuplicateInstallWarnings()
    {
        var rootsInUse = Tabs
            .Where(t => !string.IsNullOrWhiteSpace(t.ServerRoot))
            .GroupBy(t => t.ServerRoot, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in Tabs)
        {
            tab.DuplicateInstallWarning = !string.IsNullOrWhiteSpace(tab.ServerRoot) && rootsInUse.Contains(tab.ServerRoot)
                ? $"Another open tab is already connected to this same install ({tab.ServerRoot}) — you may be looking at the same server twice."
                : string.Empty;
        }
    }

    private TabSession CreateTab()
    {
        var tab = new TabSession();
        Tabs.Add(tab);
        WireTabTimer(tab);
        (CloseTabCommand as RelayCommand<TabSession>)?.RaiseCanExecuteChanged();
        (CloseActiveTabCommand as RelayCommand)?.RaiseCanExecuteChanged();
        return tab;
    }

    // v0.7.16.0: called from MainWindow's code-behind whenever the tab strip's own host panel
    // resizes (window resize, or the fixed-width columns either side of it changing). Previously
    // the tab ListBox had a hardcoded MaxWidth="720" regardless of how much window width was
    // actually available, and tabs past whatever fit were silently clipped with no way to reach
    // them -- no scrolling, no indicator, nothing.
    // v0.7.82.0: direct live feedback ("the server environment should expand or shrink to fill the
    // screen") -- was a fixed MaxHeight="405" regardless of actual window size, so a taller window
    // left the table artificially short (wasted space below) and a shorter one could overflow.
    // Reactively recomputed on every window resize (Window_OnSizeChanged, the same trigger
    // UpdateMaximizeGlyph already uses), same established pattern as UpdateTabStripWidth/
    // UpdateRibbonWidth above for "this needs to track live window size" in this codebase. The
    // subtracted offset approximates every other fixed-height chrome element above and below the
    // table on the Server Setup page (title bar, category tabs, ribbon, page header, summary cards
    // row, section label, table's own header row, Operation Monitor card, margins) -- clamped to a
    // sane minimum/maximum rather than tuned to an exact pixel count, since that chrome height can
    // itself vary slightly by theme/DPI.
    public double ServerSetupTableMaxHeight { get => _serverSetupTableMaxHeight; private set => SetField(ref _serverSetupTableMaxHeight, value); }

    public void UpdateServerSetupTableHeight(double windowHeight)
    {
        const double nonTableChromeHeight = 560;
        const double minTableHeight = 180;
        const double maxTableHeight = 900;
        ServerSetupTableMaxHeight = Math.Clamp(windowHeight - nonTableChromeHeight, minTableHeight, maxTableHeight);
    }

    // v0.9.8.0: below 1200 px the title bar drops the "Palworld Server Manager" subtitle and its fixed 430 px brand width, so
    // the tab strip keeps room for a tab and the "+".
    private bool _showBrandSubtitle = true;
    public bool ShowBrandSubtitle { get => _showBrandSubtitle; private set { if (SetField(ref _showBrandSubtitle, value)) RaisePropertyChanged(nameof(BrandMinWidth)); } }
    public double BrandMinWidth => ShowBrandSubtitle ? 430 : 0;
    // The tab list's cap: the strip less the "+" and, when shown, the "»", so both always fit after the tabs.
    private double _tabListMaxWidth = double.PositiveInfinity;
    public double TabListMaxWidth { get => _tabListMaxWidth; private set => SetField(ref _tabListMaxWidth, value); }
    public void UpdateWindowWidth(double width) { if (width > 0) ShowBrandSubtitle = width >= 1200; }

    public void UpdateTabStripWidth(double width)
    {
        if (width <= 0 || Math.Abs(width - _tabStripWidth) < 1) return;
        _tabStripWidth = width;
        RecomputeTabLayout();
    }

    private void RecomputeTabLayout()
    {
        const double perTabWidth = 204; // Border MinWidth 200 + Margin 2+2
        const double addButtonWidth = 52; // Width 44 + Margin 4+4
        const double overflowButtonWidth = 40; // Width 36 + Margin 2+2

        var available = Math.Max(0, _tabStripWidth - addButtonWidth);
        var maxVisible = Tabs.Count == 0 ? 0 : (int)Math.Floor(available / perTabWidth);
        if (maxVisible >= Tabs.Count)
        {
            SyncTabCollections(Tabs, []);
            return;
        }

        // Doesn't fit without the overflow button itself claiming some of that space too. At least one tab stays: the list must
        // hold the active tab (its selection would otherwise clear it), and since v0.9.8.0 the "+" and "»" have their own
        // columns, so a tab wider than the space left is clipped inside the tabs' column instead of pushing the "+" off.
        maxVisible = Math.Max(1, (int)Math.Floor((available - overflowButtonWidth) / perTabWidth));
        var visible = Tabs.Take(maxVisible).ToList();
        var overflow = Tabs.Skip(maxVisible).ToList();

        // The active tab must never be the one that gets hidden -- if it landed in the overflow
        // slice, swap it with the last "naturally visible" tab instead of pushing the visible count
        // past what the measured width can actually hold.
        if (ActiveTab is not null && overflow.Contains(ActiveTab) && visible.Count > 0)
        {
            var bumped = visible[^1];
            visible[^1] = ActiveTab;
            var overflowIndex = overflow.IndexOf(ActiveTab);
            overflow[overflowIndex] = bumped;
        }

        SyncTabCollections(visible, overflow);
    }

    private void SyncTabCollections(IReadOnlyList<TabSession> visible, IReadOnlyList<TabSession> overflow)
    {
        if (!VisibleTabs.SequenceEqual(visible))
        {
            VisibleTabs.Clear();
            foreach (var tab in visible) VisibleTabs.Add(tab);
        }
        if (!OverflowTabs.SequenceEqual(overflow))
        {
            OverflowTabs.Clear();
            foreach (var tab in overflow) OverflowTabs.Add(tab);
        }
        HasOverflowTabs = OverflowTabs.Count > 0;
        TabListMaxWidth = Math.Max(0, _tabStripWidth - 52 - (HasOverflowTabs ? 40 : 0));
    }

    public void UpdateRibbonWidth(double width)
    {
        if (width <= 0 || Math.Abs(width - _ribbonWidth) < 1) return;
        _ribbonWidth = width;
        RecomputeRibbonLayout();
    }

    // Called once at startup and whenever the active page changes (RaisePageVisibility). Today this
    // always returns the same two page-agnostic groups (Server Control/Quick Actions), matching
    // current behavior exactly -- later versions extend this to append page-specific groups based on
    // SelectedPage as their own buttons get relocated into the ribbon.
    private void RebuildRibbonGroups()
    {
        _allRibbonGroups = GateRibbonByRole(LocalizeRibbon(BuildRibbonGroupsForActivePage()));
        RecomputeRibbonLayout();
    }

    private List<RibbonGroupViewModel> BuildRibbonGroupsForActivePage()
    {
        var groups = new List<RibbonGroupViewModel>
        {
            new("Server Control",
            [
                new("↻", "Refresh", "Refresh", ConnectCommand, null, RibbonIconColor.Amber),
                new("▶", "Start", "Start server", StartCommand, null, RibbonIconColor.Green, IsSuccessButton: true),
                new("⟳", "Restart", "Restart server", RestartCommand, null, RibbonIconColor.Blue),
                new("■", "Stop", "Stop server", StopCommand, null, RibbonIconColor.Red, IsDangerButton: true),
            ]),
            new("Quick Actions",
            [
                new("⇩", "Backup", "Create backup", Backups.CreateBackupCommand, null, RibbonIconColor.Amber),
                new("▰", "Console", "Open Console", NavigateCommand, "Console", RibbonIconColor.Cyan),
                new("✚", "Doctor", "Open Doctor", NavigateCommand, "Doctor", RibbonIconColor.Green),
            ]),
        };

        // v0.7.26.0: first per-page ribbon groups -- each relocates buttons that used to live
        // in-page (see the release's architecture doc for the full before/after per page).
        if (IsServerSetupPage)
        {
            groups.Add(new("Environment",
            [
                new("✓", "Verify Files", "Verify environment files", VerifyEnvironmentCommand, null, RibbonIconColor.Green, IsSuccessButton: true),
                // v0.7.28.0: InstallMissingEnvironmentAsync already existed, previously only
                // reachable indirectly through a per-row action dispatch -- first standalone command.
                new("⬇", "Install Missing", "Install missing environment components", InstallMissingEnvironmentCommand, null, RibbonIconColor.Amber),
            ]));
        }
        else if (IsServerPage)
        {
            groups.Add(new("Configuration",
            [
                new("⇧", "Import", "Import configuration", null, null, RibbonIconColor.Amber, NativeDialogAction: "ImportConfig"),
                new("⇩", "Export", "Export configuration", null, null, RibbonIconColor.Cyan, NativeDialogAction: "ExportConfig"),
                new("✓", "Save", "Save configuration changes", Configuration.SavePalworldConfigurationCommand, null, RibbonIconColor.Green, IsSuccessButton: true),
                new("↺", "Reset", "Reset unsaved configuration changes", Configuration.ResetConfigChangesCommand, null, RibbonIconColor.Blue),
            ]));
        }
        else if (IsConsolePage)
        {
            groups.Add(new("Console",
            [
                new("↻", "Refresh", "Refresh console view", RefreshConsoleViewCommand, null, RibbonIconColor.Amber),
                new("Ⅱ", "Pause", "Pause or resume console view", Logs.PauseCommand, null, RibbonIconColor.Blue),
                new("✕", "Clear", "Clear console view", Logs.ClearCommand, null, RibbonIconColor.Red),
                new("⇩", "Export", "Export console view", null, null, RibbonIconColor.Cyan, NativeDialogAction: "ExportConsole"),
            ]));
        }
        else if (IsWorkspacePage)
        {
            groups.Add(new("Workspace",
            [
                new("↻", "Refresh", "Refresh workspace paths", Configuration.LoadConfigurationCommand, null, RibbonIconColor.Amber),
            ]));
        }
        else if (IsBackupsPage)
        {
            groups.Add(new("Backups",
            [
                new("⇩", "Create", "Create backup", Backups.CreateBackupCommand, null, RibbonIconColor.Amber, IsSuccessButton: true),
                new("✓", "Verify All", "Verify all backups", Backups.VerifyAllBackupsCommand, null, RibbonIconColor.Green),
                new("↻", "Refresh", "Refresh backups", Backups.RefreshBackupsCommand, null, RibbonIconColor.Blue),
                new("📁", "Open Root", "Open backup root", Backups.OpenBackupRootCommand, null, RibbonIconColor.Cyan),
            ]));
        }
        // v0.7.27.0: MOD Dashboard and MOD Library already shared one in-page toolbar via
        // IsModInventoryPage before this relocation -- kept that same combined gating here rather
        // than splitting it, so both pages keep reaching Refresh/Verify & Scan exactly as before.
        else if (IsModInventoryPage)
        {
            groups.Add(new("MODs",
            [
                new("↻", "Refresh MODs", "Refresh MODs", Mods.RefreshModsCommand, null, RibbonIconColor.Amber),
                new("✓", "Verify & Scan", "Verify and scan MODs", Mods.VerifyModsCommand, null, RibbonIconColor.Green, IsSuccessButton: true),
            ]));

            // v0.7.78.0: page-level (no-selection-required) MOD mutation actions relocated from an
            // in-page button row, direct follow-up to the button-row redesign ("should we move the
            // buttons to the ribbon?"). MOD Library only -- MOD Dashboard stays intentionally
            // read-only (its own page framing already says "Open MOD Library to change enabled
            // state," see v0.7.43.0). Selection-scoped actions (Enable/Disable/Rollback/Repair/
            // Delete Selected) deliberately stay OFF the ribbon and move to a right-click menu on
            // the Installed MODs list instead, matching how Bases/Guilds/Players already expose
            // per-row mutations rather than crowding the ribbon with selection-dependent buttons.
            if (IsModLibraryPage)
            {
                groups.Add(new("MOD Maintenance",
                [
                    new("✓", "Enable All", "Enable all MODs", Mods.EnableAllModsCommand, null, RibbonIconColor.Green, IsSuccessButton: true),
                    new("✕", "Disable All", "Disable all MODs", Mods.DisableAllModsCommand, null, RibbonIconColor.Amber),
                    new("⚑", "Repair", "Neutralize legacy MOD overrides", Mods.RepairModsCommand, null, RibbonIconColor.Blue),
                    new("⚠", "Safe-Start", "Diagnose which MOD is crashing or hanging startup", Mods.BeginModSafeStartCommand, null, RibbonIconColor.Red, IsDangerButton: true),
                ]));
            }
        }
        else if (IsUe4ssPage)
        {
            groups.Add(new("UE4SS",
            [
                new("↻", "Refresh Runtime", "Refresh UE4SS runtime", Mods.RefreshModsCommand, null, RibbonIconColor.Amber),
                new("👁", "Preview Install", "Preview installing the selected UE4SS release", Mods.PreviewUe4ssInstallCommand, null, RibbonIconColor.Blue),
                new("⬇", "Confirm Install", "Install the previewed UE4SS release", Mods.ApplyUe4ssInstallCommand, null, RibbonIconColor.Green, IsSuccessButton: true),
                new("↩", "Rollback", "Undo the last UE4SS install", Mods.RollbackUe4ssInstallCommand, null, RibbonIconColor.Red, IsDangerButton: true),
            ]));
        }
        else if (IsDoctorPage)
        {
            groups.Add(new("Doctor",
            [
                new("⚕", "Run Doctor", "Run doctor and refresh", RunDoctorCommand, null, RibbonIconColor.Green, IsSuccessButton: true),
                new("⇩", "Export Report", "Export diagnostic report", ExportDoctorCommand, null, RibbonIconColor.Cyan),
                // v0.7.28.0: reuses the same ForceStopServerCommand as the "Danger" group below --
                // functionally identical (force-terminate the managed process tree), just reachable
                // here too since this is the page showing exactly which processes it would kill.
                new("⛔", "Kill Processes", "Force-kill managed server processes", ForceStopServerCommand, null, RibbonIconColor.Red, IsDangerButton: true),
                // v0.7.44.0: machine-wide scan -- distinct from "Kill Processes" above, which only
                // ever targets this profile's own managed process tree via ForceStopServerCommand.
                new("🔎", "Detect Instances", "Scan for every Palworld process on this machine", RefreshAllInstancesCommand, null, RibbonIconColor.Amber),
            ]));
        }
        // v0.7.42.0: cross-page consistency sweep, applying the same ribbon-relocation pattern
        // established in v0.7.26.0-v0.7.28.0 to every remaining page that still had page-header
        // action buttons in-page. Sub-feature-local actions embedded in a labeled workflow card
        // (Fleet's Backup All/Doctor All/Update All, World Transactions' confirm-gated Repair
        // Center apply, Alert Center's per-collapsible-section Discord Bot/Anti-Cheat refreshes)
        // stay where they are, matching how Backups' Retention Cleanup apply button was kept
        // in-page rather than relocated -- only page-level primary actions move.
        else if (IsWorldExplorerPage)
        {
            groups.Add(new("World",
            [
                new("↻", "Refresh World", "Refresh world explorer", RefreshWorldExplorerCommand, null, RibbonIconColor.Amber),
            ]));
        }
        else if (IsWorldTransactionsPage)
        {
            groups.Add(new("World Transactions",
            [
                new("✓", "Validate", "Validate active world", Workspace.ValidateActiveWorldCommand, null, RibbonIconColor.Green, IsSuccessButton: true),
                new("⇩", "Export Report", "Export world validation report", null, null, RibbonIconColor.Cyan, NativeDialogAction: "ExportWorldValidation"),
                new("↻", "Refresh Ops", "Refresh operation platform", RefreshOperationsCommand, null, RibbonIconColor.Amber),
            ]));
        }
        else if (IsGuildsPage || IsBasesPage)
        {
            groups.Add(new("Guilds",
            [
                new("↻", "Refresh Evidence", "Refresh guild and base evidence", RefreshPlayerGuildExplorerCommand, null, RibbonIconColor.Amber),
            ]));
        }
        else if (IsMapPage)
        {
            groups.Add(new("Map",
            [
                new("↻", "Refresh Map", "Refresh online players and base locations", RefreshPlayersCommand, null, RibbonIconColor.Amber),
                new("⌂", "Refresh Bases", "Re-read base locations from the world save", Map.RefreshMapBasesCommand, null, RibbonIconColor.Cyan),
            ]));
        }
        else if (IsPlayersPage)
        {
            groups.Add(new("Players",
            [
                new("↻", "Discover Saves", "Discover player saves", RefreshPlayersCommand, null, RibbonIconColor.Amber),
                new("⇩", "Export CSV", "Export players CSV", null, null, RibbonIconColor.Cyan, NativeDialogAction: "ExportPlayersCsv"),
            ]));
        }
        else if (IsDiagnosticsPage)
        {
            groups.Add(new("Diagnostics",
            [
                new("⚡", "Run Diagnostics", "Run network diagnostics", RunNetworkDiagnosticsCommand, null, RibbonIconColor.Amber, IsSuccessButton: true),
                new("🛡", "Repair Firewall", "Add or repair firewall rule", RepairFirewallCommand, null, RibbonIconColor.Blue),
                new("⟳", "Restart Server", "Restart server", RestartFromDiagnosticsCommand, null, RibbonIconColor.Red, IsDangerButton: true),
            ]));
        }
        else if (IsSaveToolsPage)
        {
            groups.Add(new("Save Tools",
            [
                new("↻", "Refresh", "Refresh save tools", Backups.RefreshSaveToolsCommand, null, RibbonIconColor.Amber),
                new("✓", "Self-Tests", "Run save tools self-tests", Backups.RunSaveToolsSelfTestCommand, null, RibbonIconColor.Green, IsSuccessButton: true),
            ]));
        }
        else if (IsNotificationsPage)
        {
            groups.Add(new("Notifications",
            [
                new("↻", "Refresh", "Refresh notifications", RefreshNotificationsCommand, null, RibbonIconColor.Amber),
                new("✓", "Self-Test", "Notification self-test", NotificationSelfTestCommand, null, RibbonIconColor.Green),
                new("👁", "Mark All Read", "Mark all notifications read", MarkAllNotificationsReadCommand, null, RibbonIconColor.Blue),
                new("⇩", "Export", "Export visible notifications", null, null, RibbonIconColor.Cyan, NativeDialogAction: "ExportNotifications"),
            ]));
        }
        else if (IsAutomationPage)
        {
            groups.Add(new("Automation",
            [
                new("↻", "Refresh", "Refresh automation", RefreshAutomationCommand, null, RibbonIconColor.Amber),
            ]));
        }
        else if (IsAlertCenterPage)
        {
            groups.Add(new("Alerts",
            [
                new("↻", "Refresh", "Refresh alert center", RefreshAlertCenterCommand, null, RibbonIconColor.Amber),
            ]));
        }
        else if (IsSecurityPage)
        {
            groups.Add(new("Security",
            [
                new("↻", "Refresh", "Refresh security", RefreshSecurityCommand, null, RibbonIconColor.Amber),
            ]));
        }
        else if (IsFleetPage)
        {
            groups.Add(new("Fleet",
            [
                new("↻", "Refresh", "Refresh fleet", RefreshFleetCommand, null, RibbonIconColor.Amber),
            ]));
        }
        else if (IsHostPage)
        {
            groups.Add(new("Host",
            [
                new("↻", "Refresh", "Read the machine again", Host.RefreshHostCommand, null, RibbonIconColor.Amber),
            ]));
        }
        else if (IsCrashAnalyzerPage)
        {
            groups.Add(new("Crash Analyzer",
            [
                new("↻", "Refresh History", "Refresh crash history", RefreshCrashHistoryCommand, null, RibbonIconColor.Amber),
                new("🔍", "Run Analysis", "Analyze crashes", AnalyzeCrashesCommand, null, RibbonIconColor.Green, IsSuccessButton: true),
            ]));
        }
        else if (IsUpdateCenterPage)
        {
            groups.Add(new("Update Center",
            [
                new("↻", "Refresh", "Refresh distribution status", RefreshDistributionCommand, null, RibbonIconColor.Amber),
                new("📋", "Preview Plan", "Preview SteamCMD update plan", PreviewDistributionPlanCommand, null, RibbonIconColor.Blue),
            ]));
        }

        // v0.7.28.0: reachable from every page (not gated by SelectedPage), positioned last so it
        // renders at the right edge of whatever else is visible -- ForceStopServerAsync already
        // existed and was already reachable from ShutdownForExitAsync(force: true) on app exit; this
        // is its first user-facing exposure.
        groups.Add(new("Danger",
        [
            new("⛔", "Force Stop", "Force stop server", ForceStopServerCommand, null, RibbonIconColor.Red, IsDangerButton: true),
        ]));

        return groups;
    }

    // v0.8.6.0: the shown text comes from the translations ("ribbon.<Label>", "ribbon.group.<Title>", letters and digits
    // only); Label and Title stay English because RibbonIcons and the dialog dispatch key off them.
    public static string RibbonKey(string text) => new(text.Where(char.IsLetterOrDigit).ToArray());

    private static List<RibbonGroupViewModel> LocalizeRibbon(List<RibbonGroupViewModel> groups) =>
        groups.Select(g => g with
        {
            DisplayTitle = Localizer.Instance[$"ribbon.group.{RibbonKey(g.Title)}"],
            Actions = g.Actions.Select(a => a with { DisplayLabel = Localizer.Instance[$"ribbon.{RibbonKey(a.Label)}"] }).ToArray()
        }).ToList();

    // Groups don't share one fixed width like tabs do (RecomputeTabLayout's perTabWidth constant),
    // since button count/labels vary per group -- so this walks groups in order accumulating an
    // estimated width instead of a simple integer division.
    private void RecomputeRibbonLayout()
    {
        const double overflowButtonWidth = 40;

        if (_allRibbonGroups.Count == 0)
        {
            SyncRibbonCollections([], []);
            return;
        }

        var totalWidth = _allRibbonGroups.Sum(EstimateRibbonGroupWidth);
        if (totalWidth <= _ribbonWidth)
        {
            SyncRibbonCollections(_allRibbonGroups, []);
            return;
        }

        var available = Math.Max(0, _ribbonWidth - overflowButtonWidth);
        var visible = new List<RibbonGroupViewModel>();
        var used = 0.0;
        foreach (var group in _allRibbonGroups)
        {
            var groupWidth = EstimateRibbonGroupWidth(group);
            if (used + groupWidth > available) break;
            used += groupWidth;
            visible.Add(group);
        }
        // An empty ribbon would be worse than one slightly-clipped group -- always keep at least the
        // first one visible, even if it alone exceeds the measured available width.
        if (visible.Count == 0) visible.Add(_allRibbonGroups[0]);

        SyncRibbonCollections(visible, _allRibbonGroups.Skip(visible.Count).ToList());
    }

    // Border.ribbonGroup: Padding="6" (12px) + Margin="0,0,5,0" (5px) = 17px of group chrome.
    // Button.ribbon: MinWidth="68"; the buttons' own StackPanel: Spacing="5" between each.
    // v0.8.6.0: a button also grows with its label (translations run longer than English), so each is estimated as the
    // wider of its minimum and its text (about 7.2 px per character plus padding); so is the group title underneath.
    public static double EstimateRibbonGroupWidth(RibbonGroupViewModel group) =>
        Math.Max(17 + group.Actions.Sum(a => Math.Max(68, a.DisplayLabel.Length * 7.2 + 16)) + Math.Max(0, group.Actions.Count - 1) * 5,
                 17 + group.DisplayTitle.Length * 6 + 8);

    private void SyncRibbonCollections(IReadOnlyList<RibbonGroupViewModel> visible, IReadOnlyList<RibbonGroupViewModel> overflow)
    {
        if (!VisibleRibbonGroups.SequenceEqual(visible))
        {
            VisibleRibbonGroups.Clear();
            foreach (var group in visible) VisibleRibbonGroups.Add(group);
        }
        if (!OverflowRibbonGroups.SequenceEqual(overflow))
        {
            OverflowRibbonGroups.Clear();
            foreach (var group in overflow) OverflowRibbonGroups.Add(group);
        }
        HasOverflowRibbonGroups = OverflowRibbonGroups.Count > 0;
    }

    // One DispatcherTimer per open tab (replacing the single app-wide timer that existed before
    // true multi-tab support), so every open connection keeps polling independently of which tab
    // is focused. The active tab's tick runs the exact same full-refresh body the old shared timer
    // always ran (still correct, since it now operates through ActiveTab-delegated properties); a
    // background tab's tick runs a lightweight, tab-scoped health poll instead -- enough to keep
    // its bearer token alive and its own status dot accurate, without fanning the full page-data
    // refresh (Console, World Explorer, Player Registry, Configuration, etc.) out across every
    // open tab at once. That full-per-tab duplication is explicitly out of scope -- see the
    // architecture notes for why.
    private void WireTabTimer(TabSession tab)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += async (_, _) =>
        {
            if (!AutoRefreshEnabled)
                return;

            if (!ReferenceEquals(tab, ActiveTab))
            {
                await RefreshTabLightweightAsync(tab);
                return;
            }

            await RefreshActiveTabDataAsync();
        };
        tab.Timer = timer;
        timer.Start();
    }

    // v0.7.6.0: shared by the active tab's own timer tick above and by ActiveTab's setter (an
    // immediate, out-of-cycle refresh right after switching tabs) so a newly focused tab's player
    // list/dashboard data arrives within one round trip instead of waiting up to 5 seconds for the
    // next tick -- previously switching tabs displayed the PREVIOUS tab's data until that happened.
    private async Task RefreshActiveTabDataAsync()
    {
        if (IsBusy)
            return;

        if (SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id && ConnectionState != "Connected")
        {
            await RefreshLocalInstallationAsync();
            return;
        }

        if (ConnectionState != "Connected")
            return;

        await RefreshStatusPollingAsync();
        // v0.8.17.0: the HOST tab's readings follow the same 5-second tick while the page is open.
        if (IsHostPage) await Host.RefreshHostAsync(silent: true);
        _refreshTick++;
    }

    // Background-tab poll: operates directly on the tab's own stored fields, never through the
    // ActiveTab-delegated SelectedProfile/BearerToken/etc. properties -- those reflect whichever
    // tab is currently focused, and writing through them from a background tab's timer would
    // silently corrupt the focused tab's displayed connection state with a different tab's data.
    private async Task RefreshTabLightweightAsync(TabSession tab)
    {
        if (tab.Profile is null)
            return;

        ConnectionProfile profile;
        try { profile = BuildProfileFromTab(tab); }
        catch { return; }

        // v0.7.23.0: mirrors RefreshAsync's own health-check exception classification (~line 3016)
        // instead of a bare catch that discarded the real reason -- a version mismatch or a missing
        // bearer token previously collapsed into the exact same "Connection failed" as a genuinely
        // unreachable server, on a background tab an operator might not switch to for a while.
        try
        {
            var health = await _api.GetHealthAsync(profile, tab.BearerToken);
            if (!string.Equals(health.Component, "mysttiq-headless", StringComparison.OrdinalIgnoreCase) || health.ApiVersion < 1)
                throw new InvalidOperationException($"The endpoint answered health checks but is not a compatible MystTiq management API (component={health.Component}, apiVersion={health.ApiVersion}).");
            if (health.Authentication && string.IsNullOrWhiteSpace(tab.BearerToken))
                throw new UnauthorizedAccessException("This MystTiq management API requires a bearer token.");

            tab.ManagementApiConnected = true;
            tab.ConnectionState = "Connected";
        }
        catch (InvalidOperationException)
        {
            tab.ManagementApiConnected = false;
            tab.ConnectionState = "Incompatible API version";
        }
        catch (UnauthorizedAccessException)
        {
            tab.ManagementApiConnected = false;
            tab.ConnectionState = "Needs bearer token";
        }
        catch
        {
            tab.ManagementApiConnected = false;
            tab.ConnectionState = "Connection failed";
        }
    }

    // BuildProfileFromEditor's counterpart for a tab that isn't ActiveTab -- same validation, same
    // shape, sourced from that TabSession's own fields instead of the shared editor properties.
    private ConnectionProfile BuildProfileFromTab(TabSession tab)
    {
        if (string.IsNullOrWhiteSpace(tab.ProfileName))
            throw new InvalidOperationException("Profile name is required.");

        if (!Uri.TryCreate(tab.ServerUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Enter an absolute http:// or https:// MystTiq management URL.");

        var pin = MystTiqApiClient.NormalizeFingerprint(tab.CertificateSha256);
        var existingId = tab.Profile?.Id;
        var accentColorKey = ResolveAccentColorKey(existingId, tab.Profile);

        return new ConnectionProfile(existingId ?? Guid.NewGuid().ToString("N"), tab.ProfileName.Trim(), uri, pin, accentColorKey);
    }

    // v0.7.11.0: called from MainWindow's close-tab confirmation flow when the user chooses
    // "Stop & Close" for a tab that isn't necessarily ActiveTab (Fleet allows several servers
    // running at once) -- deliberately built the same way RefreshTabLightweightAsync operates on a
    // background tab's own fields directly, never through the ActiveTab-delegated properties,
    // since those would silently target whichever tab is currently focused instead of the one
    // actually being closed.
    public async Task StopTabServerAsync(TabSession tab)
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromTab(tab); }
        catch { return; }

        try { await _api.StopServerAsync(profile, tab.BearerToken, CancellationToken.None); }
        catch { /* best-effort -- the confirmation dialog already told the user what this would do */ }
    }

    private void DeleteSelectedProfile()
    {
        var selected = SelectedProfile;
        if (selected is null || selected.Id == ConnectionProfile.LocalDefault.Id)
            return;

        // v0.7.6.0: deleting a profile another open tab is actively using left that tab operating
        // against a profile no longer tracked anywhere (not in the picker, not in Profiles), with no
        // warning at the point of deletion -- mirrors ConnectExistingProfileTab's own duplicate-tab
        // guard rather than only checking at the point a NEW tab is opened.
        if (Tabs.Any(t => !ReferenceEquals(t, ActiveTab) && t.Profile?.Id == selected.Id))
        {
            Detail = $"Cannot delete '{selected.Name}' -- it is open in another tab. Close that tab first.";
            return;
        }

        Profiles.Remove(selected);
        _profileStore.Save(Profiles);
        _credentialStore.Delete(selected.Id);
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == ConnectionProfile.LocalDefault.Id) ?? Profiles.FirstOrDefault();
        Detail = $"Connection profile '{selected.Name}' deleted.";
    }

    // v0.7.71.0: clears a saved encrypted token for the currently-selected profile without
    // deleting the profile itself -- for a rotated/revoked token, or simply not wanting it
    // remembered. Only clears the ON-DISK entry; BearerToken in the editor is left as-is so the
    // field doesn't go blank mid-edit (the user can still Connect with what's typed there).
    private void ForgetSavedToken()
    {
        if (SelectedProfile is not { } selected) return;
        _credentialStore.Delete(selected.Id);
        Detail = $"Saved token forgotten for '{selected.Name}'. It will need to be entered again next time.";
    }

    private async Task InitializeLocalDashboardAsync()
    {
        var snapshot = await RefreshLocalInstallationAsync();
        if (SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id)
        {
            await RefreshAsync(silent: false);
            if (ConnectionState != "Connected" && snapshot is not null)
            {
                StatusBarText = "Starting local MystTiq management API…";
                var bootstrap = await _localBootstrapper.EnsureAvailableAsync(snapshot);
                LocalApiStatus = bootstrap.Available ? "Available" : "Unavailable";
                LocalDiscoveryDetail = $"{LocalDiscoveryDetail} {bootstrap.Detail}".Trim();
                if (bootstrap.Available)
                {
                    ServerUrl = bootstrap.Endpoint;
                    await RefreshAsync(silent: false);
                    if (ConnectionState == "Connected" && !snapshot.ServiceInstalled)
                        ServiceState = "Standalone headless API";
                }
            }
            if (ConnectionState != "Connected")
                await DiscoverServicesAsync();
        }
    }

    // v0.7.72.0: reopens whichever OTHER profiles were still open as tabs the last time the app
    // exited (see TabSessionStore/SaveTabSession) -- previously every launch started back at just
    // the single default local tab, no matter how many tabs (e.g. a remote server) were open before.
    // Runs after InitializeLocalDashboardAsync so tab 1 (the default local profile) is already
    // settled; each remembered extra profile reconnects the same way the "+" menu's "Connect to
    // {profile.Name}" entries do (ConnectExistingProfileTab), including its own duplicate-tab guard.
    private async Task RestoreTabSessionAsync()
    {
        var rememberedIds = _tabSessionStore.Load();
        if (rememberedIds.Count == 0) return;

        foreach (var profileId in rememberedIds)
        {
            var profile = Profiles.FirstOrDefault(p => p.Id == profileId);
            if (profile is null) continue; // profile was deleted since the session was saved
            ConnectExistingProfileTab(profile);
            await Task.Delay(50); // stagger connects instead of firing every RefreshAsync at once
        }
    }

    private async Task DiscoverServicesAsync()
    {
        // v0.9.6.0: the search no longer holds the whole window busy; each service appears the moment it answers, and
        // the search can be cancelled.
        using var cancellation = new CancellationTokenSource();
        _discoveryCancellation = cancellation;
        IsDiscovering = true;
        DiscoveryProgressValue = 0;
        DiscoveredServices.Clear();
        DiscoveryStateText = "Scanning local IPv4 networks for MystTiq services…";
        var port = Configuration.ConfigPort is > 0 and <= 65535 ? Configuration.ConfigPort : 8213;
        DiscoveryProgress? last = null;
        var finished = false;
        var progress = new Progress<DiscoveryProgress>(update =>
        {
            // Reports are posted to the window's thread and can arrive after the search has returned.
            if (finished) return;
            last = update;
            DiscoveryRangesText = string.Join(" · ", update.Ranges.Select(r => r.DisplayText));
            DiscoverySkippedText = string.Join(" · ", update.Plan.Skipped.Select(r => r.DisplayText));
            DiscoveryProblemsText = string.Join(", ", update.Plan.Problems);
            DiscoveryProgressValue = update.Total == 0 ? 100 : 100.0 * update.Checked / update.Total;
            if (update.Found is { } found && !DiscoveredServices.Any(s => s.BaseAddress == found.BaseAddress))
                DiscoveredServices.Add(found);
            if (!cancellation.IsCancellationRequested)
                DiscoveryStateText = DiscoveryRunningText(update.Checked, update.Total, update.Ranges.Count, DiscoveredServices.Count);
        });
        try
        {
            var discovered = await _serviceDiscovery.DiscoverAsync(port, new DiscoveryOptions(DiscoveryIncludeVirtualAdapters, DiscoveryExtraRanges), progress, cancellation.Token);
            finished = true;
            DiscoveredServices.Clear();
            foreach (var service in discovered)
                DiscoveredServices.Add(service);

            var seconds = (last?.Elapsed ?? TimeSpan.Zero).TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            DiscoveryStateText = discovered.Count switch
            {
                0 => $"No MystTiq services answered /healthz on port {port}.",
                1 => $"1 MystTiq service discovered in {seconds} s.",
                _ => $"{discovered.Count} MystTiq services discovered in {seconds} s."
            };
            DiscoveryProgressValue = 100;
            // The local profile must prefer loopback. LAN services are available for explicit selection
            // in Settings, but must never displace a healthy local sidecar automatically.
            var preferred = SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id
                ? discovered.FirstOrDefault(item => System.Net.IPAddress.TryParse(item.Address, out var candidateAddress) && System.Net.IPAddress.IsLoopback(candidateAddress)) ?? discovered.FirstOrDefault()
                : discovered.FirstOrDefault();
            // v0.9.6.0: a connected tab only shows what was found. Selecting a service rewrites this tab's address and
            // marks it "Not connected", so every search disconnected the tab it was run from (seen live on the clone tab).
            if (preferred is not null && ManagementApiConnected)
            {
                _selectedDiscoveredService = DiscoveredServices.FirstOrDefault(s => s.BaseAddress.ToString().TrimEnd('/') == ServerUrl.TrimEnd('/')) ?? preferred;
                RaisePropertyChanged(nameof(SelectedDiscoveredService));
            }
            else if (preferred is not null)
            {
                SelectedDiscoveredService = preferred;
                var isLoopback = System.Net.IPAddress.TryParse(preferred.Address, out var address) && System.Net.IPAddress.IsLoopback(address);
                if (SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id && isLoopback && !preferred.AuthenticationEnabled)
                {
                    ServerUrl = preferred.BaseAddress.ToString().TrimEnd('/');
                    await RefreshAsync(silent: false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            finished = true;
            DiscoveryStateText = $"Search cancelled after {last?.Checked ?? 0} of {last?.Total ?? 0} addresses; {DiscoveredServices.Count} found.";
        }
        catch (Exception ex)
        {
            finished = true;
            DiscoveryStateText = $"LAN discovery failed: {ex.Message}";
        }
        finally
        {
            _discoveryCancellation = null;
            IsDiscovering = false;
        }
    }

    public static string DiscoveryRunningText(int checkedCount, int total, int ranges, int found) =>
        $"Scanning {total} address(es) in {ranges} range(s)… {checkedCount} checked, {found} found.";

    private async Task<LocalInstallationSnapshot?> RefreshLocalInstallationAsync()
    {
        try
        {
            var snapshot = await _localDiscovery.DiscoverAsync();
            LocalServiceStatus = snapshot.ServiceState switch
            {
                LocalMystTiqServiceState.Running => "Running",
                LocalMystTiqServiceState.Stopped => "Stopped",
                LocalMystTiqServiceState.NotInstalled => "Not installed",
                LocalMystTiqServiceState.Unreachable => "Unreachable",
                _ => "Unknown"
            };
            LocalPalServerStatus = snapshot.PalServerState == LocalPalServerInstallationState.Found ? "Found" :
                snapshot.PalServerState == LocalPalServerInstallationState.NotFound ? "Not found" : "Unknown";
            LocalServerRootText = snapshot.ServerRoot ?? "Not discovered";
            LocalConfigPathText = snapshot.ConfigurationPath ?? "No MystTiq configuration discovered";
            LocalDiscoverySource = snapshot.DiscoverySource;
            LocalDiscoveryDetail = snapshot.Detail;
            ServiceState = LocalServiceStatus;
            ServerInstallState = snapshot.ServerExecutableExists ? "Installed / Found" : snapshot.PalServerProcessDetected ? "Process detected" : "Not found";
            SteamCmdState = snapshot.SteamCmdExists ? "Installed" : "Not found";
            DistributionPlatform = snapshot.Platform;
            DistributionDetail = snapshot.Detail;
            if (snapshot.PalServerProcessDetected)
            {
                ServerState = "Running (local discovery)";
                NativePidText = snapshot.PalServerProcessId?.ToString() ?? "—";
            }
            else if (snapshot.ServerExecutableExists)
            {
                ServerState = "Installed / process not detected";
            }

            if (SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id && !string.IsNullOrWhiteSpace(snapshot.ApiBaseAddress))
                ServerUrl = snapshot.ApiBaseAddress.TrimEnd('/');

            LocalApiStatus = snapshot.ApiAuthenticationEnabled && string.IsNullOrWhiteSpace(BearerToken)
                ? "Authentication required"
                : "Checking";
            return snapshot;
        }
        catch (Exception ex)
        {
            LocalServiceStatus = "Unreachable";
            LocalPalServerStatus = "Unknown";
            LocalApiStatus = "Configuration error";
            LocalDiscoveryDetail = ex.Message;
            return null;
        }
    }

    private async Task RefreshDashboardBackendAsync(ConnectionProfile profile)
    {
        await RefreshMonitoringAsyncCore(profile);

        try
        {
            var distribution = await _api.GetServerDistributionStatusAsync(profile, BearerToken);
            DistributionPlatform = distribution.Platform;
            SteamCmdState = distribution.SteamCmdExists ? "Installed" : "Missing";
            ServerInstallState = distribution.IncompleteReason is not null ? "Incomplete" : distribution.ServerExecutableExists ? "Installed" : "Missing";
            DistributionState = distribution.IncompleteReason is not null ? "Update required" : distribution.ServerExecutableExists ? "Ready" : "Setup required";
            DistributionDetail = distribution.Detail;

            // v0.7.31.0: install-location identity, not host:port -- see TabSession.ServerRoot.
            if (ActiveTab is not null && !string.IsNullOrWhiteSpace(distribution.ServerRoot))
            {
                ActiveTab.ServerRoot = distribution.ServerRoot;
                RecomputeDuplicateInstallWarnings();
            }
        }
        catch (Exception ex) { DistributionDetail = $"Distribution: {ex.Message}"; }

        try
        {
            var backups = await _api.GetBackupsAsync(profile, BearerToken);
            Backups.PopulateBackupItems(backups);
            Backups.SelectedBackup = Backups.BackupItems.FirstOrDefault();
            Backups.BackupState = $"{backups.Count} backup(s)";
            Backups.BackupTotalSizeText = $"{backups.TotalSizeBytes / 1024d / 1024d:F2} MB";
            Dashboard.BackupText = backups.Count == 0 ? "No backups" : $"{backups.Count} available";
            Dashboard.BackupStripText = backups.Count == 0 ? "Backup: none" : $"Backup: {backups.Count} available";
        }
        catch (Exception ex) { Backups.BackupDetail = $"Dashboard backup load: {ex.Message}"; }

        try
        {
            var world = await _api.GetWorldExplorerAsync(profile, BearerToken);
            Workspace.ActiveWorldIdText = world.ActiveWorldId ?? "Not resolved";
            Workspace.WorldCountText = world.WorldCount.ToString();
            Workspace.WorldPlayerSaveCountText = world.PlayerSaveCount.ToString();
            Workspace.WorldSizeText = $"{world.TotalSizeBytes / 1024d / 1024d:F2} MB";
            Dashboard.WorldNicknameText = BuildWorldNickname(world.ActiveWorldId);
            Dashboard.WorldText = world.Available ? Workspace.ActiveWorldIdText : "Unavailable";
            Dashboard.WorldPulseText = world.Available ? $"{Workspace.WorldPlayerSaveCountText} player saves · {Workspace.WorldSizeText}" : "World data unavailable";
        }
        catch (Exception ex) { World.WorldExplorerDetail = $"Dashboard world load: {ex.Message}"; }

        try
        {
            var identities = await _api.GetPlayerGuildExplorerAsync(profile, BearerToken);
            Players.PlayerRecordCountText = identities.Players.Count.ToString();
            World.GuildRecordCountText = identities.Guilds.Count.ToString();
            Dashboard.GuildText = $"{identities.Guilds.Count} guild(s) · {identities.Players.Count} known player(s)";
        }
        catch (Exception ex) { World.PlayerGuildDetail = $"Dashboard identity load: {ex.Message}"; }

        try
        {
            // v0.6.4.0: "no health deduction should exist without a corresponding visible Doctor
            // finding" -- the Dashboard badge now overrides its lifecycle-only default (already set
            // by ApplyStatus above) with the unified Doctor/Environment report whenever it actually
            // found something wrong, so a Fail/Warning finding is always reflected here too. Not
            // fetched on the fast silent poll tick (only this full-refresh path) since Doctor's own
            // checks (journalctl, disk space) are too heavy to run every poll interval.
            LatestDiagnosticsReport = await _api.GetDiagnosticsReportAsync(profile, BearerToken);
            if (LatestDiagnosticsReport.Failures > 0)
            {
                DashboardHealthText = "ATTENTION";
                Dashboard.HealthDetail = $"{LatestDiagnosticsReport.Failures} Doctor finding(s) need attention. Open Doctor.";
            }
            else if (LatestDiagnosticsReport.Warnings > 0 && DashboardHealthText == "READY")
            {
                DashboardHealthText = "DEGRADED";
                Dashboard.HealthDetail = $"{LatestDiagnosticsReport.Warnings} Doctor finding(s) are warnings. Open Doctor.";
            }
        }
        catch (Exception ex) { DiagnosticsReportDetail = $"Dashboard diagnostics load: {ex.Message}"; }

        try
        {
            var mods = await _api.GetModsAsync(profile, BearerToken);
            Mods.ApplyModInventory(mods);
            Dashboard.ModText = $"{mods.Installed} installed · {mods.ConfirmedIssues} confirmed issue(s)";
        }
        catch (Exception ex) { Mods.ModSummary = $"Dashboard MOD load: {ex.Message}"; }
    }

    private async Task RefreshStatusPollingAsync()
    {
        // v0.7.6.0: OnlinePlayers/ServerState/logs/etc. are not themselves tab-scoped, so a response
        // for a tab the user has since switched away from must never be allowed to land on top of
        // the newly active tab's data -- captured before the request, checked after it returns.
        var requestTab = ActiveTab;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { StatusBarText = ex.Message; return; }

        try
        {
            // v0.7.33.0: log count raised to the server's real 500-line cap (was 120) -- this is
            // the recurring per-tab timer tick that normally feeds the Dashboard's log card, so
            // leaving it at 120 here would have undone the fuller view moments after any explicit
            // refresh or lifecycle operation supplied it.
            var snapshot = await _api.GetStatusPollingAsync(profile, 500, BearerToken);
            if (!ReferenceEquals(requestTab, ActiveTab)) return;
            ApplyStatus(snapshot.Status);
            ApplyServiceStatus(snapshot.Service);
            // v1.0.0.1: the addresses line (at most once a minute; the service keeps the public address for an hour).
            _ = Dashboard.RefreshHostAddressesAsync(profile, requestTab, force: false);
            // v1.0.2.0 (roadmap R-2): the Dashboard's warning when a switched-on channel has no proven delivery.
            _ = RefreshDeliveryHealthAsync(profile, requestTab, force: false);
            ApplyPlayers(snapshot.Players);
            ApplyMetrics(snapshot.Metrics);
            ApplyLogs(snapshot.LogTail);
            ApplyDashboardSupport(snapshot);
            ManagementApiConnected = true;
            ConnectionState = "Connected";
            NoteConnected(snapshot.Status, snapshot.Players);
            StatusBarText = $"Connected — {ServerState} — {snapshot.Players.OnlineCount} player(s)";
            StatusBarObservedText = $"Updated {snapshot.ObservedAt.ToLocalTime():HH:mm:ss}";
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(requestTab, ActiveTab)) return;
            // v1.0.7.2 (roadmap Q-7): the recurring poll names the cause too (and says when it is reconnecting).
            NoteConnectionFailure(ex, profile.BaseAddress);
            StatusBarText = $"Status unavailable — {ex.Message}";
            StatusBarObservedText = $"Failed {DateTimeOffset.Now:HH:mm:ss}";
        }
    }

    private async Task RefreshAsync() => await RefreshAsync(silent: false);

    private async Task RefreshAsync(bool silent)
    {
        // v0.7.6.0: see the matching guard in RefreshStatusPollingAsync -- a response for a tab the
        // user has since switched away from must never overwrite the newly active tab's data.
        var requestTab = ActiveTab;

        if (SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id)
            await RefreshLocalInstallationAsync();

        ConnectionProfile profile;
        try
        {
            profile = BuildProfileFromEditor(SelectedProfile?.Id);
        }
        catch (Exception ex)
        {
            ConnectionState = "Invalid profile";
            Detail = ex.Message;
            return;
        }

        IsBusy = true;
        if (!silent)
        {
            ConnectionState = "Connecting…";
            ServerState = "Checking";
        }

        try
        {
            var health = await _api.GetHealthAsync(profile, BearerToken);
            if (!string.Equals(health.Component, "mysttiq-headless", StringComparison.OrdinalIgnoreCase) || health.ApiVersion < 1)
                throw new InvalidOperationException($"The endpoint answered health checks but is not a compatible MystTiq management API (component={health.Component}, apiVersion={health.ApiVersion}).");
            if (health.Authentication && string.IsNullOrWhiteSpace(BearerToken))
                throw new UnauthorizedAccessException("This MystTiq management API requires a bearer token: paste one, or sign in with a user account below. The desktop-owned local sidecar should not require remote credentials.");

            // Initial connection uses the same aggregate endpoint as periodic status polling. This
            // prevents connection establishment from fanning out into independent status/service
            // calls. v0.7.33.0: log count raised to 500 (was 120), matching the other log-bearing
            // call sites, so a freshly opened tab's log view isn't starved from the first paint.
            var poll = await _api.GetStatusPollingAsync(profile, 500, BearerToken);
            if (!ReferenceEquals(requestTab, ActiveTab)) return;
            ApplyStatus(poll.Status);
            ApplyServiceStatus(poll.Service);
            ApplyPlayers(poll.Players);
            ApplyMetrics(poll.Metrics);
            ApplyLogs(poll.LogTail);
            ApplyDashboardSupport(poll);
            ManagementApiConnected = true;
            ConnectionState = "Connected";
            LocalApiStatus = SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id ? "Connected" : LocalApiStatus;
            Detail = $"Connected to MystTiq {health.Version} ({health.Platform}) at {profile.BaseAddress}.";
            // v1.0.6.1: a local service older than this app runs the servers without the fixes since its version.
            CheckLocalServiceVersion(health.Version, profile.BaseAddress);

            // v0.7.71.0: remember a successfully-used token (encrypted, see CredentialStore) so the
            // next connect to this same profile doesn't need it re-pasted. Only saves on a proven
            // successful connect, never a token that was merely typed -- an invalid paste never
            // reaches this line. No-op on non-Windows builds.
            if (!string.IsNullOrWhiteSpace(BearerToken))
                _credentialStore.Save(profile.Id, BearerToken);

            // v0.7.114.0: know who is connected from the start, so every role-gated card (CanManageAdmin,
            // CanManagePrincipals) reflects the real role on every page, not only after visiting Security.
            // With authentication off everyone is Owner, which is what a null principal already means.
            try { ApplyCurrentPrincipal(health.Authentication ? await _api.WhoAmIAsync(profile, BearerToken) : null); }
            catch (Exception) { /* the Security page reports this properly; the connection itself is fine */ }

            // v0.7.64.0: reported live -- connecting to an already-running remote server still
            // routed through wizard Step 2 ("In-Game Server Defaults (new server only)", its own
            // title says so), forcing an extra click through server-creation UI that's irrelevant
            // to a server that's plainly already configured and running. ServerIsRunning at this
            // point reflects the poll this same method just applied a few lines up, so it's a
            // reliable signal, not a stale/optimistic guess. Only short-circuits the wizard, never
            // touches real settings -- CreateDefaultServerSettingsCommand (Settings step's own
            // action) was already a no-op against an existing PalWorldSettings.ini before this
            // change; skipping straight to Confirm is strictly less surprising than showing disabled
            // World Source/Install/Settings steps for a server that's plainly already set up.
            //
            // v0.7.88.0 bug fix: this used to also fire for IsNewServerSetupFlow, which broke the
            // "Set Up New Server" wizard entirely -- reported live as "it jumps right to [Confirm]."
            // Root cause: step 1's connection for the New Server Setup flow is ALWAYS to the shared
            // default local management API (no new fleet profile exists yet at step 1), so
            // ServerIsRunning here reflects whatever the DEFAULT profile's own server is doing, not
            // "is the new server already set up" -- which is nonsensical anyway since it doesn't
            // exist yet. Any user who already has a running default server (the common case) hit this
            // on every single "+ New Server" attempt. Scoped to the Connect flow only, where the
            // connection genuinely targets the server being configured.
            if (IsCreatingNewProfile && !IsNewServerSetupFlow && NewServerWizardStep == 1 && ServerIsRunning)
                NewServerWizardStep = 5;
            // v0.7.82.0: complementary case -- a genuinely fresh, not-yet-running connection (the
            // common case for "Set Up New Server," auto-triggered by OpenNewServerTab above) skips
            // straight to World Source the moment the local engine connects, instead of leaving the
            // user sitting on a screen that reads like "connect to an existing service."
            else if (IsCreatingNewProfile && NewServerWizardStep == 1 && IsNewServerSetupFlow && ManagementApiConnected)
                AdvanceWizardStep();

            await RefreshHistoricalMetricsAsync(force: true);

            if (!silent)
                await RefreshDashboardBackendAsync(profile);
        }
        catch (Exception ex)
        {
            ManagementApiConnected = false;
            ConnectionState = "Connection failed";
            NoteConnectionFailure(ex, SelectedProfile?.BaseAddress);
            if (SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id)
            {
                if (LocalApiStatus != "Authentication required")
                    LocalApiStatus = "Unavailable";
                ServerState = NativePidText != "—" ? "Running (local discovery)" :
                    LocalPalServerStatus == "Found" ? "Installed / API unavailable" : "Unknown";
                Detail = $"Local PalServer discovery: {LocalPalServerStatus}. Management API unavailable: {ex.Message}";
            }
            else
            {
                ServerState = "Unknown";
                ServiceState = "Unknown";
                Detail = ex.Message;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshMonitoringAsync()
    {
        ConnectionProfile profile;
        try
        {
            profile = BuildProfileFromEditor(SelectedProfile?.Id);
        }
        catch (Exception ex)
        {
            MonitoringDetail = ex.Message;
            return;
        }

        IsBusy = true;
        try
        {
            await RefreshMonitoringAsyncCore(profile);
        }
        finally
        {
            IsBusy = false;
        }
    }





    private async Task RefreshMonitoringAsyncCore(ConnectionProfile profile)
    {
        try
        {
            var playersTask = _api.GetPlayersAsync(profile, BearerToken);
            // v0.7.33.0: was 120 -- well under GetLogTail's own 500-line server cap, so merging
            // 2-4 sources (MystTiq stdout, Pal.log, AdminCommands logs) left as few as ~40 lines
            // per source, starving exactly the kind of full UE4SS/MOD LOAD startup diagnostics a
            // modded server writes. Requesting the server's actual max fixes both this shared core
            // (feeds the Dashboard's mini log card) and the Console page's own fuller view, which
            // filters from this same LogLines collection.
            var logsTask = _api.GetLogTailAsync(profile, 500, BearerToken);
            var metricsTask = _api.GetMetricsAsync(profile, BearerToken);

            await Task.WhenAll(playersTask, logsTask, metricsTask);

            ApplyPlayers(await playersTask);
            ApplyLogs(await logsTask);
            ApplyMetrics(await metricsTask);
            if (IsConsolePage)
                await RefreshConsoleCaptureStatusCoreAsync(profile);
        }
        catch (Exception ex)
        {
            MonitoringDetail = $"Monitoring refresh failed: {ex.Message}";
        }
    }

    private void ApplyPlayers(PlayersSnapshotDto snapshot)
    {
        OnlinePlayers.Clear();
        foreach (var player in snapshot.Players)
            OnlinePlayers.Add(player);

        OnlinePlayerCountText = snapshot.OnlineCount.ToString();
        Dashboard.PlayersSessionText = $"{snapshot.OnlineCount} online";
        PlayersState = snapshot.Available
            ? snapshot.OnlineCount == 0 ? "REST available / 0 online" : $"{snapshot.OnlineCount} online"
            : "REST unavailable";

        MonitoringDetail = snapshot.Detail;
        Map.RebuildPlayerMapPoints(snapshot.Players);
    }

















    // v0.7.96.0: says plainly what is (and is not) drawn, so an empty map is never a mystery.
    // Player dots come from the running server's REST API; bases come from the world save, so
    // bases can be present while nobody (or nothing) is online.





    // v1.0.8.0 (roadmap C-0): the session the page ViewModels share, and the pages moved out of this file (C-1 to C-9 add
    // theirs). The Console page and its log lines live in ConsoleLogViewModel; the Dashboard's part stays here until C-1.
    public SessionContext Session { get; }
    public ConsoleLogViewModel Logs { get; }
    public HostPageViewModel Host { get; }
    public DashboardViewModel Dashboard { get; }
    // The HOST page's navigation flags (the nav pane and the category bar bind them).
    public bool IsHostPage => SelectedPage == NavigationPage.Host;
    public bool IsV5HostCategory => SelectedPage == NavigationPage.Host;
    public PlayersViewModel Players { get; }
    public MapViewModel Map { get; }
    public WorldViewModel World { get; }
    public ConfigurationViewModel Configuration { get; }
    public WorkspaceViewModel Workspace { get; }
    public BackupsViewModel Backups { get; }
    public ModsViewModel Mods { get; }
    private readonly IReadOnlyList<PageViewModel> _pages;

    // v1.0.8.2 (roadmap C-2): the Players page refresh is one part of the whole status refresh, so it stays here.
    private async Task RefreshPlayersPageAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { Players.PlayersPageDetail = ex.Message; return; }

        // v0.7.8.0: same guard as RefreshAsync/RefreshStatusPollingAsync -- a response for a tab the
        // user has since switched away from must not overwrite the newly active tab's player list.
        var requestTab = ActiveTab;
        var selectedId = Players.SelectedPlayerRecord?.PlayerId;
        IsBusy = true;
        Players.PlayersPageState = "Discovering live and saved players…";
        try
        {
            // One aggregate status request supplies all live state. Save discovery remains an
            // explicit headless endpoint so remote desktops never inspect server-side paths.
            var pollTask = _api.GetStatusPollingAsync(profile, 120, BearerToken);
            var explorerTask = _api.GetPlayerGuildExplorerAsync(profile, BearerToken);
            await Task.WhenAll(pollTask, explorerTask);
            if (!ReferenceEquals(requestTab, ActiveTab)) return;

            var poll = await pollTask;
            ApplyStatus(poll.Status);
            ApplyServiceStatus(poll.Service);
            ApplyPlayers(poll.Players);
            ApplyMetrics(poll.Metrics);
            ApplyLogs(poll.LogTail);
            ApplyDashboardSupport(poll);

            var snapshot = await explorerTask;
            Map.ApplyBaseLocations(snapshot);
            Players.PlayerRecords.Clear();
            foreach (var player in snapshot.Players.OrderByDescending(x => x.Online).ThenBy(x => x.PlayerName).ThenBy(x => x.PlayerId))
                Players.PlayerRecords.Add(player);

            Players.ApplyPlayerFilters();
            Players.SelectedPlayerRecord = !string.IsNullOrWhiteSpace(selectedId)
                ? Players.PlayerRecords.FirstOrDefault(x => string.Equals(x.PlayerId, selectedId, StringComparison.OrdinalIgnoreCase))
                : Players.FilteredPlayerRecords.FirstOrDefault();

            Players.PlayersPageState = snapshot.Available ? $"Ready · {poll.Players.OnlineCount} online" : "Player discovery unavailable";
            Players.PlayersPageDetail = snapshot.Detail;
            ManagementApiConnected = true;
            ConnectionState = "Connected";
            StatusBarObservedText = $"Updated {poll.ObservedAt.ToLocalTime():HH:mm:ss}";

            if (Players.SelectedPlayerRecord is not null)
                await Players.LoadSelectedPlayerMetadataCoreAsync(profile, Players.SelectedPlayerRecord.PlayerId);

            try
            {
                var providers = await _api.GetPlayerModerationProvidersAsync(profile, BearerToken);
                Players.ModerationProviderStatusText = providers.Count == 0
                    ? string.Empty
                    : string.Join("   ", providers.Select(x => $"{x.DisplayName}: {x.Health}"));
            }
            catch { Players.ModerationProviderStatusText = string.Empty; }
        }
        catch (Exception ex)
        {
            Players.PlayersPageState = "Unavailable";
            Players.PlayersPageDetail = ex.Message;
        }
        finally { IsBusy = false; }
    }

    private ConnectionProfile? ActiveProfileOrNull()
    {
        try { return BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (InvalidOperationException) { return null; }
    }

    private void ApplyLogs(LogTailSnapshotDto snapshot)
    {
        LogFileText = snapshot.Available
            ? snapshot.FileName ?? "Palworld log"
            : "Log unavailable";
        if (!snapshot.Available)
            MonitoringDetail = snapshot.Detail;

        // v1.0.7.3 (roadmap Q-6): a poll with no new lines changes nothing.
        if (!Logs.Apply(snapshot)) return;
        Dashboard.ActivityLines.ReplaceAll(Logs.LogLines.Where(x => !string.IsNullOrWhiteSpace(x)).TakeLast(8));
        Dashboard.LastActivityText = Dashboard.ActivityLines.Count > 0 ? Dashboard.ActivityLines[^1] : "No recent console activity";
    }

    private async Task RunRconDoctorAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var result = await _api.RunRconDoctorAsync(SelectedProfile, BearerToken);
            RconConnected = result.Authenticated;
            RconState = result.Success ? "Ready" : "Unavailable";
            RconDetail = result.Detail;
            RconOutputLines.Clear();
            foreach (var check in result.Checks) RconOutputLines.Add(check);
        }
        catch (Exception ex) { RconConnected = false; RconState = "Error"; RconDetail = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task ConnectRconAsync() => await RunRconDoctorAsync();

    private void DisconnectRcon()
    {
        RconConnected = false;
        RconState = "Disconnected";
        RconDetail = "MystTiq uses short-lived server-side RCON sessions; no credential or socket remains in the GUI.";
    }

    private async Task SendRconCommandAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var result = await _api.SendRconCommandAsync(SelectedProfile, RconCommandText, BearerToken);
            RconState = result.Success ? "Command complete" : "Command failed";
            RconDetail = result.Message;
            RconOutputLines.Add($"> {result.Command}");
            if (!string.IsNullOrWhiteSpace(result.Response))
                foreach (var line in result.Response.Replace("\r", string.Empty).Split('\n')) RconOutputLines.Add(line);
            if (!result.Success) RconOutputLines.Add($"ERROR: {result.Message}");
        }
        catch (Exception ex) { RconState = "Error"; RconDetail = ex.Message; RconOutputLines.Add($"ERROR: {ex.Message}"); }
        finally { IsBusy = false; }
    }

    private void ApplyDashboardSupport(StatusPollingDto snapshot)
    {
        var world = snapshot.World;
        if (world.Available)
        {
            Workspace.ActiveWorldIdText = world.ActiveWorldId ?? "Not resolved";
            Dashboard.WorldNicknameText = BuildWorldNickname(world.ActiveWorldId);
            Dashboard.WorldText = Workspace.ActiveWorldIdText;
            Workspace.WorldPlayerSaveCountText = world.PlayerSaveCount.ToString();
            Workspace.WorldSizeText = $"{world.TotalSizeBytes / 1024d / 1024d:F2} MB";
            Dashboard.WorldPulseText = $"{world.PlayerSaveCount} player save(s) · {Workspace.WorldSizeText}";
            Dashboard.WorldClockText = world.WorldDayNumber.HasValue
                ? $"Day {world.WorldDayNumber.Value:N0} • {world.WorldTimeText ?? "--:--"}"
                : "Day — • --:--";
            // v1.0.0.4: the day is read from a decoded copy of Level.sav; when the world was saved after that decode, say so
            // (live, the Dashboard showed Day 210 for three days while the world was at Day 248).
            Dashboard.WorldClockDetailText = !world.WorldDayNumber.HasValue
                ? "Decoded Level.sav JSON unavailable — MystTiq will not estimate the day"
                : world.WorldClockCurrent || world.WorldClockAsOfUtc is null
                    ? "Exact saved world clock from GameTimeSaveData.GameDateTimeTicks"
                    : $"From the save as of {world.WorldClockAsOfUtc.Value.ToLocalTime():g}; the world has been saved since and is being read again.";
            Dashboard.PulseSaveText = world.LastWorldSaveUtc.HasValue
                ? $"World save: {FormatAge(DateTimeOffset.Now - world.LastWorldSaveUtc.Value.ToLocalTime())} ago"
                : "World save: —";
        }
        else
        {
            // v0.7.89.0 bug fix: reported live -- a brand-new server's Dashboard showed the exact
            // same "Active World" card (ID, player-save count, size) as a completely different,
            // already-running server, with no clone involved. Root cause: these fields are plain
            // top-level ViewModel properties (not per-tab), and this branch only ever reset the
            // clock/save-age text -- Workspace.ActiveWorldIdText/Dashboard.WorldNicknameText/Dashboard.WorldText/
            // Workspace.WorldPlayerSaveCountText/Workspace.WorldSizeText/Dashboard.WorldPulseText kept whatever the
            // previously active tab's last successful poll had set, since a genuinely fresh server
            // (world.Available == false, e.g. still starting and hasn't generated Level.sav yet)
            // never got a chance to overwrite them with its own -- or lack of -- data.
            Workspace.ActiveWorldIdText = "Not resolved";
            Dashboard.WorldNicknameText = string.Empty;
            Dashboard.WorldText = "Unavailable";
            Workspace.WorldPlayerSaveCountText = "0";
            Workspace.WorldSizeText = "0.00 MB";
            Dashboard.WorldPulseText = "World telemetry unavailable";
            Dashboard.WorldClockText = "Day — • --:--";
            Dashboard.WorldClockDetailText = "World telemetry unavailable";
            Dashboard.PulseSaveText = "World save: —";
        }

        var latestBackup = snapshot.BackupInventory.Items.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        Dashboard.BackupText = snapshot.BackupInventory.Count == 0 ? "No backups" : $"{snapshot.BackupInventory.Count} available";
        Dashboard.BackupStripText = snapshot.BackupInventory.Count == 0 ? "Backup: none" : $"Backup: {snapshot.BackupInventory.Count} available";
        Backups.BackupTotalSizeText = $"{snapshot.BackupInventory.TotalSizeBytes / 1024d / 1024d:F2} MB";
        Dashboard.PulseBackupText = latestBackup is null
            ? "Backup: none detected"
            : $"Backup: {FormatAge(DateTimeOffset.Now - latestBackup.CreatedAt.ToLocalTime())} ago{(latestBackup.Verified ? " · Verified" : string.Empty)}";

        var serverName = snapshot.PalworldSettings.Settings.FirstOrDefault(x => x.Name.Equals("ServerName", StringComparison.OrdinalIgnoreCase))?.Value;
        var description = snapshot.PalworldSettings.Settings.FirstOrDefault(x => x.Name.Equals("ServerDescription", StringComparison.OrdinalIgnoreCase))?.Value;
        Dashboard.ServerName = string.IsNullOrWhiteSpace(serverName) ? "—" : serverName;
        Dashboard.ServerDescriptionVerbatim = string.IsNullOrWhiteSpace(description) ? "—" : description;
        var rconEnabled = snapshot.PalworldSettings.Settings.FirstOrDefault(x => x.Name.Equals("RCONEnabled", StringComparison.OrdinalIgnoreCase))?.Value;
        var rconPort = snapshot.PalworldSettings.Settings.FirstOrDefault(x => x.Name.Equals("RCONPort", StringComparison.OrdinalIgnoreCase))?.Value?.Trim('"');
        Dashboard.RconText = string.Equals(rconEnabled?.Trim('"'), "True", StringComparison.OrdinalIgnoreCase)
            ? $"RCON: Enabled{(string.IsNullOrWhiteSpace(rconPort) ? string.Empty : $" : {rconPort}")}"
            : "RCON: Disabled";
    }

    private static string BuildWorldNickname(string? worldId) =>
        string.IsNullOrWhiteSpace(worldId) ? "World —" : $"World {worldId[..Math.Min(8, worldId.Length)]}";

    private void ApplyMetrics(RuntimeMetricsSnapshotDto snapshot)
    {
        // v0.7.9.0: server FPS/frame time come from the Palworld REST API directly, independent of
        // whether the managed PalServer process was found by lifecycle discovery -- set from both
        // branches below, not just the "process found" one.
        ServerFpsText = snapshot.ServerFps.HasValue ? $"{snapshot.ServerFps.Value:F0}" : "—";
        ServerFrameTimeText = snapshot.ServerFrameTimeMs.HasValue ? $"{snapshot.ServerFrameTimeMs.Value:F1} ms" : "—";

        if (!snapshot.Available)
        {
            CpuText = "—";
            MemoryText = "—";
            ThreadCountText = "—";
            CpuPercentValue = 0;
            MemoryScaleValue = 0;
            return;
        }

        CpuText = snapshot.CpuPercent.HasValue ? $"{snapshot.CpuPercent.Value:F1}%" : "Baseline";
        CpuPercentValue = Math.Clamp(snapshot.CpuPercent ?? 0d, 0d, 100d);
        var memoryMb = snapshot.WorkingSetBytes / 1024d / 1024d;
        MemoryMbValue = memoryMb;
        var recentMemoryPeak = Math.Max(memoryMb, ResourceHistory.Count == 0 ? memoryMb : ResourceHistory.Max(x => x.MemoryMb));
        MemoryScaleValue = recentMemoryPeak <= 0 ? 0 : Math.Clamp(memoryMb / recentMemoryPeak * 100d, 0d, 100d);
        MemoryText = $"{memoryMb:F1} MB";
        ThreadCountText = snapshot.ThreadCount.ToString();

        MetricHistory.Add(new MetricHistoryPoint(
            snapshot.ObservedAt,
            snapshot.CpuPercent,
            memoryMb,
            snapshot.ThreadCount));

        while (MetricHistory.Count > 30)
            MetricHistory.RemoveAt(0);

        AppendLiveResourceHistory(snapshot, memoryMb);
    }

    private void AppendLiveResourceHistory(RuntimeMetricsSnapshotDto snapshot, double memoryMb)
    {
        if (!snapshot.Available || !snapshot.CpuPercent.HasValue) return;
        var cutoff = DateTimeOffset.UtcNow - SelectedHistoryRangeDuration();
        while (ResourceHistory.Count > 0 && ResourceHistory[0].ObservedAt < cutoff)
            ResourceHistory.RemoveAt(0);

        if (ResourceHistory.Count == 0 || Math.Abs((snapshot.ObservedAt - ResourceHistory[^1].ObservedAt).TotalSeconds) >= 2)
        {
            ResourceHistory.Add(new HistoricalMetricPointDto
            {
                ObservedAt = snapshot.ObservedAt,
                CpuPercent = Math.Clamp(snapshot.CpuPercent.Value, 0, 100),
                MemoryMb = Math.Max(0, memoryMb),
                ServerFps = snapshot.ServerFps,
                ServerFrameTimeMs = snapshot.ServerFrameTimeMs
            });
        }
        while (ResourceHistory.Count > 1200) ResourceHistory.RemoveAt(0);
        UpdateHistorySummaries();
    }

    private TimeSpan SelectedHistoryRangeDuration() => SelectedHistoryRange switch
    {
        "6 Hours" => TimeSpan.FromHours(6),
        "24 Hours" => TimeSpan.FromHours(24),
        "7 Days" => TimeSpan.FromDays(7),
        "30 Days" => TimeSpan.FromDays(30),
        _ => TimeSpan.FromHours(1)
    };

    private async Task RefreshHistoricalMetricsAsync(bool force = false)
    {
        if (!ManagementApiConnected && ConnectionState != "Connected") return;
        if (!force && DateTimeOffset.UtcNow - _lastHistoryRefresh < TimeSpan.FromSeconds(2)) return;

        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch { return; }

        try
        {
            HistoryStatusText = "Loading…";
            var hours = SelectedHistoryRangeDuration().TotalHours;
            var snapshot = await _api.GetHistoricalMetricsAsync(profile, hours, 600, BearerToken);
            ResourceHistory.Clear();
            foreach (var sample in snapshot.Samples) ResourceHistory.Add(sample);
            _lastHistoryRefresh = DateTimeOffset.UtcNow;
            UpdateHistorySummaries(snapshot.CpuTrend, snapshot.MemoryTrend, snapshot.AverageFps, snapshot.PeakFps);
            HistoryStatusText = ResourceHistory.Count == 0 ? "Collecting" : $"Updated {snapshot.ObservedAt.ToLocalTime():HH:mm:ss}";
        }
        catch (Exception ex)
        {
            HistoryStatusText = $"History unavailable: {ex.Message}";
        }
    }

    private void UpdateHistorySummaries(string? cpuTrend = null, string? memoryTrend = null, double? averageFps = null, double? peakFps = null)
    {
        if (ResourceHistory.Count == 0)
        {
            HistoryCpuSummary = "CPU history collecting…";
            HistoryMemorySummary = "Memory history collecting…";
            HistoryFpsSummary = "FPS history collecting…";
            HistorySampleSummary = "0 samples";
            return;
        }

        var averageCpu = ResourceHistory.Average(x => x.CpuPercent);
        var peakCpu = ResourceHistory.Max(x => x.CpuPercent);
        var averageMemory = ResourceHistory.Average(x => x.MemoryMb);
        var peakMemory = ResourceHistory.Max(x => x.MemoryMb);
        HistoryCpuSummary = $"{cpuTrend ?? Trend(ResourceHistory.Select(x => x.CpuPercent))} Avg {averageCpu:F1}% · Peak {peakCpu:F1}%";
        HistoryMemorySummary = $"{memoryTrend ?? Trend(ResourceHistory.Select(x => x.MemoryMb))} Avg {FormatMemory(averageMemory)} · Peak {FormatMemory(peakMemory)}";
        // v0.7.15.0: real in-game FPS history -- "Not recorded" (not "0 FPS") whenever the Palworld
        // REST API was disabled for the entire selected range, matching ServerFps's null-means-
        // unavailable convention everywhere else this data appears.
        var fpsSamples = ResourceHistory.Where(x => x.ServerFps.HasValue).Select(x => x.ServerFps!.Value).ToArray();
        HistoryFpsSummary = fpsSamples.Length == 0
            ? "FPS: not recorded (REST API disabled for this range)"
            : $"FPS Avg {(averageFps ?? fpsSamples.Average()):F1} · Peak {(peakFps ?? fpsSamples.Max()):F1}";
        HistorySampleSummary = $"{ResourceHistory.Count} sample(s) in selected range";
    }

    private static string Trend(IEnumerable<double> values)
    {
        var data = values.ToArray();
        if (data.Length < 2) return "→";
        var count = Math.Max(1, data.Length / 4);
        var first = data.Take(count).Average();
        var last = data.TakeLast(count).Average();
        var tolerance = Math.Max(0.01, Math.Abs(first) * 0.03);
        return last > first + tolerance ? "↑" : last < first - tolerance ? "↓" : "→";
    }

    private static string FormatMemory(double mb) => mb >= 1024 ? $"{mb / 1024d:F2} GB" : $"{mb:F0} MB";


    private async Task RefreshActivityAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { ActivityDetail = ex.Message; return; }
        IsBusy = true;
        ActivityState = "Loading…";
        try
        {
            var snapshot = await _api.GetActivityLogTailAsync(profile, 250, BearerToken);
            _allActivityLines.Clear();
            _allActivityLines.AddRange(snapshot.Lines);
            ApplyActivityFilters();
            ActivityFileText = snapshot.FileName;
            if (!snapshot.Available) ActivityState = "Unavailable";
            ActivityDetail = snapshot.Detail;
        }
        catch (Exception ex)
        {
            ActivityState = "Unavailable";
            ActivityDetail = ex.Message;
        }
        finally { IsBusy = false; }
    }

    private void ApplyActivityFilters()
    {
        var query = ActivitySearchText.Trim();
        ActivityLines.Clear();
        foreach (var line in _allActivityLines.Where(line =>
                     (string.IsNullOrWhiteSpace(query) || line.Contains(query, StringComparison.OrdinalIgnoreCase)) &&
                     (ActivitySeverityFilter == "All" || line.Contains($"[{ActivitySeverityFilter}]", StringComparison.OrdinalIgnoreCase) ||
                      (ActivitySeverityFilter == "Error" && line.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase))) &&
                     (ActivityCategoryFilter == "All" || line.Contains($"[{ActivityCategoryFilter}]", StringComparison.OrdinalIgnoreCase))))
            ActivityLines.Add(line);
        ActivityState = $"{ActivityLines.Count} visible / {_allActivityLines.Count} event(s)";
    }

    private async Task RefreshNotificationsAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try { ApplyNotificationSnapshot(await _api.GetNotificationsAsync(SelectedProfile, BearerToken)); }
        catch (Exception ex) { NotificationState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RunNotificationSelfTestAsync() => await MutateNotificationsAsync(() => _api.RunNotificationSelfTestAsync(SelectedProfile!, BearerToken));
    private async Task MarkAllNotificationsReadAsync() => await MutateNotificationsAsync(() => _api.MarkAllNotificationsReadAsync(SelectedProfile!, BearerToken));
    private async Task ToggleNotificationReadAsync() { if (SelectedNotification is { } item) await MutateNotificationsAsync(() => _api.SetNotificationReadAsync(SelectedProfile!, item.Id, !item.Read, BearerToken)); }
    private async Task ToggleNotificationPinAsync() { if (SelectedNotification is { } item) await MutateNotificationsAsync(() => _api.SetNotificationPinnedAsync(SelectedProfile!, item.Id, !item.Pinned, BearerToken)); }
    private async Task DismissNotificationAsync() { if (SelectedNotification is { } item) await MutateNotificationsAsync(() => _api.DismissNotificationAsync(SelectedProfile!, item.Id, BearerToken)); }

    private async Task MutateNotificationsAsync(Func<Task<NotificationSnapshotDto>> operation)
    {
        if (SelectedProfile is null) return; IsBusy = true;
        try { ApplyNotificationSnapshot(await operation()); }
        catch (Exception ex) { NotificationState = ex.Message; }
        finally { IsBusy = false; }
    }

    private void ApplyNotificationSnapshot(NotificationSnapshotDto snapshot)
    {
        var selectedId = SelectedNotification?.Id;
        Notifications.Clear(); foreach (var item in snapshot.Items) Notifications.Add(item);
        ApplyNotificationFilters();
        SelectedNotification = FilteredNotifications.FirstOrDefault(x => x.Id == selectedId) ?? FilteredNotifications.FirstOrDefault();
        NotificationState = snapshot.Detail;
        RaisePropertyChanged(nameof(NotificationBadgeText));
    }

    private void ApplyNotificationFilters()
    {
        var query = NotificationSearchText.Trim(); var selectedId = SelectedNotification?.Id;
        FilteredNotifications.Clear();
        foreach (var item in Notifications.Where(x =>
                     (NotificationSeverityFilter == "All" || x.Severity.Equals(NotificationSeverityFilter, StringComparison.OrdinalIgnoreCase)) &&
                     (string.IsNullOrWhiteSpace(query) || x.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || x.Message.Contains(query, StringComparison.OrdinalIgnoreCase))))
            FilteredNotifications.Add(item);
        SelectedNotification = FilteredNotifications.FirstOrDefault(x => x.Id == selectedId) ?? FilteredNotifications.FirstOrDefault();
    }

    public string ExportVisibleActivity() => string.Join(Environment.NewLine, ActivityLines);
    public string ExportVisibleNotifications() => string.Join(Environment.NewLine, FilteredNotifications.Select(x => $"[{x.CreatedUtc:O}] [{x.Severity}] [{(x.Read ? "READ" : "UNREAD")}] [{(x.Pinned ? "PINNED" : "UNPINNED")}] {x.Title} — {x.Message}"));

    private async Task RefreshAutomationAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var rules = await _api.GetAutomationRulesAsync(SelectedProfile, BearerToken);
            var selectedId = SelectedAutomationRule?.Id;
            AutomationRules.Clear(); foreach (var rule in rules) AutomationRules.Add(rule);
            SelectedAutomationRule = AutomationRules.FirstOrDefault(x => x.Id == selectedId) ?? AutomationRules.FirstOrDefault();

            var runs = await _api.GetAutomationRunsAsync(SelectedProfile, 100, BearerToken);
            AutomationRuns.Clear(); foreach (var run in runs) AutomationRuns.Add(run);
            AutomationState = $"{AutomationRules.Count} rule(s), {AutomationRuns.Count} recent run(s).";
        }
        catch (Exception ex) { AutomationState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task CreateAutomationRuleAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var request = BuildAutomationRuleRequest();
            var rule = await _api.CreateAutomationRuleAsync(SelectedProfile, request, BearerToken);
            AutomationRules.Add(rule);
            SelectedAutomationRule = rule;
            NewAutomationRuleName = string.Empty;
            AutomationState = $"Created rule '{rule.Name}'.";
        }
        catch (Exception ex) { AutomationState = ex.Message; }
        finally { IsBusy = false; }
    }

    private AutomationRuleRequestDto BuildAutomationRuleRequest()
    {
        var trigger = new AutomationTriggerDto { Kind = NewAutomationTriggerKind };
        if (NewAutomationTriggerKind == "Interval")
            trigger.Interval = TimeSpan.FromMinutes(Math.Max(1, NewAutomationIntervalMinutes));
        else if (NewAutomationTriggerKind == "IdleEmpty")
            trigger.IdleThresholdMinutes = Math.Max(1, NewAutomationIdleThresholdMinutes);
        else
            trigger.TimeOfDayUtc = TimeOnly.TryParse(NewAutomationTimeOfDayUtc, out var time) ? time : new TimeOnly(3, 0);

        var action = new AutomationActionDto { Kind = NewAutomationActionKind };
        if (NewAutomationActionKind == "SendNotification")
        {
            action.NotificationTitle = string.IsNullOrWhiteSpace(NewAutomationNotificationTitle) ? NewAutomationRuleName : NewAutomationNotificationTitle;
            action.NotificationMessage = NewAutomationNotificationMessage;
        }
        else if (NewAutomationActionKind == "SendRconCommand")
        {
            action.RconCommand = NewAutomationRconCommand;
        }

        return new AutomationRuleRequestDto(NewAutomationRuleName, trigger, new AutomationConditionDto(), action);
    }

    private async Task DeleteAutomationRuleAsync()
    {
        if (SelectedProfile is null || SelectedAutomationRule is not { } rule) return;
        IsBusy = true;
        try
        {
            await _api.DeleteAutomationRuleAsync(SelectedProfile, rule.Id, BearerToken);
            AutomationRules.Remove(rule);
            SelectedAutomationRule = AutomationRules.FirstOrDefault();
            AutomationState = $"Deleted rule '{rule.Name}'.";
        }
        catch (Exception ex) { AutomationState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task ToggleAutomationRuleEnabledAsync()
    {
        if (SelectedProfile is null || SelectedAutomationRule is not { } rule) return;
        IsBusy = true;
        try
        {
            var updated = await _api.SetAutomationRuleEnabledAsync(SelectedProfile, rule.Id, !rule.Enabled, BearerToken);
            var index = AutomationRules.IndexOf(rule);
            if (index >= 0) AutomationRules[index] = updated;
            SelectedAutomationRule = updated;
            AutomationState = $"Rule '{updated.Name}' is now {(updated.Enabled ? "enabled" : "disabled")}.";
        }
        catch (Exception ex) { AutomationState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RunAutomationRuleNowAsync()
    {
        if (SelectedProfile is null || SelectedAutomationRule is not { } rule) return;
        IsBusy = true;
        try
        {
            await _api.RunAutomationRuleNowAsync(SelectedProfile, rule.Id, BearerToken);
            AutomationState = $"Rule '{rule.Name}' dispatched. Refresh runs shortly to see the result.";
        }
        catch (Exception ex) { AutomationState = ex.Message; }
        finally { IsBusy = false; }
    }

    // ---- v0.7.114.0: named user accounts --------------------------------------------------------------------
    // Signing in trades a username and password for a session token, which then goes in the tab's Bearer token
    // exactly like a pasted token (and is remembered, encrypted, the same way). The password is never stored.
    private string _signInUsername = string.Empty;
    private string _signInPassword = string.Empty;
    private string _signInStatusText = string.Empty;
    private string _newUserUsername = string.Empty;
    private string _newUserDisplayName = string.Empty;
    private string _newUserPassword = string.Empty;
    private string _newUserRole = "Operator";
    private string _newUserScope = string.Empty;
    private string _userResetPassword = string.Empty;
    private string _userAccountsState = string.Empty;
    private UserAccountDto? _selectedUserAccount;
    public string SignInUsername { get => _signInUsername; set => SetField(ref _signInUsername, value ?? string.Empty); }
    public string SignInPassword { get => _signInPassword; set => SetField(ref _signInPassword, value ?? string.Empty); }
    public string SignInStatusText { get => _signInStatusText; set => SetField(ref _signInStatusText, value ?? string.Empty); }
    public string SignedInText => CurrentPrincipal is { } p
        ? $"Signed in as {p.Name} ({p.Role}){(p.ExpiresUtc is { } until ? $" until {until.ToLocalTime():g}" : string.Empty)}."
        : "Not signed in with a named account (a pasted token or the local sidecar decides what you can do).";
    public string NewUserUsername { get => _newUserUsername; set => SetField(ref _newUserUsername, value ?? string.Empty); }
    public string NewUserDisplayName { get => _newUserDisplayName; set => SetField(ref _newUserDisplayName, value ?? string.Empty); }
    public string NewUserPassword { get => _newUserPassword; set => SetField(ref _newUserPassword, value ?? string.Empty); }
    public string NewUserRole { get => _newUserRole; set => SetField(ref _newUserRole, value ?? "Operator"); }
    public string NewUserScope { get => _newUserScope; set => SetField(ref _newUserScope, value ?? string.Empty); }
    public string UserResetPassword { get => _userResetPassword; set => SetField(ref _userResetPassword, value ?? string.Empty); }
    public string UserAccountsState { get => _userAccountsState; set => SetField(ref _userAccountsState, value ?? string.Empty); }
    public UserAccountDto? SelectedUserAccount { get => _selectedUserAccount; set => SetField(ref _selectedUserAccount, value); }
    public ObservableCollection<UserAccountDto> UserAccounts { get; } = [];
    public ICommand SignInCommand { get; private set; } = null!;
    public ICommand SignOutCommand { get; private set; } = null!;
    public ICommand CreateUserCommand { get; private set; } = null!;
    public ICommand ResetUserPasswordCommand { get; private set; } = null!;
    public ICommand ToggleUserEnabledCommand { get; private set; } = null!;
    public ICommand DeleteUserCommand { get; private set; } = null!;

    private void InitializeUserAccountCommands()
    {
        SignInCommand = new AsyncCommand(SignInAsync, () => !IsBusy);
        SignOutCommand = new AsyncCommand(SignOutAsync, () => !IsBusy);
        CreateUserCommand = new AsyncCommand(CreateUserAsync, () => !IsBusy);
        ResetUserPasswordCommand = new AsyncCommand(ResetUserPasswordAsync, () => !IsBusy);
        ToggleUserEnabledCommand = new AsyncCommand(ToggleUserEnabledAsync, () => !IsBusy);
        DeleteUserCommand = new AsyncCommand(DeleteUserAsync, () => !IsBusy);
    }

    private void ApplyCurrentPrincipal(MystTiqPrincipalDto? principal)
    {
        CurrentPrincipal = principal;
        RaisePropertyChanged(nameof(CanOperate));
        RaisePropertyChanged(nameof(CanManageAdmin));
        RaisePropertyChanged(nameof(CanManagePrincipals));
        RaisePropertyChanged(nameof(RoleHiddenNotice));
        RaisePropertyChanged(nameof(HasRoleHiddenNotice));
        RaisePropertyChanged(nameof(SignedInText));
        // v0.8.19.0: the Ribbon follows the role too.
        RebuildRibbonGroups();
        // v0.8.23.0: and so does every command, wherever its button is.
        RaiseCommandRoleGatesChanged();
    }

    private async Task SignInAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { SignInStatusText = ex.Message; return; }
        if (string.IsNullOrWhiteSpace(SignInUsername) || string.IsNullOrEmpty(SignInPassword)) { SignInStatusText = "Enter a username and password."; return; }
        IsBusy = true;
        try
        {
            var result = await _api.LoginAsync(profile, SignInUsername.Trim(), SignInPassword);
            SignInPassword = string.Empty;
            if (!result.Success || string.IsNullOrEmpty(result.Token)) { SignInStatusText = result.Message; return; }
            BearerToken = result.Token;
            SignInStatusText = result.Message;
        }
        catch (Exception ex) { SignInStatusText = ex.Message; return; }
        finally { IsBusy = false; }
        // Connect with the new token: this is what saves it (encrypted) and loads who is signed in.
        await RefreshAsync(silent: false);
    }

    private async Task SignOutAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { SignInStatusText = ex.Message; return; }
        IsBusy = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(BearerToken)) await _api.LogoutAsync(profile, BearerToken);
            _credentialStore.Delete(profile.Id);
            BearerToken = string.Empty;
            ApplyCurrentPrincipal(null);
            ManagementApiConnected = false;
            ConnectionState = "Signed out";
            SignInStatusText = "Signed out. The session token was ended on the server and forgotten here.";
        }
        catch (Exception ex) { SignInStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RefreshUserAccountsAsync()
    {
        if (SelectedProfile is null || !CanManagePrincipals) { UserAccounts.Clear(); return; }
        var selectedId = SelectedUserAccount?.Id;
        var users = await _api.GetUsersAsync(SelectedProfile, BearerToken);
        UserAccounts.Clear();
        foreach (var user in users) UserAccounts.Add(user);
        SelectedUserAccount = UserAccounts.FirstOrDefault(u => u.Id == selectedId);
        UserAccountsState = $"{UserAccounts.Count} user account(s).";
    }

    private async Task RunUserAccountActionAsync(Func<Task<UserAccountResultDto>> action)
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var result = await action();
            UserAccountsState = result.Message;
            if (result.Success) await RefreshUserAccountsAsync();
            if (result.Success) UserAccountsState = result.Message;
        }
        catch (Exception ex) { UserAccountsState = ex.Message; }
        finally { IsBusy = false; }
    }

    private Task CreateUserAsync() => RunUserAccountActionAsync(async () =>
    {
        var result = await _api.CreateUserAsync(SelectedProfile!, NewUserUsername.Trim(), NewUserDisplayName.Trim(), NewUserRole, NewUserPassword,
            string.IsNullOrWhiteSpace(NewUserScope) ? null : NewUserScope.Trim(), BearerToken);
        if (result.Success) { NewUserUsername = string.Empty; NewUserDisplayName = string.Empty; NewUserPassword = string.Empty; }
        return result;
    });

    private Task ResetUserPasswordAsync()
    {
        if (SelectedUserAccount is not { } user) { UserAccountsState = "Select an account first."; return Task.CompletedTask; }
        return RunUserAccountActionAsync(async () =>
        {
            var result = await _api.SetUserPasswordAsync(SelectedProfile!, user.Id, UserResetPassword, BearerToken);
            if (result.Success) UserResetPassword = string.Empty;
            return result;
        });
    }

    private Task ToggleUserEnabledAsync()
    {
        if (SelectedUserAccount is not { } user) { UserAccountsState = "Select an account first."; return Task.CompletedTask; }
        return RunUserAccountActionAsync(() => _api.UpdateUserAsync(SelectedProfile!, user.Id, user.DisplayName, user.Role, user.ScopedServerProfileId, !user.Enabled, BearerToken));
    }

    private Task DeleteUserAsync()
    {
        if (SelectedUserAccount is not { } user) { UserAccountsState = "Select an account first."; return Task.CompletedTask; }
        return RunUserAccountActionAsync(() => _api.DeleteUserAsync(SelectedProfile!, user.Id, BearerToken));
    }

    private async Task RefreshSecurityAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var who = await _api.WhoAmIAsync(SelectedProfile, BearerToken);
            ApplyCurrentPrincipal(who);
            await RefreshUserAccountsAsync();

            var principals = await _api.GetPrincipalsAsync(SelectedProfile, BearerToken);
            var selectedId = SelectedPrincipal?.Id;
            Principals.Clear(); foreach (var principal in principals) Principals.Add(principal);
            SelectedPrincipal = Principals.FirstOrDefault(x => x.Id == selectedId);
            SecurityState = $"Signed in as {who.Name} ({who.Role}). {Principals.Count} principal(s) issued.";
        }
        catch (Exception ex) { SecurityState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task CreatePrincipalAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var result = await _api.CreatePrincipalAsync(SelectedProfile, new HeadlessCreatePrincipalRequestDto(NewPrincipalName, NewPrincipalRole, null), BearerToken);
            Principals.Add(result.Principal);
            LastCreatedPrincipalToken = result.PlaintextToken;
            NewPrincipalName = string.Empty;
            SecurityState = $"Created principal '{result.Principal.Name}'. Copy the token now — it will not be shown again.";
        }
        catch (Exception ex) { SecurityState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RevokePrincipalAsync()
    {
        if (SelectedProfile is null || SelectedPrincipal is not { } principal) return;
        IsBusy = true;
        try
        {
            await _api.RevokePrincipalAsync(SelectedProfile, principal.Id, BearerToken);
            Principals.Remove(principal);
            SelectedPrincipal = null;
            SecurityState = $"Revoked principal '{principal.Name}'.";
        }
        catch (Exception ex) { SecurityState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RefreshAlertCenterAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            AlertRules = await _api.GetAlertRulesAsync(SelectedProfile, BearerToken);
            DiskSpacePrediction = await _api.GetDiskSpacePredictionAsync(SelectedProfile, BearerToken);
            NotificationDelivery = await _api.GetNotificationDeliveryAsync(SelectedProfile, BearerToken);
            await RefreshDeliveryHealthAsync(SelectedProfile, null, force: true);
            AlertCenterState = "Alert rules and disk-space prediction loaded.";
        }
        catch (Exception ex) { AlertCenterState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task SaveAlertRulesAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            AlertRules = await _api.SaveAlertRulesAsync(SelectedProfile, AlertRules, BearerToken);
            AlertCenterState = "Alert rules saved.";
        }
        catch (Exception ex) { AlertCenterState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task MuteAlertsAsync(int minutes)
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            AlertRules = await _api.MuteAlertsAsync(SelectedProfile, minutes, BearerToken);
            AlertCenterState = minutes <= 0 ? "Alerts unmuted." : AlertRules.MuteStatusText;
        }
        catch (Exception ex) { AlertCenterState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task PauseDeliveryAsync(int minutes)
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            NotificationDelivery = await _api.PauseNotificationDeliveryAsync(SelectedProfile, minutes, BearerToken);
            AlertCenterState = minutes <= 0 ? "Outside delivery resumed." : "Outside delivery paused: " + NotificationDelivery.StatusText;
        }
        catch (Exception ex) { AlertCenterState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task SendTestNotificationAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var result = await _api.SendTestNotificationAsync(SelectedProfile, BearerToken);
            NotificationDelivery = result.Delivery;
            AlertCenterState = result.Message;
        }
        catch (Exception ex) { AlertCenterState = ex.Message; }
        finally { IsBusy = false; }
        // v1.0.2.0 (roadmap R-2): the sends run in the background (each tries twice, 10 s apiece); show their results.
        if (SelectedProfile is { } profile)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            await RefreshDeliveryHealthAsync(profile, null, force: true);
        }
    }

    private async Task RefreshDiscordBotConfigAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var view = await _api.GetDiscordBotConfigAsync(SelectedProfile, BearerToken);
            DiscordBotConfig = new DiscordBotConfigurationDto
            {
                Enabled = view.Enabled,
                GuildId = view.GuildId,
                OwnerDiscordUserId = view.OwnerDiscordUserId,
                RoleMappings = view.RoleMappings,
                StatusChannelId = view.StatusChannelId,
                EventsChannelId = view.EventsChannelId,
                ShowPresence = view.ShowPresence
            };
            DiscordBotTokenConfigured = view.TokenConfigured;
            DiscordBotConnectionState = view.ConnectionState;
            DiscordRoleMappings.Clear();
            foreach (var mapping in view.RoleMappings) DiscordRoleMappings.Add(mapping);
            DiscordBotState = $"Discord bot configuration loaded. Connection: {view.ConnectionState}.";
        }
        catch (Exception ex) { DiscordBotState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task SaveDiscordBotConfigAsync()
    {
        if (SelectedProfile is null) return;
        // v0.7.95.0: catch a mistyped channel id here with a plain reason instead of sending it.
        foreach (var (label, value) in new[] { ("Status channel", DiscordBotConfig.StatusChannelId), ("Events channel", DiscordBotConfig.EventsChannelId) })
        {
            if (!string.IsNullOrWhiteSpace(value) && !MystTiq.Core.Models.DiscordSnowflake.TryParse(value, out _))
            {
                DiscordBotState = $"{label} ID must be the 17-20 digit number Discord shows (enable Developer Mode, right-click the channel, Copy Channel ID), or leave it blank to turn that feature off.";
                return;
            }
        }

        IsBusy = true;
        try
        {
            DiscordBotConfig.RoleMappings = [.. DiscordRoleMappings];
            var view = await _api.SaveDiscordBotConfigAsync(SelectedProfile, DiscordBotConfig, BearerToken);
            DiscordBotTokenConfigured = view.TokenConfigured;
            DiscordBotConnectionState = view.ConnectionState;
            DiscordBotConfig.BotToken = null;
            DiscordBotState = "Discord bot configuration saved.";
        }
        catch (Exception ex) { DiscordBotState = ex.Message; }
        finally { IsBusy = false; }
    }

    private void AddDiscordRoleMapping()
    {
        if (string.IsNullOrWhiteSpace(NewRoleMappingDiscordRoleId)) return;
        DiscordRoleMappings.Add(new DiscordRoleMappingDto { DiscordRoleId = NewRoleMappingDiscordRoleId.Trim(), Role = NewRoleMappingRole });
        NewRoleMappingDiscordRoleId = string.Empty;
    }

    private void RemoveDiscordRoleMapping()
    {
        if (SelectedRoleMapping is null) return;
        DiscordRoleMappings.Remove(SelectedRoleMapping);
        SelectedRoleMapping = null;
    }

    private async Task RefreshAntiCheatAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            AntiCheatRules = await _api.GetAntiCheatRulesAsync(SelectedProfile, BearerToken);
            var findings = await _api.GetAntiCheatFindingsAsync(SelectedProfile, BearerToken);
            AntiCheatFindings.Clear();
            foreach (var finding in findings) AntiCheatFindings.Add(finding);
            AntiCheatState = $"Anti-cheat rules loaded. {findings.Count} recent finding(s).";
        }
        catch (Exception ex) { AntiCheatState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task SaveAntiCheatRulesAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            AntiCheatRules = await _api.SaveAntiCheatRulesAsync(SelectedProfile, AntiCheatRules, BearerToken);
            AntiCheatState = "Anti-cheat rules saved.";
        }
        catch (Exception ex) { AntiCheatState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RefreshFleetAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var summaries = await _api.GetServerProfilesAsync(SelectedProfile, BearerToken);
            FleetServers.Clear();
            foreach (var summary in summaries) FleetServers.Add(summary);
            FleetState = $"{FleetServers.Count} server profile(s) configured.";
        }
        catch (Exception ex) { FleetState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task CloneWorldAsync()
    {
        if (SelectedProfile is null || string.IsNullOrWhiteSpace(CloneNewProfileId) || !CloneWorldConfirmed) return;
        IsBusy = true;
        CloneWorldStatusText = "Cloning… this copies the entire server installation and can take a while.";
        CloneWorldNeedsRestart = false;
        try
        {
            var newProfileId = CloneNewProfileId.Trim();
            var request = new WorldCloneRequestDto
            {
                NewProfileId = newProfileId,
                NewProfileName = string.IsNullOrWhiteSpace(CloneNewProfileName) ? null : CloneNewProfileName.Trim(),
                PortOffset = CloneWorldPortOffset
            };
            var result = await _api.CloneWorldAsync(SelectedProfile, request, BearerToken);
            CloneWorldStatusText = result.Message;
            if (result.Success)
            {
                // Reset the workflow back to a fresh state rather than leaving a checked
                // confirmation box sitting ready to re-fire against stale profile ID/name text.
                CloneWorldConfirmed = false;
                await RefreshFleetAsync();
                CloneWorldNewProfileId = newProfileId;
                CloneWorldNeedsRestart = true;
            }
        }
        catch (Exception ex) { CloneWorldStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    // v0.7.84.0: explicit, user-triggered restart offered after a successful Clone World here --
    // reuses the exact same RestartOwnedSidecarAsync built for the wizard's own second-server flow
    // in v0.7.83.0. Deliberately does NOT touch this tab's own TargetServerId/reconnect the way the
    // wizard does (this tab should stay on its own already-established profile); it only brings the
    // local service back up with the new profile now visible, matching what Fleet's own profile list
    // already needed a restart+refresh to show.
    private async Task RestartAfterCloneAsync()
    {
        if (string.IsNullOrWhiteSpace(CloneWorldNewProfileId)) return;
        IsBusy = true;
        CloneWorldStatusText = "Restarting the local MystTiq service to bring the new server online…";
        try
        {
            var snapshot = await _localDiscovery.DiscoverAsync();
            if (snapshot.ServiceInstalled)
            {
                CloneWorldStatusText = "This machine runs MystTiq as an installed Windows Service, which the desktop app can't restart on its own -- restart it manually, then refresh this page.";
                return;
            }
            var restartResult = await _localBootstrapper.RestartOwnedSidecarAsync(snapshot, CloneWorldNewProfileId);
            CloneWorldStatusText = restartResult.Available
                ? $"Restarted. '{CloneWorldNewProfileId}' is now online."
                : $"Automatic restart failed: {restartResult.Detail}";
            if (restartResult.Available)
            {
                CloneWorldNeedsRestart = false;
                await RefreshFleetAsync();
            }
        }
        catch (Exception ex) { CloneWorldStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    // v0.7.81.0: "Clone an Existing Server" step of the new-server install wizard. Distinct from
    // CloneWorldAsync above (Fleet's own Clone World card), which always clones FROM the currently
    // connected tab's own profile. Here the source is a DIFFERENT, already-saved profile (never
    // SelectedProfile -- that's this brand-new, not-yet-saved profile being created) and the target
    // is this wizard's in-progress profile. This works without touching MystTiqApiClient at all:
    // CloneWorldAsync already takes an explicit source ConnectionProfile parameter rather than
    // always using SelectedProfile, and CredentialStore.TryLoad(profileId) already resolves a
    // bearer token for any saved profile by Id, independent of tabs -- no new backend needed.
    private async Task CloneIntoNewServerAsync()
    {
        if (NewServerCloneSourceProfile is not { } source || string.IsNullOrWhiteSpace(NewServerCloneTargetId)) return;
        IsBusy = true;
        NewServerCloneStatusText = "Cloning… this copies the entire server installation and can take a while.";
        try
        {
            var newProfileId = NewServerCloneTargetId.Trim();
            var request = new WorldCloneRequestDto
            {
                NewProfileId = newProfileId,
                NewProfileName = string.IsNullOrWhiteSpace(ProfileName) ? null : ProfileName.Trim(),
                PortOffset = CloneWorldPortOffset
            };
            var token = _credentialStore.TryLoad(source.Id);
            var result = await _api.CloneWorldAsync(source, request, token);
            NewServerCloneStatusText = result.Message;
            if (!result.Success) return;

            // v0.7.82.0: Clone already registers the new fleet profile server-side (AddServerAsync,
            // called internally by CloneAsync), but like any other fleet-membership change that only
            // takes effect after a MystTiq process restart -- previously left entirely undisclosed
            // and unhandled here, silently landing on Confirm against a profile that wasn't actually
            // reachable yet. Now automated the same way the Install Directory step's own
            // second-server path is (see ContinueFromInstallDirectoryAsync's comment for the full
            // Windows-Service-install caveat).
            NewServerCloneStatusText = "Cloned. Restarting the local MystTiq service to bring the new server online…";
            var snapshot = await _localDiscovery.DiscoverAsync();
            if (snapshot.ServiceInstalled)
            {
                NewServerCloneStatusText = $"{result.Message} This machine runs MystTiq as an installed Windows Service, which the desktop app can't restart on its own -- restart it manually, then connect to '{newProfileId}' from the \"+\" menu.";
                return;
            }
            var restartResult = await _localBootstrapper.RestartOwnedSidecarAsync(snapshot, newProfileId);
            if (!restartResult.Available)
            {
                NewServerCloneStatusText = $"{result.Message} Automatic restart failed: {restartResult.Detail}";
                return;
            }
            ServerUrl = restartResult.Endpoint;
            TargetServerId = newProfileId;
            await RefreshAsync();
            NewServerCloneStatusText = ManagementApiConnected
                ? $"Cloned and connected to the new profile '{newProfileId}'."
                : $"{result.Message} Restarted, but reconnecting to the new profile failed. Try again.";
            if (ManagementApiConnected)
                NewServerWizardStep = 8;
        }
        catch (Exception ex) { NewServerCloneStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RunFleetActionAsync(string label, Func<ConnectionProfile, string?, Task<IReadOnlyList<FleetActionResultDto>>> action)
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var results = await action(SelectedProfile, BearerToken);
            FleetActionResults.Clear();
            foreach (var result in results) FleetActionResults.Add(result);
            var failures = results.Count(r => !r.Success);
            FleetState = failures == 0
                ? $"{label}: completed on all {results.Count} server(s)."
                : $"{label}: {failures} of {results.Count} server(s) failed. See results below.";
            await RefreshFleetAsync();
        }
        catch (Exception ex) { FleetState = ex.Message; }
        finally { IsBusy = false; }
    }

    private Task BackupAllAsync() => RunFleetActionAsync("Backup All", (profile, token) => _api.BackupAllAsync(profile, token));
    private Task DoctorAllAsync() => RunFleetActionAsync("Doctor All", (profile, token) => _api.DoctorAllAsync(profile, token));
    private Task UpdateAllAsync() => RunFleetActionAsync("Update All", (profile, token) => _api.UpdateAllAsync(profile, token));

    private async Task AnalyzeCrashesAsync()
    {
        if (SelectedProfile is null) return; IsBusy = true; CrashAnalyzerState = "Analyzing bounded recent-log evidence…";
        try { ApplyCrashAnalysis(await _api.AnalyzeCrashesAsync(SelectedProfile, BearerToken)); await LoadCrashHistoryCoreAsync(); }
        catch (Exception ex) { CrashAnalyzerState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RefreshCrashHistoryAsync()
    {
        if (SelectedProfile is null) return; IsBusy = true;
        try { await LoadCrashHistoryCoreAsync(); }
        catch (Exception ex) { CrashAnalyzerState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task LoadCrashHistoryCoreAsync()
    {
        var history = await _api.GetCrashHistoryAsync(SelectedProfile!, BearerToken);
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


    public async Task<PlayerCopyPreviewDto?> PreviewCopyPlayerAsync(string sourcePlayerId, string destinationPlayerId)
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { Players.PlayerAdminStatusText = ex.Message; return null; }

        IsBusy = true;
        try
        {
            var preview = await _api.PreviewPlayerCopyAsync(profile, sourcePlayerId, destinationPlayerId, BearerToken);
            if (!preview.CanApply) Players.PlayerAdminStatusText = string.Join(" ", preview.Findings);
            return preview;
        }
        catch (Exception ex) { Players.PlayerAdminStatusText = ex.Message; return null; }
        finally { IsBusy = false; }
    }

    public async Task<bool> ApplyCopyPlayerAsync(string previewToken)
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { Players.PlayerAdminStatusText = ex.Message; return false; }

        IsBusy = true;
        try
        {
            var result = await _api.ApplyPlayerCopyAsync(profile, previewToken, true, BearerToken);
            Players.PlayerAdminStatusText = result.Message;
            await RefreshPlayersPageAsync();
            await RefreshActivityAsync();
            return result.Success;
        }
        catch (Exception ex) { Players.PlayerAdminStatusText = ex.Message; return false; }
        finally { IsBusy = false; }
    }








    private async Task SaveWorldNowAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { Players.PlayersPageDetail = ex.Message; return; }

        IsBusy = true;
        Players.PlayersPageDetail = "Saving world…";
        try
        {
            var result = await _api.SaveWorldNowAsync(profile, BearerToken);
            Players.PlayersPageDetail = result.Message;
        }
        catch (Exception ex) { Players.PlayersPageDetail = ex.Message; }
        finally { IsBusy = false; }
    }


    private async Task RunDoctorAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { DoctorSummary = ex.Message; return; }
        IsBusy = true; DoctorStatus = "Running…";
        try
        {
            var report = await _api.RunDoctorAsync(profile, BearerToken);
            DoctorChecks.Clear();
            foreach (var check in report.Checks) DoctorChecks.Add(check);
            DoctorCheckedAt = report.CheckedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
            (ExportDoctorCommand as RelayCommand)?.RaiseCanExecuteChanged();

            // v0.6.4.0: the unified report supersedes the raw /doctor-only status/summary text
            // (DoctorChecks above stays populated too, for the existing export format) -- this is
            // what makes the Doctor page and the Dashboard badge agree, since both now read from
            // the same HeadlessDiagnosticsService output.
            LatestDiagnosticsReport = await _api.GetDiagnosticsReportAsync(profile, BearerToken);
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
        finally { IsBusy = false; }
        // v0.7.44.0: Run Doctor is the natural moment to also refresh the machine-wide instance
        // list -- same page, same "what's actually running" concern. Best-effort: a failure here
        // must not blank out the diagnostics report that already succeeded above.
        await RefreshAllInstancesAsync();
    }

    // v0.7.44.0: Palworld Instance Detection & Termination Tool. Lists every Palworld process on
    // the machine (not just this profile's own managed one) via the new /server/instances route,
    // which wraps IServerLifecycleService.FindAllInstancesAsync -- itself just the existing raw
    // FindProcessesByName primitive, unfiltered by ServerRoot, tagged per-entry with whether it
    // matches this profile's own configured install path.
    private async Task RefreshAllInstancesAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch { return; }
        try
        {
            var instances = await _api.GetAllInstancesAsync(profile, BearerToken);
            AllInstances.Clear();
            foreach (var instance in instances) AllInstances.Add(instance);
            RaisePropertyChanged(nameof(HasAllInstances));
            if (SelectedInstance is { } current && instances.All(i => i.ProcessId != current.ProcessId))
                SelectedInstance = null;
        }
        catch { /* best-effort -- the Doctor page already surfaces connection problems elsewhere */ }
    }

    // Deliberately only reachable for an instance NOT confirmed as this profile's own managed
    // process (see SelectedInstance/IsSelectedInstanceManaged) -- this is a raw kill by PID with
    // no crash-recovery coordination of any kind. If the target actually belongs to a different
    // local MystTiq session's own tracked ServerRoot, that session's own crash-recovery will see
    // an unrequested stop and may auto-restart it; this tool cannot know that in advance and the
    // MOD DETAILS-style details panel discloses exactly this risk before the button is reachable.
    private async Task TerminateSelectedInstanceAsync()
    {
        if (SelectedInstance is not { ManagedByThisProfile: false } target) return;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { InstanceTerminationResultText = ex.Message; return; }
        IsBusy = true; BusyReason = $"Terminating PID {target.ProcessId}…";
        try
        {
            var result = await _api.TerminateInstanceAsync(profile, target.ProcessId, BearerToken);
            InstanceTerminationResultText = result.Message ?? string.Empty;
            await RefreshAllInstancesAsync();
        }
        catch (Exception ex) { InstanceTerminationResultText = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RecheckDiagnosticAsync(DiagnosticFindingDto? finding)
    {
        if (finding is null) return;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { DiagnosticsReportDetail = ex.Message; return; }

        IsBusy = true;
        try
        {
            var updated = await _api.RecheckDiagnosticFindingAsync(profile, finding.Id, BearerToken);
            if (updated is null) { DiagnosticsReportDetail = $"{finding.Component}: finding no longer exists."; return; }
            var index = DiagnosticFindings.ToList().FindIndex(f => f.Id == updated.Id);
            if (index >= 0) DiagnosticFindings[index] = updated;
            DiagnosticsReportDetail = $"{updated.Component}: {updated.StateText}.";
        }
        catch (Exception ex) { DiagnosticsReportDetail = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task FixDiagnosticAsync(DiagnosticFindingDto? finding)
    {
        if (finding is null || !finding.CanFix || !finding.FixConfirmed) return;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { DiagnosticsReportDetail = ex.Message; return; }

        IsBusy = true;
        try
        {
            var result = await _api.FixDiagnosticFindingAsync(profile, finding.Id, BearerToken);
            DiagnosticsReportDetail = result.Message;
            if (result.Success) await RecheckDiagnosticAsync(finding);
        }
        catch (Exception ex) { DiagnosticsReportDetail = ex.Message; }
        finally { IsBusy = false; }
    }

    private void ExportDoctorReport()
    {
        if (DoctorChecks.Count == 0) return;
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MystTiqReports");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"MystTiq-Doctor-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var lines = new List<string> { $"MystTiq {Version} Production Doctor", $"Status: {DoctorStatus}", $"Checked: {DoctorCheckedAt}", DoctorSummary, "" };
            foreach (var c in DoctorChecks) { lines.Add($"[{c.State}] {c.Component}"); lines.Add($"Evidence: {c.Evidence}"); lines.Add($"Recommendation: {c.Recommendation}"); lines.Add(""); }
            File.WriteAllLines(path, lines); DoctorExportPath = path; DoctorSummary = $"Diagnostic report exported: {path}";
        }
        catch (Exception ex) { DoctorSummary = ex.Message; }
    }





























    private void GenerateServerName()
    {
        var setting = Configuration.PalworldSettings.FirstOrDefault(x => x.Name.Equals("ServerName", StringComparison.OrdinalIgnoreCase));
        if (setting is null) { Configuration.PalworldConfigState = "ServerName is not present in the active configuration."; return; }
        setting.Value = GenerateRandomServerName();
        Configuration.PalworldConfigState = "Generated a new server name locally. Save Changes to publish it to PalWorldSettings.ini.";
    }

    private static string GenerateRandomServerName()
    {
        string[] adjectives = ["Misty", "Golden", "Azure", "Moonlit", "Frostbound", "Wild", "Crystal", "Ember"];
        string[] nouns = ["Pal Haven", "Pal Realm", "Pal Isles", "Adventure", "Sanctuary", "Frontier", "Expedition", "World"];
        return $"{adjectives[Random.Shared.Next(adjectives.Length)]} {nouns[Random.Shared.Next(nouns.Length)]} {Random.Shared.Next(1000, 10000)}";
    }














    // v0.7.81.0: was a hard `if (SelectedProfile is null) return;` -- SelectedProfile is always
    // null throughout the entire "Set Up New Server" wizard (that's what IsCreatingNewProfile
    // means), so the environment checklist could never be shown there. Switched to the same
    // BuildProfileFromEditor(SelectedProfile?.Id) pattern UpdatePalworldServerAsync/
    // CheckSetupPortAsync already use, which behaves identically once a profile is saved (re-derives
    // the same profile from ProfileName/ServerUrl) and additionally now works mid-wizard once Step
    // 1's Connect has populated those same fields.
    private async Task RefreshEnvironmentAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch { return; }
        EnvironmentHealthText = "Checking…";
        try
        {
            var snapshot = await _api.GetEnvironmentChecklistAsync(profile, BearerToken);
            EnvironmentItems.Clear();
            foreach (var item in snapshot.Items) EnvironmentItems.Add(item);
            var attention = Math.Max(0, snapshot.TotalCount - snapshot.ReadyCount);
            EnvironmentHealthText = attention == 0
                ? $"{snapshot.ReadyCount} / {snapshot.TotalCount} READY"
                : $"{snapshot.ReadyCount} / {snapshot.TotalCount} READY · {attention} NEED ATTENTION";
            RaisePropertyChanged(nameof(SetupReadyCountText));
            RaisePropertyChanged(nameof(SetupAttentionCountText));
            RaisePropertyChanged(nameof(SetupComponentCountText));
            RaisePropertyChanged(nameof(Workspace.WorkspaceDiscoveryText));
        }
        catch (Exception ex)
        {
            EnvironmentHealthText = "Unavailable";
            SetupOperationDetail = ex.Message;
        }
    }

    private async Task VerifyEnvironmentAsync()
    {
        SetupOperationState = "VERIFYING";
        SetupOperationTitle = "Verifying server environment";
        SetupOperationDetail = "Refreshing every server prerequisite through the headless management API.";
        SetupOperationProgress = 25;
        await RefreshEnvironmentAsync();
        SetupOperationProgress = 100;
        SetupOperationState = EnvironmentItems.All(x => x.IsReady) ? "COMPLETE" : "ATTENTION";
        SetupOperationTitle = EnvironmentItems.All(x => x.IsReady) ? "Environment verification completed" : "Environment verification found items needing attention";
        SetupRecentActivity = $"Recent activity: Verify environment · {DateTime.Now:t}.";
    }

    private async Task CreateDefaultServerSettingsAsync()
    {
        if (!SetupCreateConfirmed)
        {
            SetupOperationState = "CONFIRMATION REQUIRED";
            SetupOperationDetail = "Review the first-run values and confirm that a new PalWorldSettings.ini should be created.";
            return;
        }
        if (!int.TryParse(SetupMaximumPlayers, out var maximumPlayers) ||
            !int.TryParse(SetupGamePort, out var gamePort) ||
            !int.TryParse(SetupRestPort, out var restPort))
        {
            SetupOperationState = "VALIDATION FAILED";
            SetupOperationDetail = "Players, Game Port, and REST Port must contain whole numbers.";
            return;
        }

        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { SetupOperationDetail = ex.Message; return; }

        IsBusy = true;
        SetupOperationState = "CREATING";
        SetupOperationTitle = "Creating first-run Palworld settings";
        SetupOperationDetail = "The authenticated headless service is validating the request and will refuse to overwrite an active configuration.";
        SetupOperationProgress = 20;
        try
        {
            var request = new PalworldDefaultConfigurationRequestDto
            {
                ConfirmCreate = true,
                ServerName = SetupServerName,
                ServerDescription = SetupServerDescription,
                AdminPassword = SetupAdminPassword,
                ServerPassword = SetupServerPassword,
                MaximumPlayers = maximumPlayers,
                GamePort = gamePort,
                RestPort = restPort
            };
            var result = await _api.CreateDefaultPalworldConfigurationAsync(profile, request, BearerToken);
            SetupOperationProgress = result.Success ? 100 : 0;
            SetupOperationState = result.Success ? "COMPLETE" : "VALIDATION FAILED";
            SetupOperationTitle = result.Success ? "Default server settings created" : "Default settings were not created";
            SetupOperationDetail = result.Success ? result.Message : string.Join(" • ", result.ValidationErrors.DefaultIfEmpty(result.Message));
            SetupRecentActivity = $"Recent activity: Create default settings {(result.Success ? "completed" : "rejected")} · {DateTime.Now:t}.";
            if (result.Success)
            {
                SetupAdminPassword = string.Empty;
                SetupServerPassword = string.Empty;
                SetupCreateConfirmed = false;
                await Configuration.LoadPalworldConfigurationAsync();
                await RefreshEnvironmentAsync();
            }
        }
        catch (Exception ex)
        {
            SetupOperationProgress = 0;
            SetupOperationState = "FAILED";
            SetupOperationTitle = "Default settings were not created";
            SetupOperationDetail = ex.Message;
        }
        finally { IsBusy = false; }
    }

    // v0.6.3.0: Setup/Update Center cleanup -- this used to call UpdatePalworldServerAsync()
    // (a full SteamCMD update/reinstall) unconditionally, even when SteamCMD/the Palworld
    // Dedicated Server were already installed. Update Center's own "Update Palworld Server"
    // button is now the one authoritative place that mutation happens; Setup only performs it
    // directly for the genuine first-run case (something is actually missing).
    private async Task InstallMissingEnvironmentAsync()
    {
        var stillMissing = EnvironmentItems.Where(x => x.Component is "SteamCMD" or "Palworld Dedicated Server" && x.IsMissing).ToList();
        if (stillMissing.Count == 0)
        {
            SetupOperationState = "COMPLETE";
            SetupOperationTitle = "Nothing to install";
            SetupOperationDetail = "SteamCMD and the Palworld Dedicated Server are already installed. Use Update Center to check for or apply updates.";
            SetupRecentActivity = $"Recent activity: Install missing skipped, nothing missing · {DateTime.Now:t}.";
            return;
        }

        SetupOperationState = "INSTALLING";
        SetupOperationTitle = "Installing required server distribution components";
        SetupOperationDetail = "MystTiq will provision SteamCMD when needed and install/validate the Palworld Dedicated Server. Optional developer/save-tool dependencies remain explicitly listed if manual installation is required.";
        SetupOperationProgress = 15;
        await UpdatePalworldServerAsync();
        SetupOperationProgress = 80;
        await RefreshEnvironmentAsync();
        SetupOperationProgress = 100;
        SetupOperationState = EnvironmentItems.Any(x => x.Status == "MISSING") ? "ATTENTION" : "COMPLETE";
        SetupOperationTitle = SetupOperationState == "COMPLETE" ? "Required server distribution is ready" : "Install completed with remaining optional/manual prerequisites";
        SetupRecentActivity = $"Recent activity: Install missing · {DateTime.Now:t}.";
    }

    private void RunEnvironmentAction(EnvironmentChecklistItemDto? item)
    {
        if (item is null) return;
        if (!item.ActionSupported)
        {
            SetupOperationState = "BACKEND REQUIRED";
            SetupOperationTitle = $"{item.Component} cannot be changed from the GUI yet";
            SetupOperationDetail = item.UnavailableReason ?? "A safe server-side management implementation is required before this action can be enabled.";
            SetupRecentActivity = $"Recent activity: {item.Component} action blocked honestly · {DateTime.Now:t}.";
            return;
        }
        if (item.Component is "Default Server Settings" or "REST API" or "RCON")
        {
            Navigate(nameof(NavigationPage.Configuration));
            return;
        }
        if (item.Component == "UE4SS Runtime")
        {
            Navigate(nameof(NavigationPage.Ue4ss));
            return;
        }
        if (item.Component is "SteamCMD" or "Palworld Dedicated Server")
        {
            // v0.6.3.0: this row's action used to always call InstallMissingEnvironmentAsync
            // (a full SteamCMD update/reinstall), even when clicked on an already-installed row
            // reading "VERIFY" -- silently mutating on what looked like a read-only check. Setup
            // now only mutates directly for the genuine first-run "INSTALL" case; the ongoing
            // "already installed, check/apply updates" case belongs to Update Center.
            if (item.IsMissing)
            {
                RunUi(() => InstallMissingEnvironmentAsync());
            }
            else
            {
                SetupRecentActivity = $"Recent activity: {item.Component} already installed — opening Update Center · {DateTime.Now:t}.";
                Navigate(nameof(NavigationPage.UpdateCenter));
            }
            return;
        }
        SetupOperationState = "VERIFYING";
        SetupOperationTitle = $"Verifying {item.Component}";
        SetupOperationDetail = item.Details;
        RunUi(() => VerifyEnvironmentAsync());
    }

    private async Task RefreshDistributionAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { DistributionDetail = ex.Message; return; }

        IsBusy = true;
        DistributionState = "Checking…";
        try
        {
            var status = await _api.GetServerDistributionStatusAsync(profile, BearerToken);
            DistributionPlatform = status.Platform;
            SteamCmdState = status.SteamCmdExists ? "Installed" : "Missing";
            ServerInstallState = status.IncompleteReason is not null ? "Incomplete" : status.ServerExecutableExists ? "Installed" : "Missing";
            DistributionState = status.IncompleteReason is not null ? "Update required" : status.ServerExecutableExists ? "Ready" : "Setup required";
            DistributionDetail = status.Detail;
        }
        catch (Exception ex)
        {
            DistributionState = "Unavailable";
            DistributionDetail = ex.Message;
        }
        finally { IsBusy = false; }
        // v0.7.45.0: same page, same "is everything up to date" concern -- Refresh now also
        // pulls the full component-by-component version table. Best-effort: a failure here must
        // not blank out the distribution status that already succeeded above.
        await RefreshComponentVersionsAsync();
    }

    // v0.7.82.0: the Update Center's first real, actionable in-place update -- direct request ("the
    // update center where it says update available should allow us to click on it to update").
    // Refreshes the whole component table afterward so the row reflects the real new installed
    // version immediately, not just a stale "Update available" left over from before the upgrade.
    private async Task UpdatePipAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { DistributionDetail = ex.Message; return; }

        IsBusy = true;
        DistributionState = "Updating pip…";
        try
        {
            var result = await _api.UpdatePipAsync(profile, BearerToken);
            DistributionState = result.Success ? "Complete" : "Failed";
            DistributionDetail = result.Message;
        }
        catch (Exception ex)
        {
            DistributionState = "Failed";
            DistributionDetail = ex.Message;
        }
        finally { IsBusy = false; }
        await RefreshComponentVersionsAsync();
    }

    private async Task RefreshComponentVersionsAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch { return; }
        try
        {
            var snapshot = await _api.GetComponentVersionsAsync(profile, BearerToken);
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
            ComponentVersionsCheckedAtText = FormatTime(snapshot.ObservedAt);
        }
        catch { /* best-effort -- the existing DistributionState/Detail already surface connection problems */ }
    }

    private async Task PreviewDistributionPlanAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { DistributionDetail = ex.Message; return; }

        IsBusy = true;
        try
        {
            var plan = await _api.GetServerDistributionPlanAsync(
                profile,
                ValidateServerFiles,
                BearerToken);

            DistributionPlanText =
                $"Platform: {plan.Platform}{Environment.NewLine}" +
                $"SteamCMD: {plan.SteamCmdPath}{Environment.NewLine}" +
                $"Server root: {plan.ServerRoot}{Environment.NewLine}" +
                $"Validate: {plan.Validate}{Environment.NewLine}{Environment.NewLine}" +
                string.Join(Environment.NewLine, plan.Arguments);

            DistributionDetail = "SteamCMD plan refreshed. No server files were changed.";
        }
        catch (Exception ex) { DistributionDetail = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task UpdatePalworldServerAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { DistributionDetail = ex.Message; return; }

        IsBusy = true;
        // v0.7.89.0: direct live feedback -- the install step gave no sense of what stage SteamCMD
        // was actually at. This single REST call is atomic server-side (no incremental progress
        // events exist to report yet), so rather than fabricate fake step percentages, the status
        // text now at least sets an honest expectation up front; DistributionOutputText (SteamCMD's
        // own tail output, already captured) is now also shown in the wizard's Install step itself
        // once this completes, giving real detail instead of just a flat "Complete."
        DistributionState = ValidateServerFiles ? "Updating / validating… (SteamCMD; can take several minutes on first install)" : "Updating… (SteamCMD; can take several minutes on first install)";
        DistributionOutputText = string.Empty;

        try
        {
            var result = await _api.UpdateServerDistributionAsync(
                profile,
                ValidateServerFiles,
                BearerToken);

            DistributionOutputText = string.Join(Environment.NewLine, result.OutputTail);
            DistributionState = result.Success ? "Complete" : "Failed";
            DistributionDetail = result.Message;

            var status = await _api.GetServerDistributionStatusAsync(profile, BearerToken);
            SteamCmdState = status.SteamCmdExists ? "Installed" : "Missing";
            ServerInstallState = status.ServerExecutableExists ? "Installed" : "Missing";
            DistributionPlatform = status.Platform;
        }
        catch (Exception ex)
        {
            DistributionState = "Failed";
            DistributionDetail = ex.Message;
        }
        finally { IsBusy = false; }
    }

    // v0.7.89.0 bug fix: reported live -- "after we install palworld it should install all the
    // items we require and then automatically refresh." The wizard's own Install button called
    // UpdatePalworldServerAsync directly, which never refreshes EnvironmentItems (only
    // Distribution/SteamCmd state), so the Server Environment table kept showing the pre-install
    // "MISSING" row for Palworld Dedicated Server until the user manually clicked Refresh. This
    // wraps the same install with the missing auto-refresh, mirroring what Server Setup's own
    // InstallMissingEnvironmentAsync already does for that page. UE4SS Runtime/Palworld Save
    // Tools/PIM-Oodle Decoder are deliberately NOT auto-installed here -- confirmed via
    // RunEnvironmentAction that none of the three have a one-click install path anywhere in the
    // app today (UE4SS needs its own page's version picker, the other two have no management API
    // at all yet, see their own "BACKEND REQUIRED" rows) -- inventing one here would be a much
    // larger, separate feature, not a bug fix.
    private async Task InstallPalworldServerFromWizardAsync()
    {
        await UpdatePalworldServerAsync();
        await RefreshEnvironmentAsync();
    }

    // v0.7.90.0: direct live feedback -- "option to install just the server without mods and then
    // the option of installing it with the extras." UE4SS is the only checklist item that's both
    // "required" in spirit and actually has a real install path anywhere in the app today (Palworld
    // Save Tools/PIM-Oodle Decoder are BACKEND REQUIRED, see InstallPalworldServerFromWizardAsync's
    // own comment -- nothing to chain in for those yet). Reuses the UE4SS page's own established
    // Preview-then-Apply flow verbatim, just auto-selects the newest non-prerelease Palworld Fork
    // release instead of asking the user to pick one -- the same tradeoff offered and accepted
    // directly: skip the manual review, since this is an explicit "with extras" choice already.
    private async Task InstallPalworldServerWithExtrasAsync()
    {
        await UpdatePalworldServerAsync();
        if (DistributionState != "Complete") return;
        await Mods.InstallLatestUe4ssStableAsync();
        await RefreshEnvironmentAsync();
    }



    private async Task RefreshOperationsAsync()
    {
        if (SelectedProfile is null) return;
        try
        {
            var operations = await _api.GetOperationsAsync(SelectedProfile, 50, BearerToken);
            RecentOperations.Clear();
            foreach (var item in operations) RecentOperations.Add(item);
        }
        catch (Exception ex) { World.WorldTransactionState = "Operations: " + ex.Message; }
    }


    public async Task AnalyzeWorldArchiveAsync(Stream stream, string fileName)
    {
        // v0.7.85.0 bug fix: same class as RefreshEnvironmentAsync's v0.7.81.0 fix -- SelectedProfile
        // is always null while IsCreatingNewProfile (the entire "Set Up New Server" wizard), so this
        // silently no-op'd if embedded there. BuildProfileFromEditor already correctly resolves the
        // wizard's own in-progress connection (RefreshWorldExplorerAsync/World.ApplyWorldTransactionAsync
        // use the same pattern).
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { World.WorldTransactionState = ex.Message; return; }
        IsBusy = true;
        try
        {
            var preview = await _api.AnalyzeWorldArchiveAsync(profile, World.WorldTransactionMode, stream, BearerToken);
            World.WorldPreviewToken = preview.PreviewToken;
            World.WorldTransactionConfirmed = false;
            World.WorldTransactionPlanSteps.Clear();
            foreach (var step in preview.Steps) World.WorldTransactionPlanSteps.Add(step);
            World.WorldTransactionState = $"{fileName}: {preview.EntryCount} entries, {preview.ArchiveBytes / 1024d / 1024d:F2} MB. Preview expires {preview.ExpiresUtc.ToLocalTime():t}.";
        }
        catch (Exception ex) { World.WorldPreviewToken = string.Empty; World.WorldTransactionState = ex.Message; }
        finally { IsBusy = false; }
    }




    // v0.6.15.0 "Save-Data Edit Engine Foundation" -- the first real Pal-level editing UI, on the
    // same Preview/Apply pattern as Guild/Base Ownership above. The edit form is pre-populated from
    // the selected Pal's current values (see World.SelectedExplorerPal's setter) and always sends every
    // field on preview; the server's own diff (in the returned findings) is what actually tells the
    // user what would change, so nothing needs to be diffed client-side.
    private async Task RefreshPalsAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        World.PalEditStatusText = "Loading Pals…";
        try
        {
            var pals = await _api.GetPalsAsync(SelectedProfile, null, BearerToken);
            World.ExplorerPals.Clear();
            foreach (var pal in pals) World.ExplorerPals.Add(pal);
            World.PalEditStatusText = $"{pals.Count} Pal(s) loaded. Select one to edit.";
        }
        catch (Exception ex) { World.PalEditStatusText = ex.Message; }
        finally { IsBusy = false; }
    }







    public string ExportWorldValidationReport()
    {
        var lines = new List<string> { "MystTiq World Validation Report", Workspace.WorldValidationState, $"Exported: {DateTimeOffset.Now:O}", string.Empty };
        lines.AddRange(Workspace.WorldValidationFindings.Select(x => $"[{x.Severity}] {x.Category} / {x.Check}: {x.Detail}"));
        return string.Join(Environment.NewLine, lines);
    }

    private async Task RefreshWorldExplorerAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { World.WorldExplorerDetail = ex.Message; return; }

        IsBusy = true;
        World.WorldExplorerState = "Scanning…";
        try
        {
            var worldTask = _api.GetWorldExplorerAsync(profile, BearerToken);
            var evidenceTask = _api.GetPlayerGuildExplorerAsync(profile, BearerToken);
            await Task.WhenAll(worldTask, evidenceTask);
            var snapshot = await worldTask;
            var evidence = await evidenceTask;

            Workspace.WorldCandidates.Clear();
            foreach (var world in snapshot.Worlds)
                Workspace.WorldCandidates.Add(world);

            Workspace.WorldFiles.Clear();
            foreach (var file in snapshot.Files)
                Workspace.WorldFiles.Add(file);

            Workspace.SelectedWorldFile = Workspace.WorldFiles.FirstOrDefault();

            Workspace.ActiveWorldIdText = snapshot.ActiveWorldId ?? "Not resolved";
            Workspace.WorldCountText = snapshot.WorldCount.ToString();
            Workspace.WorldFileCountText = snapshot.FileCount.ToString();
            Workspace.WorldPlayerSaveCountText = snapshot.PlayerSaveCount.ToString();
            Workspace.WorldSizeText = $"{snapshot.TotalSizeBytes / 1024d / 1024d:F2} MB";
            Workspace.WorldSaveRootText = snapshot.SaveRoot;
            Workspace.WorldSaveDataCountText = snapshot.Statistics.SaveDataFiles.ToString();
            Workspace.WorldDiagnosticCountText = snapshot.Statistics.DiagnosticFiles.ToString();
            Workspace.WorldEmptyCountText = snapshot.Statistics.EmptyFiles.ToString();
            Workspace.WorldAgeRangeText = snapshot.Statistics.AgeRangeText;
            Workspace.WorldIntegrityState = snapshot.Integrity.State;
            Workspace.WorldIntegrityFindings.Clear();
            foreach (var finding in snapshot.Integrity.Findings) Workspace.WorldIntegrityFindings.Add(finding);
            if (Workspace.WorldIntegrityFindings.Count == 0) Workspace.WorldIntegrityFindings.Add("Required world files are present and no structural issue was detected.");
            World.WorldInspectorPlayerCountText = evidence.Players.Count.ToString();
            World.WorldInspectorGuildCountText = evidence.Guilds.Count.ToString();
            World.WorldInspectorBaseCountText = evidence.Guilds.Sum(x => x.BaseIds.Count).ToString();
            World.WorldExplorerState = snapshot.Available ? "Ready" : "Unavailable";
            World.WorldExplorerDetail = snapshot.Detail;
        }
        catch (Exception ex)
        {
            World.WorldExplorerState = "Unavailable";
            World.WorldExplorerDetail = ex.Message;
        }
        finally { IsBusy = false; }
    }

    private async Task RefreshPlayerGuildExplorerAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { World.PlayerGuildDetail = ex.Message; return; }

        IsBusy = true;
        World.PlayerGuildState = "Scanning…";
        try
        {
            var selectedGuildId = World.SelectedExplorerGuild?.GuildId;
            var selectedBaseId = World.SelectedExplorerBase?.BaseId;
            var snapshot = await _api.GetPlayerGuildExplorerAsync(profile, BearerToken);

            World.ExplorerPlayers.Clear();
            foreach (var player in snapshot.Players)
                World.ExplorerPlayers.Add(player);

            World.ExplorerGuilds.Clear();
            foreach (var guild in snapshot.Guilds)
                World.ExplorerGuilds.Add(guild);

            // v1.0.0.5: base cards (location, workers) and guild rosters (MainWindowViewModel.WorldCards.cs); the same read
            // gives the map its bases, so "Show on map" has them.
            World.PlayerGuildWarnings.Clear();
            foreach (var warning in snapshot.Warnings)
                World.PlayerGuildWarnings.Add(warning);
            ApplyWorldCards(snapshot);
            Map.ApplyBaseLocations(snapshot);

            World.ApplyGuildFilters();
            World.ApplyBaseFilters();

            World.SelectedExplorerPlayer = World.ExplorerPlayers.FirstOrDefault();
            World.SelectedExplorerGuild = !string.IsNullOrWhiteSpace(selectedGuildId)
                ? World.ExplorerGuilds.FirstOrDefault(x => string.Equals(x.GuildId, selectedGuildId, StringComparison.OrdinalIgnoreCase))
                : World.FilteredExplorerGuilds.FirstOrDefault();
            World.SelectedExplorerBase = !string.IsNullOrWhiteSpace(selectedBaseId)
                ? World.ExplorerBases.FirstOrDefault(x => string.Equals(x.BaseId, selectedBaseId, StringComparison.OrdinalIgnoreCase))
                : World.FilteredExplorerBases.FirstOrDefault();

            Players.PlayerRecordCountText = snapshot.Players.Count.ToString();
            World.GuildRecordCountText = snapshot.Guilds.Count.ToString();
            SemanticStateText = snapshot.SemanticAvailable
                ? "Decoded guild semantics available"
                : "Player-save identities only";
            SemanticSourceText = snapshot.SemanticSource;
            World.PlayerGuildState = snapshot.Available ? "Ready" : "Unavailable";
            World.PlayerGuildDetail = snapshot.Detail;
        }
        catch (Exception ex)
        {
            World.PlayerGuildState = "Unavailable";
            World.PlayerGuildDetail = ex.Message;
        }
        finally { IsBusy = false; }
    }



    private async Task OpenSelectedGuildLeaderAsync()
    {
        var leaderId = World.SelectedExplorerGuild?.LeaderPlayerId;
        if (string.IsNullOrWhiteSpace(leaderId)) return;
        SelectedPage = NavigationPage.Players;
        await RefreshPlayersPageAsync();
        Players.SelectedPlayerRecord = Players.PlayerRecords.FirstOrDefault(x => string.Equals(x.PlayerId, leaderId, StringComparison.OrdinalIgnoreCase));
        if (Players.SelectedPlayerRecord is not null)
            await Players.LoadSelectedPlayerMetadataAsync();
    }












    private readonly INexusModsClient _nexus;


































    private async Task StartServerAsync()
    {
        if (!await EnsureManagementConnectionForLifecycleAsync())
            return;

        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { LifecycleStatusText = ex.Message; Detail = ex.Message; return; }

        try
        {
            var distribution = await _api.GetServerDistributionStatusAsync(profile, BearerToken);
            if (!distribution.ServerExecutableExists)
            {
                LifecycleStatusText = $"Start blocked: PalServer executable was not found. {distribution.Detail}";
                Detail = LifecycleStatusText;
                return;
            }
        }
        catch (Exception ex)
        {
            LifecycleStatusText = $"Start preflight failed: {ex.Message}";
            Detail = LifecycleStatusText;
            return;
        }

        await RunLifecycleAsync("Starting…", _api.StartServerAsync);
    }

    private async Task<bool> EnsureManagementConnectionForLifecycleAsync()
    {
        if (ManagementApiConnected) return true;
        // v0.7.89.0 bug fix: reported live -- "the server did not start and i have no messages as
        // to why." This used to only attempt local reconnection when SelectedProfile.Id was
        // literally "default", silently returning false (no status text set at all -- Start just
        // appeared to do nothing) for a genuine second local server registered via the New Server
        // wizard or Fleet. Such a profile is exactly as local as "default" -- same owned sidecar,
        // same machine -- it just doesn't have the reserved default Id. The actual signal that
        // matters here is whether this profile's own address is loopback, not its Id.
        var isLoopbackProfile = SelectedProfile is not null &&
            System.Net.IPAddress.TryParse(SelectedProfile.BaseAddress.Host, out var loopbackCandidate) &&
            System.Net.IPAddress.IsLoopback(loopbackCandidate);
        if (!isLoopbackProfile)
        {
            LifecycleStatusText = "Not connected to the management API, and this isn't a local desktop-owned server MystTiq can automatically reconnect to. Reconnect it from Settings, then try again.";
            Detail = LifecycleStatusText;
            return false;
        }

        LifecycleStatusText = "Connecting to the local MystTiq management backend…";
        var snapshot = await RefreshLocalInstallationAsync();
        if (snapshot is null) return false;
        var bootstrap = await _localBootstrapper.EnsureAvailableAsync(snapshot);
        if (!bootstrap.Available)
        {
            LifecycleStatusText = bootstrap.Detail;
            Detail = bootstrap.Detail;
            return false;
        }
        if (bootstrap.StaleInstanceDetected)
        {
            // A different-version MystTiq backend was already running locally and was left alone
            // (never killed automatically) -- surfaced here, not just on failure, since this exact
            // path (connecting right before Start/Stop) is where that confusion is most likely.
            LifecycleStatusText = bootstrap.Detail;
            Detail = bootstrap.Detail;
        }
        ServerUrl = bootstrap.Endpoint;
        await RefreshAsync(silent: true);
        if (!ManagementApiConnected)
        {
            LifecycleStatusText = $"The local management backend is reachable at {bootstrap.Endpoint}, but the GUI could not establish a compatible API session. {Detail}";
            Detail = LifecycleStatusText;
        }
        return ManagementApiConnected;
    }

    private async Task StopServerAsync() =>
        await RunLifecycleAsync("Stopping…", _api.StopServerAsync);

    private async Task RestartServerAsync() =>
        await RunLifecycleAsync("Restarting…", _api.RestartServerAsync);

    private async Task ForceStopServerAsync() =>
        await RunLifecycleAsync("Force stopping…", _api.ForceStopServerAsync);

    public async Task ShutdownForExitAsync(bool force)
    {
        if (!ManagementApiConnected) return;
        try
        {
            var profile = BuildProfileFromEditor(SelectedProfile?.Id);
            LifecycleStatusText = force ? "Force-stopping PalServer before exit…" : "Stopping PalServer safely before exit…";
            var result = force
                ? await _api.ForceStopServerAsync(profile, BearerToken, CancellationToken.None)
                : await _api.StopServerAsync(profile, BearerToken, CancellationToken.None);
            LifecycleStatusText = result.Message ?? (result.Success ? "PalServer stopped for application exit." : "PalServer shutdown reported a failure.");
        }
        catch (Exception ex)
        {
            LifecycleStatusText = $"Exit shutdown warning: {ex.Message}";
        }
    }

    private async Task RunLifecycleAsync(
        string activity,
        Func<ConnectionProfile, string?, CancellationToken, Task<LifecycleOperationResultDto>> operation)
    {
        ConnectionProfile profile;
        try
        {
            profile = BuildProfileFromEditor(SelectedProfile?.Id);
        }
        catch (Exception ex)
        {
            ConnectionState = "Invalid profile";
            Detail = ex.Message;
            return;
        }

        BusyReason = activity;
        IsBusy = true;
        ConnectionState = activity;
        LifecycleStatusText = $"{activity.TrimEnd('…')} PalServer through the MystTiq management service.";
        Detail = LifecycleStatusText;

        // v0.6.14.0: the passive auto-refresh timer skips its tick entirely while IsBusy (set just
        // above), and this is the only other place ApplyLogs was called -- once, after the whole
        // operation finished. A real Start/Stop can take anywhere from several seconds to the
        // configured startup/stop timeout, so the Console page showed nothing new for that entire
        // window, exactly during the moment its live narrative matters most. Runs a lightweight,
        // console-only tail poll concurrently with the main operation (not the full status poll --
        // that stays IsBusy-gated to avoid overlapping lifecycle-adjacent calls), stopped the moment
        // the operation completes.
        using var consoleTailCts = new CancellationTokenSource();
        var consoleTailLoop = PollConsoleWhileBusyAsync(profile, consoleTailCts.Token);

        try
        {
            var result = await operation(profile, BearerToken, CancellationToken.None);
            if (result.Snapshot is not null)
                ApplyStatus(result.Snapshot);

            LifecycleStatusText = result.Message ?? (result.Success ? "Lifecycle operation completed." : $"Lifecycle operation failed (exit code {result.ExitCode}).");
            Detail = LifecycleStatusText;
            ManagementApiConnected = true;
            ConnectionState = result.Success ? "Connected" : "Operation failed";

            // Always reacquire one coherent authoritative sample after a mutation. v0.7.33.0: log
            // count raised to the server's real 500-line cap (was 120) -- this is the console
            // snapshot right after Start/Stop/Restart complete, exactly where a modded server's
            // full UE4SS/MOD LOAD startup output needs to actually be visible.
            var poll = await _api.GetStatusPollingAsync(profile, 500, BearerToken);
            ApplyStatus(poll.Status);
            ApplyServiceStatus(poll.Service);
            ApplyPlayers(poll.Players);
            ApplyMetrics(poll.Metrics);
            ApplyLogs(poll.LogTail);
        }
        catch (Exception ex)
        {
            ConnectionState = "Operation failed";
            LifecycleStatusText = ex.Message;
            Detail = ex.Message;
        }
        finally
        {
            consoleTailCts.Cancel();
            try { await consoleTailLoop; } catch (OperationCanceledException) { }
            IsBusy = false;
            BusyReason = null;
        }
    }

    private async Task PollConsoleWhileBusyAsync(ConnectionProfile profile, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(2), token); }
            catch (OperationCanceledException) { return; }

            // v0.7.33.0: raised to the server's real 500-line cap (was 120) -- this loop is the
            // live view during the entire Start/Stop/Restart window, the exact moment a modded
            // server's full startup output needs to be visible, not just the final snapshot after.
            try { ApplyLogs(await _api.GetLogTailAsync(profile, 500, BearerToken)); }
            catch { /* best-effort; the final authoritative poll after the operation completes will catch up */ }
        }
    }

    private void ApplyStatus(ServerStatusDto status)
    {
        // Same "is the server running" definition already proven server-side for gating a
        // mutating action while running (HeadlessPalEditService.ApplyAsync's precondition) --
        // IsProcessLive catches "process launched but not yet Ready" (mid-startup), so Start
        // doesn't stay clickable during that window. v0.7.88.0 bug fix: this used to test
        // NativeProcessId.HasValue directly, which stayed true forever using a stale last-known PID
        // even once the backend reported the server genuinely Stopped -- see ServerStatusDto.IsProcessLive.
        ServerIsRunning = status.IsProcessLive || status.Ready;

        ManagedProcesses.Clear();
        foreach (var process in status.Processes) ManagedProcesses.Add(process);
        RaisePropertyChanged(nameof(HasManagedProcesses));

        // v0.7.82.0 bug fix: ServerIsRunning above already treats a detected-but-not-yet-ready
        // native process as "running" (so Start correctly stays disabled and only Stop is offered),
        // but this text used to collapse that exact same state down to "Stopped / Not ready" --
        // directly contradicting the ribbon right next to it. A real PalServer process being alive
        // is never "stopped," whether or not its port has come up yet.
        ServerState = status.Ready
            ? "Running / Ready"
            : status.CrashDetected
                ? "Crash detected"
                : status.IsProcessLive
                    ? "Starting / Not Ready"
                    : "Stopped / Not ready";
        // v1.0.0.1: a start that has run for StartupWatch.StuckAfter without opening its port offers the stuck-start test.
        IsStartupStuck = status.StartupStuck && status.IsProcessLive;
        // v1.0.7.2 (roadmap Q-8): the step a running start is at.
        StartupStageText = status.StartupStageText(DateTimeOffset.UtcNow);

        NativePidText = status.NativeProcessId?.ToString() ?? "—";
        ListenerText = status.GuardedListeningPorts.Count > 0
            ? string.Join(", ", status.GuardedListeningPorts.Select(p => $"UDP {p}"))
            : "None";

        LastObservedText = FormatTime(status.ObservedAt);
        LastTransitionText = FormatTime(status.LastTransitionAt);
        UptimeText = status.Ready && status.LastTransitionAt is { } lastTransitionAt
            ? FormatDuration(DateTimeOffset.UtcNow - lastTransitionAt)
            : "—";

        Detail = status.Detail ?? "MystTiq returned server status.";
        DashboardHealthText = status.Ready ? "READY" : status.CrashDetected ? "ATTENTION" : status.IsProcessLive ? "STARTING" : "STOPPED";
        // v0.7.29.0 bug fix: these two used to always show a generic hardcoded string whenever the
        // server wasn't Ready, discarding status.Detail entirely -- so even after GetStatusAsync
        // started distinguishing "genuinely not running" from "running at an unexpected path" (see
        // WindowsServerLifecycleService), the Dashboard's SERVER/OVERALL HEALTH cards never showed
        // the difference. Now they surface the backend's actual explanation.
        Dashboard.HealthDetail = status.Ready
            ? $"PalServer healthy · {ListenerText}"
            : status.CrashDetected ? "Crash evidence detected; open Doctor." : (status.Detail ?? "Stopped intentionally or awaiting start.");
        Dashboard.SessionText = status.Ready ? $"Session {UptimeText}" : (status.Detail ?? "No active PalServer session");
    }

    private void ApplyServiceStatus(ServiceStatusDto status)
    {
        if (string.Equals(status.SubState, "windows-sidecar", StringComparison.OrdinalIgnoreCase))
        {
            ServiceState = "Standalone headless API";
            return;
        }

        if (!status.Installed)
        {
            ServiceState = "Not installed";
            return;
        }

        ServiceState = string.Equals(status.ActiveState, "active", StringComparison.OrdinalIgnoreCase)
            ? status.Enabled ? "Active / Enabled" : "Active"
            : $"{status.ActiveState ?? "unknown"} / {status.SubState ?? "unknown"}";
    }

    internal static string FormatTime(DateTimeOffset value) =>
        value == default ? "Unknown" : value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

    private static string FormatTime(DateTimeOffset? value) =>
        value.HasValue ? FormatTime(value.Value) : "Unknown";

    private static string FormatAge(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        if (value.TotalSeconds < 60) return $"{Math.Max(0, (int)value.TotalSeconds)}s";
        if (value.TotalMinutes < 60) return $"{(int)value.TotalMinutes}m";
        if (value.TotalHours < 24) return $"{(int)value.TotalHours}h {value.Minutes}m";
        return $"{(int)value.TotalDays}d {value.Hours}h";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            return "—";

        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m";

        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";

        return $"{Math.Max(0, duration.Minutes)}m {duration.Seconds}s";
    }

    private ConnectionProfile BuildProfileFromEditor(string? existingId)
    {
        if (string.IsNullOrWhiteSpace(ProfileName))
            throw new InvalidOperationException("Profile name is required.");

        if (!Uri.TryCreate(ServerUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("Enter an absolute http:// or https:// MystTiq management URL.");

        var pin = MystTiqApiClient.NormalizeFingerprint(CertificateSha256);
        var accentColorKey = ResolveAccentColorKey(existingId, ActiveTab?.Profile);
        var serverId = string.IsNullOrWhiteSpace(TargetServerId) ? null : TargetServerId.Trim();

        // v0.7.63.0: catches the exact mistake reported live -- two profiles pointed at the same
        // host (same BaseAddress) that also resolve to the same actual server (same ServerId, or
        // both blank -- a blank ServerId always means "this host's default server" regardless of
        // which profile leaves it blank, so two blank entries collide too). Without this, nothing
        // stopped saving a profile that silently duplicates an already-open connection under a
        // different name; the two tabs would show and control the identical PalServer instance
        // with no indication they were the same thing. Compares by normalized origin (scheme+host+
        // port) rather than the raw Uri, since ".../" vs "..." would otherwise dodge the check.
        // v0.7.82.0 bug fix: "Set Up New Server" genuinely needs to talk to the local engine through
        // the same default-scoped connection "Local MystTiq" already uses -- purely to inspect state
        // (ports, existing fleet profiles, environment) before a real, distinct identity exists (that
        // only happens once the Install Directory step provisions one and sets TargetServerId). It is
        // never trying to save a duplicate profile at this point, so the guard below (which exists to
        // stop exactly that -- see its own comment) doesn't apply yet. Reproduced live: the very first
        // real Connect attempt from this wizard threw "already connects to this exact server" and
        // could never get past Step 1, since "Local MystTiq" (ServerId null, same as this wizard's own
        // still-blank TargetServerId) is always present in Profiles as a built-in entry.
        var skipUniquenessCheckForInProgressSetup = IsNewServerSetupFlow && string.IsNullOrWhiteSpace(TargetServerId);
        if (!skipUniquenessCheckForInProgressSetup)
        {
            var normalizedOrigin = uri.GetLeftPart(UriPartial.Authority);
            var collision = Profiles.FirstOrDefault(p =>
                p.Id != (existingId ?? string.Empty) &&
                string.Equals(p.BaseAddress.GetLeftPart(UriPartial.Authority), normalizedOrigin, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.ServerId ?? string.Empty, serverId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            if (collision is not null)
                throw new InvalidOperationException(
                    $"'{collision.Name}' already connects to this exact server ({normalizedOrigin}" +
                    (string.IsNullOrWhiteSpace(serverId) ? string.Empty : $", server \"{serverId}\"") +
                    $"). Give this profile a different Target server ID, or edit '{collision.Name}' instead of creating a duplicate.");
        }

        return new ConnectionProfile(
            existingId ?? Guid.NewGuid().ToString("N"),
            ProfileName.Trim(),
            uri,
            pin,
            accentColorKey,
            serverId);
    }

    private void RaisePageVisibility()
    {
        RebuildRibbonGroups();
        RaisePropertyChanged(nameof(ShowGlobalPageHeader));
        RaisePropertyChanged(nameof(IsDashboardPage));
        RaisePropertyChanged(nameof(IsServerPage));
        RaisePropertyChanged(nameof(IsSetupUpdatePage));
        RaisePropertyChanged(nameof(IsWorldExplorerPage));
        RaisePropertyChanged(nameof(IsWorldTransactionsPage));
        RaisePropertyChanged(nameof(IsPlayerGuildExplorerPage));
        RaisePropertyChanged(nameof(IsPlayersPage));
        RaisePropertyChanged(nameof(IsMonitoringPage));
        RaisePropertyChanged(nameof(IsBackupsPage));
        RaisePropertyChanged(nameof(IsWorkspacePage));
        RaisePropertyChanged(nameof(IsLauncherPage));
        RaisePropertyChanged(nameof(IsModsPage));
        RaisePropertyChanged(nameof(IsDoctorPage));
        RaisePropertyChanged(nameof(IsDiagnosticsPage));
        RaisePropertyChanged(nameof(IsSettingsPage));
        RaisePropertyChanged(nameof(IsServerSetupPage));
        RaisePropertyChanged(nameof(IsUpdateCenterPage));
        RaisePropertyChanged(nameof(IsBasesPage));
        RaisePropertyChanged(nameof(IsGuildsPage));
        RaisePropertyChanged(nameof(IsMapPage));
        RaisePropertyChanged(nameof(IsConsolePage));
        RaisePropertyChanged(nameof(IsActivityAuditPage));
        RaisePropertyChanged(nameof(IsNotificationsPage));
        RaisePropertyChanged(nameof(IsAutomationPage));
        RaisePropertyChanged(nameof(IsSecurityPage));
        RaisePropertyChanged(nameof(IsAlertCenterPage));
        RaisePropertyChanged(nameof(IsFleetPage));
        RaisePropertyChanged(nameof(IsHostPage));
        RaisePropertyChanged(nameof(IsV5HostCategory));
        RaisePropertyChanged(nameof(IsModDashboardPage));
        RaisePropertyChanged(nameof(IsModLibraryPage));
        RaisePropertyChanged(nameof(IsUe4ssPage));
        RaisePropertyChanged(nameof(IsModInventoryPage));
        RaisePropertyChanged(nameof(IsPlaceholderPage));
        RaisePropertyChanged(nameof(IsCrashAnalyzerPage));
        RaisePropertyChanged(nameof(IsSaveToolsPage));
        RaisePropertyChanged(nameof(IsHomeGroupSelected));
        RaisePropertyChanged(nameof(IsServerGroupSelected));
        RaisePropertyChanged(nameof(IsWorldGroupSelected));
        RaisePropertyChanged(nameof(IsModsGroupSelected));
        RaisePropertyChanged(nameof(IsToolsGroupSelected));
        RaisePropertyChanged(nameof(IsSystemGroupSelected));
        RaisePropertyChanged(nameof(IsV5HomeCategory));
        RaisePropertyChanged(nameof(IsV5ServerCategory));
        RaisePropertyChanged(nameof(IsV5WorldCategory));
        RaisePropertyChanged(nameof(IsV5BackupsCategory));
        RaisePropertyChanged(nameof(IsV5ModsCategory));
        RaisePropertyChanged(nameof(IsV5ToolsCategory));
        RaisePropertyChanged(nameof(IsV5SystemCategory));
        RaisePropertyChanged(nameof(PageArtwork));
        RaisePropertyChanged(nameof(SetupArtwork));
    }
}
