using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Threading;
using MystTiq.Core.Services;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly IMystTiqApiClient _api;
    private readonly IConnectionProfileStore _profileStore;
    private readonly ILocalInstallationDiscoveryService _localDiscovery;
    private readonly IMystTiqServiceDiscoveryService _serviceDiscovery;
    private readonly ILocalManagementBootstrapper _localBootstrapper;
    private readonly LocalDiagnosticsService _localDiagnostics = new();
    private readonly DispatcherTimer _refreshTimer;

    private NavigationPage _selectedPage = NavigationPage.Dashboard;
    private ConnectionProfile? _selectedProfile;

    private string _profileName = "Local MystTiq";
    private string _serverUrl = ConnectionProfile.LocalDefault.BaseAddress.ToString().TrimEnd('/');
    private string _bearerToken = string.Empty;
    private string _certificateSha256 = string.Empty;

    private string _connectionState = "Not connected";
    private string _serverState = "Unknown";
    private string _serviceState = "Unknown";
    private string _detail = "Choose a connection profile or configure a secured remote MystTiq service.";
    private string _nativePidText = "—";
    private string _listenerText = "—";
    private string _lastObservedText = "Never";
    private string _lastTransitionText = "Unknown";
    private string _uptimeText = "—";
    private bool _isBusy;
    private bool _autoRefreshEnabled = true;
    private int _refreshTick;
    private string _playersState = "Not sampled";
    private string _onlinePlayerCountText = "—";
    private string _cpuText = "—";
    private string _memoryText = "—";
    private string _threadCountText = "—";
    private string _monitoringDetail = "Connect to a MystTiq service to begin monitoring.";
    private string _logFileText = "No log selected";
    private string _consoleSeverityFilter = "All";
    private string _consoleCategoryFilter = "All";
    private string _consoleSearchText = string.Empty;
    private bool _hideRoutineRest = true;
    private bool _consolePaused;
    private string _consoleViewState = "Live";
    private string _rconState = "Not checked";
    private string _rconDetail = "RCON is deprecated by Palworld; use it only when required by a legacy workflow.";
    private string _rconCommandText = "Info";
    private string _selectedRconPreset = "Info";
    private bool _rconConnected;
    private BackupItemDto? _selectedBackup;
    private string _backupState = "Not loaded";
    private string _backupTotalSizeText = "—";
    private string _backupDetail = "Connect to load backup inventory.";
    private string _backupRootPath = string.Empty;
    private bool _backupRestoreConfirmed;
    private int _backupRetentionKeepLatest = 10;
    private int _backupRetentionMaxAgeDays = 30;
    private string _backupRetentionToken = string.Empty;
    private string _backupRetentionState = "Preview a policy before cleanup.";

    private bool _configLoaded;
    private bool _configApiEnabled;
    private string _configBindAddress = "127.0.0.1";
    private int _configPort = 8213;
    private int _startupTimeoutSeconds = 90;
    private int _stopTimeoutSeconds = 30;
    private int _servicePollSeconds = 5;
    private int _recoveryBackoffSeconds = 10;
    private int _maximumRecoveryAttempts = 5;
    private int _recoveryWindowSeconds = 300;
    private string _configServerRoot = string.Empty;
    private string _configSteamCmdPath = string.Empty;
    private string _configBackupRoot = string.Empty;
    private string _configRuntimeRoot = string.Empty;
    private string _configLaunchArguments = string.Empty;
    private string _configSecurityText = "Not loaded";
    private string _configState = "Not loaded";
    private string _workspaceState = "Load the managed configuration to inspect server paths.";
    private string _doctorStatus = "Not run";
    private string _doctorSummary = "Connect to a MystTiq service, then run Doctor.";
    private string _doctorCheckedAt = "Never";
    private string _doctorExportPath = string.Empty;
    private DiagnosticsReportDto? _latestDiagnosticsReport;
    private string _diagnosticsReportDetail = string.Empty;
    private string _distributionState = "Not checked";
    private string _distributionDetail = "Connect to inspect Palworld server installation.";
    private string _steamCmdState = "Unknown";
    private string _serverInstallState = "Unknown";
    private string _distributionPlatform = "—";
    private string _distributionPlanText = string.Empty;
    private string _distributionOutputText = string.Empty;
    private bool _validateServerFiles = true;
    private string _worldExplorerState = "Not loaded";
    private string _worldExplorerDetail = "Connect to inspect Palworld save data.";
    private string _activeWorldIdText = "—";
    private string _worldCountText = "—";
    private string _worldFileCountText = "—";
    private string _worldPlayerSaveCountText = "—";
    private string _worldSizeText = "—";
    private string _worldSaveRootText = "—";
    private string _worldSaveDataCountText = "—";
    private string _worldDiagnosticCountText = "—";
    private string _worldEmptyCountText = "—";
    private string _worldAgeRangeText = "—";
    private string _worldIntegrityState = "Not checked";
    private string _worldInspectorPlayerCountText = "—";
    private string _worldInspectorGuildCountText = "—";
    private string _worldInspectorBaseCountText = "—";
    private WorldFileDto? _selectedWorldFile;
    private string _worldValidationState = "Not validated";
    private string _worldTransactionState = "Analyze an archive to create a reviewable plan.";
    private string _worldTransactionMode = "world-import";
    private string _worldPreviewToken = string.Empty;
    private bool _worldTransactionConfirmed;
    private string _playerGuildState = "Not loaded";
    private string _playerGuildDetail = "Connect to explore player and guild identities.";
    private string _semanticStateText = "—";
    private string _semanticSourceText = "—";
    private string _playerRecordCountText = "—";
    private string _guildRecordCountText = "—";
    private PlayerExplorerItemDto? _selectedExplorerPlayer;
    private PlayerSnapshotDto? _selectedOnlinePlayer;
    private PlayerExplorerItemDto? _selectedPlayerRecord;
    private string _playerSearchText = string.Empty;
    private string _selectedPlayerViewFilter = "All Players";
    private string _selectedPlayerAdminFilter = "All Records";
    private string _playersPageState = "Not loaded";
    private string _playersPageDetail = "Connect to discover live and saved player records.";
    private string _playerVisibleCountText = "0 visible";
    private string _selectedPlayerNotes = string.Empty;
    private string _newPlayerWarning = string.Empty;
    private string _playerMetadataState = "Select a player to load notes and warnings.";
    private PlayerExplorerItemDto? _migrationSourcePlayer;
    private PlayerExplorerItemDto? _migrationDestinationPlayer;
    private string _migrationState = "Pick a source and destination player, then Preview.";
    private CharacterMigrationPreviewDto? _migrationPreview;
    private string _migrationDisposition = "Keep";
    private GuildExplorerItemDto? _selectedExplorerGuild;
    private BaseExplorerItemDto? _selectedExplorerBase;
    private string _guildSearchText = string.Empty;
    private string _selectedGuildStatusFilter = "All Guilds";
    private string _baseSearchText = string.Empty;
    private string _selectedBaseStatusFilter = "All Bases";
    private string _guildVisibleCountText = "0 visible";
    private string _baseVisibleCountText = "0 visible";
    private string _guildOperationType = "Claim Orphaned Guild";
    private string _guildOperationPlayerId = string.Empty;
    private string _guildOperationPreviewToken = string.Empty;
    private bool _guildOperationConfirmed;
    private string _guildOperationStatusText = "Select a guild, choose an operation, and enter the target player's ID.";
    private string _baseTransferTargetGuildId = string.Empty;
    private string _baseTransferPreviewToken = string.Empty;
    private bool _baseTransferConfirmed;
    private string _baseTransferStatusText = "Select a base above and enter the target guild's ID.";
    private string _baseRecoveryPreviewToken = string.Empty;
    private bool _baseRecoveryConfirmed;
    private string _baseRecoveryStatusText = "Select a base above, then preview to see how many owned records will be removed.";
    private string _modState = "Not loaded";
    private string _modSummary = "Connect to scan MODs.";
    private string _modHealth = "—";
    private string _modInstalledText = "—";
    private string _modConfirmedText = "—";
    private string _modUnverifiedText = "—";
    private string _modDisabledText = "—";
    private string _modIssuesText = "—";
    private string _ue4ssHealth = "—";
    private string _ue4ssDetection = "—";
    private string _ue4ssActiveRoot = "—";
    private string _ue4ssRuntimeRoot = "—";
    private string _ue4ssWarning = string.Empty;
    private string _ue4ssInstalledVersion = "Not detected";
    private string _selectedUe4ssFork = "Palworld Fork";
    private ModItemDto? _selectedMod;
    private string _modInstallType = "PAK";
    private string _modInstallPackage = string.Empty;
    private WorkshopItemDto? _selectedWorkshopItem;
    private string _workshopScanState = "Refresh to scan local Steam Workshop content.";
    private string _networkHealth = "Not run";
    private string _localDiagnosticsState = "Diagnose a connection profile to see staged DNS/TCP/TLS/HTTP results.";
    private string _networkRuntime = "Unknown";
    private string _networkPort = "—";
    private string _networkBinding = "—";
    private string _networkLanEndpoint = "—";
    private string _networkRecommendation = "Run Diagnostics";
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
    private string _dashboardWorldText = "—";
    private string _dashboardBackupText = "—";
    private string _dashboardGuildText = "—";
    private string _dashboardModText = "—";
    private string _dashboardHealthText = "IDLE";
    private string _dashboardHealthDetail = "Waiting for a server sample.";
    private string _dashboardWorldPulseText = "World data not sampled";
    private string _dashboardWorldNicknameText = "World —";
    private string _dashboardWorldClockText = "Day — • --:--";
    private string _dashboardWorldClockDetailText = "Waiting for authoritative saved-world clock";
    private string _dashboardPulseSaveText = "Save: —";
    private string _dashboardPulseBackupText = "Backup: —";
    private string _dashboardRestText = "REST: Checking";
    private string _dashboardRconText = "RCON: Checking";
    private string _dashboardModsStripText = "Mods: —";
    private string _dashboardBackupStripText = "Backup: —";
    private string _dashboardServerNameText = "—";
    private string _dashboardServerDescriptionText = "—";
    private string _dashboardSessionText = "No active session";
    private string _dashboardPlayersSessionText = "0 online";
    private string _dashboardLastActivityText = "Manager started";
    private double _cpuPercentValue;
    private double _memoryMbValue;
    private double _memoryScaleValue;
    private string _statusBarText = "Ready";
    private string _statusBarObservedText = "Not sampled";
    private string _discoveryStateText = "LAN discovery has not run.";
    private DiscoveredMystTiqService? _selectedDiscoveredService;
    private bool _managementApiConnected;
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
    private string _fleetState = "Open Fleet to list configured server profiles.";
    private string _cloneNewProfileId = string.Empty;
    private string _cloneNewProfileName = string.Empty;
    private string _cloneWorldStatusText = "Clones this connection's server (binaries + world) into a new profile. Requires the source server to be stopped.";
    private string _crashAnalyzerState = "Run analysis to inspect bounded recent server-log evidence.";
    private string _saveToolsState = "Open Save Tools to inspect server-side dependencies and saves.";
    private string _saveToolsPaths = "Not inspected";
    private SaveFileDto? _selectedSaveFile;
    private string _playerAdminStatusText = "Right-click a player for administration actions.";
    private string _moderationProviderStatusText = string.Empty;
    private string _playerRegistrySummaryText = string.Empty;
    private string _playerActionMessage = "Removed by administrator.";
    private string _playerActionItem = string.Empty;
    private string _palworldConfigState = "Not loaded";
    private string _palworldConfigPath = "Not loaded";
    private bool _palworldConfigLoaded;
    private bool _isConfigSimpleView = true;
    private string _configSearchText = string.Empty;
    private string _selectedConfigCategory = "All Categories";
    private string _selectedConfigPreset = "Balanced QoL";
    private string _palworldConfigDirtyText = "No unsaved changes";
    private string _palworldConfigValidationText = "Load the active configuration to validate settings.";
    private bool _palworldConfigHasValidationErrors;
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
    private bool _setupCreateConfirmed;
    private string _selectedHistoryRange = "1 Hour";
    private string _historyCpuSummary = "CPU history collecting…";
    private string _historyMemorySummary = "Memory history collecting…";
    private string _historySampleSummary = "0 samples";
    private string _historyStatusText = "Collecting";
    private DateTimeOffset _lastHistoryRefresh = DateTimeOffset.MinValue;

    public MainWindowViewModel(IMystTiqApiClient api, IConnectionProfileStore profileStore, ILocalInstallationDiscoveryService localDiscovery, IMystTiqServiceDiscoveryService serviceDiscovery, ILocalManagementBootstrapper? localBootstrapper = null)
    {
        _api = api;
        _profileStore = profileStore;
        _localDiscovery = localDiscovery;
        _serviceDiscovery = serviceDiscovery;
        _localBootstrapper = localBootstrapper ?? new LocalManagementBootstrapper();

        foreach (var finding in _localDiagnostics.GetLocalMachineFindings())
            LocalMachineFindings.Add(finding);

        foreach (var profile in _profileStore.Load())
            Profiles.Add(profile);

        SelectedProfile = Profiles.FirstOrDefault() ?? ConnectionProfile.LocalDefault;

        ConnectCommand = new AsyncCommand(RefreshAsync, () => !IsBusy);
        DiscoverServicesCommand = new AsyncCommand(DiscoverServicesAsync, () => !IsBusy);
        RefreshMonitoringCommand = new AsyncCommand(RefreshMonitoringAsync, () => !IsBusy);
        RefreshConsoleViewCommand = new AsyncCommand(RefreshMonitoringAsync, () => !IsBusy);
        PauseConsoleCommand = new RelayCommand(ToggleConsolePause);
        ClearConsoleViewCommand = new RelayCommand(ClearConsoleView);
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
        RefreshFleetCommand = new AsyncCommand(RefreshFleetAsync, () => !IsBusy);
        CloneWorldCommand = new AsyncCommand(CloneWorldAsync, () => !IsBusy && ManagementApiConnected && !string.IsNullOrWhiteSpace(CloneNewProfileId));
        BackupAllCommand = new AsyncCommand(BackupAllAsync, () => !IsBusy && ManagementApiConnected);
        DoctorAllCommand = new AsyncCommand(DoctorAllAsync, () => !IsBusy && ManagementApiConnected);
        UpdateAllCommand = new AsyncCommand(UpdateAllAsync, () => !IsBusy && ManagementApiConnected);
        AnalyzeCrashesCommand = new AsyncCommand(AnalyzeCrashesAsync, () => !IsBusy && ManagementApiConnected);
        RefreshCrashHistoryCommand = new AsyncCommand(RefreshCrashHistoryAsync, () => !IsBusy);
        RefreshSaveToolsCommand = new AsyncCommand(() => RefreshSaveToolsAsync(false), () => !IsBusy);
        RunSaveToolsSelfTestCommand = new AsyncCommand(() => RefreshSaveToolsAsync(true), () => !IsBusy && ManagementApiConnected);
        RefreshPlayersCommand = new AsyncCommand(RefreshPlayersPageAsync, () => !IsBusy);
        LoadPlayerMetadataCommand = new AsyncCommand(LoadSelectedPlayerMetadataAsync, () => !IsBusy && SelectedPlayerRecord is not null);
        SavePlayerNotesCommand = new AsyncCommand(SaveSelectedPlayerNotesAsync, () => !IsBusy && SelectedPlayerRecord is not null);
        PreviewCharacterMigrationCommand = new AsyncCommand(PreviewCharacterMigrationAsync, () => !IsBusy && MigrationSourcePlayer is not null && MigrationDestinationPlayer is not null);
        ApplyCharacterMigrationCommand = new AsyncCommand(ApplyCharacterMigrationAsync, () => !IsBusy && MigrationPreview is { CanApply: true });
        AddPlayerWarningCommand = new AsyncCommand(AddSelectedPlayerWarningAsync, () => !IsBusy && SelectedPlayerRecord is not null && !string.IsNullOrWhiteSpace(NewPlayerWarning));
        KickSelectedPlayerCommand = new AsyncCommand(() => RunSelectedPlayerAdminActionAsync("kick"), () => !IsBusy && SelectedPlayerRecord?.Online == true);
        BanSelectedPlayerCommand = new AsyncCommand(() => RunSelectedPlayerAdminActionAsync("ban"), () => !IsBusy && SelectedPlayerRecord?.Online == true);
        WhisperSelectedPlayerCommand = new AsyncCommand(() => RunSelectedPlayerAdminActionAsync("whisper"), () => false);
        PromoteSelectedPlayerCommand = new AsyncCommand(() => RunSelectedPlayerAdminActionAsync("promote"), () => false);
        GiveItemSelectedPlayerCommand = new AsyncCommand(() => RunSelectedPlayerAdminActionAsync("give-item"), () => false);
        RefreshBackupsCommand = new AsyncCommand(RefreshBackupsAsync, () => !IsBusy);
        CreateBackupCommand = new AsyncCommand(CreateBackupAsync, () => !IsBusy && ManagementApiConnected);
        DeleteBackupCommand = new AsyncCommand(DeleteBackupAsync, () => !IsBusy && SelectedBackup is not null);
        RestoreBackupCommand = new AsyncCommand(RestoreBackupAsync, () => !IsBusy && SelectedBackup is not null && BackupRestoreConfirmed);
        VerifySelectedBackupCommand = new AsyncCommand(VerifySelectedBackupAsync, () => !IsBusy && SelectedBackup is not null);
        VerifyAllBackupsCommand = new AsyncCommand(VerifyAllBackupsAsync, () => !IsBusy && ManagementApiConnected);
        PreviewBackupRetentionCommand = new AsyncCommand(PreviewBackupRetentionAsync, () => !IsBusy && ManagementApiConnected);
        ApplyBackupRetentionCommand = new AsyncCommand(ApplyBackupRetentionAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(BackupRetentionToken));
        OpenBackupRootCommand = new RelayCommand(OpenBackupRoot, () => !string.IsNullOrWhiteSpace(BackupRootPath));
        LoadConfigurationCommand = new AsyncCommand(LoadConfigurationAsync, () => !IsBusy);
        SaveConfigurationCommand = new AsyncCommand(SaveConfigurationAsync, () => !IsBusy && ConfigLoaded);
        ValidateWorkspaceCommand = new RelayCommand(ValidateWorkspacePaths, () => ConfigLoaded);
        BootstrapLocalCommand = new AsyncCommand(BootstrapLocalAsync, () => !IsBusy && IsLocalProfile);
        LoadPalworldConfigurationCommand = new AsyncCommand(LoadPalworldConfigurationAsync, () => !IsBusy);
        SavePalworldConfigurationCommand = new AsyncCommand(SavePalworldConfigurationAsync, () => !IsBusy && PalworldConfigLoaded && PalworldConfigIsDirty && !PalworldConfigHasValidationErrors);
        ShowSimpleConfigCommand = new RelayCommand(() => SetConfigurationView(true));
        ShowAdvancedConfigCommand = new RelayCommand(() => SetConfigurationView(false));
        ApplyConfigPresetCommand = new RelayCommand(ApplySelectedConfigurationPreset, () => PalworldConfigLoaded && !IsBusy && !string.Equals(SelectedConfigPreset, "Custom", StringComparison.Ordinal));
        ResetConfigChangesCommand = new RelayCommand(ResetPalworldConfigurationChanges, () => PalworldConfigLoaded && PalworldConfigIsDirty && !IsBusy);
        GenerateServerNameCommand = new RelayCommand(GenerateServerName, () => PalworldConfigLoaded && !IsBusy);
        GenerateSetupServerNameCommand = new RelayCommand(() => SetupServerName = GenerateRandomServerName());
        RunDoctorCommand = new AsyncCommand(RunDoctorAsync, () => !IsBusy);
        RecheckDiagnosticCommand = new RelayCommand<DiagnosticFindingDto>(f => { if (!IsBusy) Dispatcher.UIThread.Post(async () => await RecheckDiagnosticAsync(f)); });
        FixDiagnosticCommand = new RelayCommand<DiagnosticFindingDto>(f => { if (!IsBusy && f is { CanFix: true }) Dispatcher.UIThread.Post(async () => await FixDiagnosticAsync(f)); });
        RunNetworkDiagnosticsCommand = new AsyncCommand(RunNetworkDiagnosticsAsync, () => !IsBusy);
        DiagnoseConnectionCommand = new AsyncCommand(DiagnoseConnectionAsync, () => !IsBusy && SelectedProfile is not null);
        RepairFirewallCommand = new AsyncCommand(RepairFirewallAsync, () => !IsBusy);
        RunWanReachabilityCommand = new AsyncCommand(RunWanReachabilityAsync, () => !IsBusy);
        RepairUpnpMappingCommand = new AsyncCommand(RepairUpnpMappingAsync, () => !IsBusy);
        OpenExternalPortCheckerCommand = new RelayCommand(OpenExternalPortChecker);
        RestartFromDiagnosticsCommand = new AsyncCommand(RestartFromDiagnosticsAsync, () => !IsBusy);
        RefreshEnvironmentCommand = new AsyncCommand(RefreshEnvironmentAsync, () => !IsBusy);
        VerifyEnvironmentCommand = new AsyncCommand(VerifyEnvironmentAsync, () => !IsBusy);
        InstallMissingEnvironmentCommand = new AsyncCommand(InstallMissingEnvironmentAsync, () => !IsBusy);
        CheckEnvironmentUpdatesCommand = new AsyncCommand(CheckEnvironmentUpdatesAsync, () => !IsBusy);
        CreateDefaultServerSettingsCommand = new AsyncCommand(CreateDefaultServerSettingsAsync, () => !IsBusy && SetupCreateConfirmed);
        EnvironmentActionCommand = new RelayCommand<EnvironmentChecklistItemDto>(RunEnvironmentAction);
        RefreshDistributionCommand = new AsyncCommand(RefreshDistributionAsync, () => !IsBusy);
        PreviewDistributionPlanCommand = new AsyncCommand(PreviewDistributionPlanAsync, () => !IsBusy);
        UpdatePalworldServerCommand = new AsyncCommand(UpdatePalworldServerAsync, () => !IsBusy);
        RefreshWorldExplorerCommand = new AsyncCommand(RefreshWorldExplorerAsync, () => !IsBusy);
        ValidateActiveWorldCommand = new AsyncCommand(ValidateActiveWorldAsync, () => !IsBusy && ManagementApiConnected);
        ApplyWorldTransactionCommand = new AsyncCommand(ApplyWorldTransactionAsync, () => !IsBusy && WorldTransactionConfirmed && !string.IsNullOrWhiteSpace(WorldPreviewToken));
        RefreshOperationsCommand = new AsyncCommand(RefreshOperationsAsync, () => !IsBusy && ManagementApiConnected);
        RefreshPlayerGuildExplorerCommand = new AsyncCommand(RefreshPlayerGuildExplorerAsync, () => !IsBusy);
        OpenSelectedGuildLeaderCommand = new AsyncCommand(OpenSelectedGuildLeaderAsync, () => !IsBusy && SelectedExplorerGuild is not null && !string.IsNullOrWhiteSpace(SelectedExplorerGuild.LeaderPlayerId));
        PreviewGuildOwnershipCommand = new AsyncCommand(PreviewGuildOwnershipAsync, () => !IsBusy && SelectedExplorerGuild is not null && !string.IsNullOrWhiteSpace(GuildOperationPlayerId));
        ApplyGuildOwnershipCommand = new AsyncCommand(ApplyGuildOwnershipAsync, () => !IsBusy && GuildOperationConfirmed && !string.IsNullOrWhiteSpace(GuildOperationPreviewToken));
        PreviewBaseTransferCommand = new AsyncCommand(PreviewBaseTransferAsync, () => !IsBusy && SelectedExplorerBase is not null && !string.IsNullOrWhiteSpace(BaseTransferTargetGuildId));
        ApplyBaseTransferCommand = new AsyncCommand(ApplyBaseTransferAsync, () => !IsBusy && BaseTransferConfirmed && !string.IsNullOrWhiteSpace(BaseTransferPreviewToken));
        PreviewBaseRecoveryCommand = new AsyncCommand(PreviewBaseRecoveryAsync, () => !IsBusy && SelectedExplorerBase is not null);
        ApplyBaseRecoveryCommand = new AsyncCommand(ApplyBaseRecoveryAsync, () => !IsBusy && BaseRecoveryConfirmed && !string.IsNullOrWhiteSpace(BaseRecoveryPreviewToken));
        RefreshModsCommand = new AsyncCommand(RefreshModsAsync, () => !IsBusy);
        VerifyModsCommand = new AsyncCommand(VerifyModsAsync, () => !IsBusy);
        EnableSelectedModCommand = new AsyncCommand(() => SetSelectedModEnabledAsync(true), () => !IsBusy);
        DisableSelectedModCommand = new AsyncCommand(() => SetSelectedModEnabledAsync(false), () => !IsBusy);
        DeleteSelectedModCommand = new AsyncCommand(DeleteSelectedModAsync, () => !IsBusy && SelectedMod is not null);
        RollbackSelectedModCommand = new AsyncCommand(RollbackSelectedModAsync, () => !IsBusy && SelectedMod is not null);
        EnableAllModsCommand = new AsyncCommand(() => SetAllModsEnabledAsync(true), () => !IsBusy);
        DisableAllModsCommand = new AsyncCommand(() => SetAllModsEnabledAsync(false), () => !IsBusy);
        RepairModsCommand = new AsyncCommand(RepairModsAsync, () => !IsBusy);
        ScanWorkshopModsCommand = new AsyncCommand(ScanWorkshopModsAsync, () => !IsBusy);
        ImportSelectedWorkshopModCommand = new AsyncCommand(ImportSelectedWorkshopModAsync, () => !IsBusy && SelectedWorkshopItem is not null && !SelectedWorkshopItem.AlreadyInstalled);
        ExportDoctorCommand = new RelayCommand(ExportDoctorReport, () => DoctorChecks.Count > 0);
        StartCommand = new AsyncCommand(StartServerAsync, () => !IsBusy && (ManagementApiConnected || LocalPalServerStatus == "Found"));
        StopCommand = new AsyncCommand(StopServerAsync, () => !IsBusy && ManagementApiConnected);
        RestartCommand = new AsyncCommand(RestartServerAsync, () => !IsBusy && ManagementApiConnected);
        SaveProfileCommand = new RelayCommand(SaveProfile);
        NewProfileCommand = new RelayCommand(BeginNewProfile);
        DeleteProfileCommand = new RelayCommand(DeleteSelectedProfile, () => SelectedProfile is not null && SelectedProfile.Id != ConnectionProfile.LocalDefault.Id);
        NavigateCommand = new RelayCommand<string>(Navigate);

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) =>
        {
            if (!AutoRefreshEnabled || IsBusy)
                return;

            if (SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id && ConnectionState != "Connected")
            {
                await RefreshLocalInstallationAsync();
                return;
            }

            if (ConnectionState != "Connected")
                return;

            await RefreshStatusPollingAsync();
            _refreshTick++;
        };
        _refreshTimer.Start();
        Dispatcher.UIThread.Post(async () => await InitializeLocalDashboardAsync());
    }

    public string ProductName => "MystTiq";
    public string Version => $"v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(4)}";
    public string PlatformText => OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsLinux() ? "Linux" : "Desktop";
    public string ProfileStorageText => $"Profiles: {_profileStore.StoragePath}";
    public string SecretPolicyText => "Bearer token is process-memory-only and is never written to the profile file.";
    public bool IsLocalProfile => SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id;

    public ObservableCollection<ConnectionProfile> Profiles { get; } = [];
    public ObservableCollection<DiscoveredMystTiqService> DiscoveredServices { get; } = [];
    public ObservableCollection<PlayerSnapshotDto> OnlinePlayers { get; } = [];
    public ObservableCollection<string> LogLines { get; } = [];
    public ObservableCollection<string> FilteredLogLines { get; } = [];
    public ObservableCollection<string> RconOutputLines { get; } = [];
    public IReadOnlyList<string> ConsoleSeverityOptions { get; } = ["All", "Errors", "Warnings", "Information"];
    public IReadOnlyList<string> ConsoleCategoryOptions { get; } = ["All", "MystTiq", "PalServer", "REST", "Mods"];
    public IReadOnlyList<string> RconPresetOptions { get; } = ["Info", "ShowPlayers", "Save", "Broadcast Server maintenance message", "Shutdown 60 Server restarting"];
    public ObservableCollection<string> ActivityLines { get; } = [];
    public ObservableCollection<NotificationItemDto> Notifications { get; } = [];
    public ObservableCollection<NotificationItemDto> FilteredNotifications { get; } = [];
    public ObservableCollection<AutomationRuleDto> AutomationRules { get; } = [];
    public ObservableCollection<AutomationRunRecordDto> AutomationRuns { get; } = [];
    public ObservableCollection<MystTiqPrincipalDto> Principals { get; } = [];
    public ObservableCollection<ServerProfileSummaryDto> FleetServers { get; } = [];
    public ObservableCollection<FleetActionResultDto> FleetActionResults { get; } = [];
    public IReadOnlyList<string> AutomationTriggerKinds { get; } = ["DailyTime", "Interval", "IdleEmpty"];
    public IReadOnlyList<string> AutomationActionKinds { get; } = ["CreateBackup", "StartServer", "StopServer", "RestartServer", "SendNotification", "SendRconCommand"];
    public IReadOnlyList<string> MystTiqRoles { get; } = ["Viewer", "Operator", "Admin", "Owner"];
    public ObservableCollection<CrashFindingDto> CrashFindings { get; } = [];
    public ObservableCollection<string> CrashIsolationPlan { get; } = [];
    public ObservableCollection<CrashAnalysisSnapshotDto> CrashHistory { get; } = [];
    public ObservableCollection<SaveToolsTestDto> SaveToolsTests { get; } = [];
    public ObservableCollection<SaveFileDto> SaveFiles { get; } = [];
    public ObservableCollection<MetricHistoryPoint> MetricHistory { get; } = [];
    public ObservableCollection<HistoricalMetricPointDto> ResourceHistory { get; } = [];
    public IReadOnlyList<string> HistoryRanges { get; } = ["1 Hour", "6 Hours", "24 Hours", "7 Days", "30 Days"];
    public ObservableCollection<string> DashboardActivityLines { get; } = [];
    public ObservableCollection<EnvironmentChecklistItemDto> EnvironmentItems { get; } = [];
    public ObservableCollection<BackupItemDto> BackupItems { get; } = [];
    public ObservableCollection<BackupRetentionItemDto> BackupRetentionItems { get; } = [];
    public ObservableCollection<WorldCandidateDto> WorldCandidates { get; } = [];
    public ObservableCollection<WorldFileDto> WorldFiles { get; } = [];
    public ObservableCollection<string> WorldIntegrityFindings { get; } = [];
    public ObservableCollection<WorldValidationFindingDto> WorldValidationFindings { get; } = [];
    public ObservableCollection<string> WorldTransactionPlanSteps { get; } = [];
    public ObservableCollection<WorldTransactionJournalDto> WorldTransactionHistory { get; } = [];
    public ObservableCollection<OperationRecordDto> RecentOperations { get; } = [];
    public ObservableCollection<PlayerExplorerItemDto> ExplorerPlayers { get; } = [];
    public ObservableCollection<PlayerExplorerItemDto> PlayerRecords { get; } = [];
    public ObservableCollection<PlayerExplorerItemDto> FilteredPlayerRecords { get; } = [];
    public ObservableCollection<PlayerWarningDto> SelectedPlayerWarnings { get; } = [];
    public ObservableCollection<GuildExplorerItemDto> ExplorerGuilds { get; } = [];
    public ObservableCollection<GuildExplorerItemDto> FilteredExplorerGuilds { get; } = [];
    public ObservableCollection<BaseExplorerItemDto> ExplorerBases { get; } = [];
    public ObservableCollection<BaseExplorerItemDto> FilteredExplorerBases { get; } = [];
    public ObservableCollection<string> PlayerGuildWarnings { get; } = [];
    public ObservableCollection<ModItemDto> ModItems { get; } = [];
    public IEnumerable<ModItemDto> ModsNeedingAttention => ModItems.Where(m => m.Health is not ("Healthy" or "Disabled" or "Active / Unverified"));
    public bool HasModsNeedingAttention => ModsNeedingAttention.Any();
    public ObservableCollection<WorkshopItemDto> WorkshopItems { get; } = [];
    public ObservableCollection<DoctorCheckDto> DoctorChecks { get; } = [];
    public ObservableCollection<DiagnosticFindingDto> DiagnosticFindings { get; } = [];
    public ObservableCollection<NetworkDiagnosticCheckDto> NetworkDiagnosticChecks { get; } = [];
    public ObservableCollection<WanReachabilityCheckDto> WanReachabilityChecks { get; } = [];
    public ObservableCollection<DiagnosticFindingDto> LocalConnectionFindings { get; } = [];
    public ObservableCollection<DiagnosticFindingDto> LocalMachineFindings { get; } = [];
    public ObservableCollection<PalworldSettingDto> PalworldSettings { get; } = [];
    public ObservableCollection<PalworldSettingDto> FilteredPalworldSettings { get; } = [];
    public ObservableCollection<PalworldSimpleSettingItem> SimplePalworldSettings { get; } = [];
    public ObservableCollection<string> PalworldConfigCategories { get; } = ["All Categories"];
    public IReadOnlyList<string> PalworldConfigPresets { get; } = ["Official / Vanilla", "Balanced QoL", "Relaxed QoL", "Custom"];
    public IReadOnlyList<string> PlayerViewFilters { get; } = ["All Players", "Online", "Known Saves", "Missing Saves"];
    public IReadOnlyList<string> PlayerAdminFilters { get; } = ["All Records", "Actionable Online", "Needs Review"];
    public IReadOnlyList<string> GuildStatusFilters { get; } = ["All Guilds", "Healthy", "Needs Review"];
    public IReadOnlyList<string> BaseStatusFilters { get; } = ["All Bases", "Healthy Owner", "Needs Review"];

    public PlayerSnapshotDto? SelectedOnlinePlayer
    {
        get => _selectedOnlinePlayer;
        set
        {
            if (!SetField(ref _selectedOnlinePlayer, value)) return;
            (KickSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (BanSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (WhisperSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (PromoteSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (GiveItemSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    public PlayerExplorerItemDto? SelectedPlayerRecord
    {
        get => _selectedPlayerRecord;
        set
        {
            if (!SetField(ref _selectedPlayerRecord, value)) return;
            SelectedPlayerWarnings.Clear();
            SelectedPlayerNotes = string.Empty;
            PlayerMetadataState = value is null ? "Select a player to load notes and warnings." : "Metadata not loaded.";
            RaisePlayerCommandStates();
        }
    }

    public string PlayerSearchText
    {
        get => _playerSearchText;
        set { if (SetField(ref _playerSearchText, value ?? string.Empty)) ApplyPlayerFilters(); }
    }

    public string SelectedPlayerViewFilter
    {
        get => _selectedPlayerViewFilter;
        set { if (SetField(ref _selectedPlayerViewFilter, value ?? "All Players")) ApplyPlayerFilters(); }
    }

    public string SelectedPlayerAdminFilter
    {
        get => _selectedPlayerAdminFilter;
        set { if (SetField(ref _selectedPlayerAdminFilter, value ?? "All Records")) ApplyPlayerFilters(); }
    }

    public string PlayersPageState { get => _playersPageState; private set => SetField(ref _playersPageState, value); }
    public string PlayersPageDetail { get => _playersPageDetail; private set => SetField(ref _playersPageDetail, value); }
    public string PlayerVisibleCountText { get => _playerVisibleCountText; private set => SetField(ref _playerVisibleCountText, value); }
    public string SelectedPlayerNotes { get => _selectedPlayerNotes; set => SetField(ref _selectedPlayerNotes, value ?? string.Empty); }
    public string NewPlayerWarning
    {
        get => _newPlayerWarning;
        set
        {
            if (!SetField(ref _newPlayerWarning, value ?? string.Empty)) return;
            (AddPlayerWarningCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }
    public string PlayerMetadataState { get => _playerMetadataState; private set => SetField(ref _playerMetadataState, value); }
    public PlayerExplorerItemDto? MigrationSourcePlayer { get => _migrationSourcePlayer; set { if (SetField(ref _migrationSourcePlayer, value)) (PreviewCharacterMigrationCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public PlayerExplorerItemDto? MigrationDestinationPlayer { get => _migrationDestinationPlayer; set { if (SetField(ref _migrationDestinationPlayer, value)) (PreviewCharacterMigrationCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public string MigrationState { get => _migrationState; private set => SetField(ref _migrationState, value); }
    public CharacterMigrationPreviewDto? MigrationPreview { get => _migrationPreview; private set { if (SetField(ref _migrationPreview, value)) (ApplyCharacterMigrationCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public IReadOnlyList<string> CharacterDispositionOptions { get; } = ["Keep", "Archive", "Delete", "Reset (not yet available)"];
    public string MigrationDisposition { get => _migrationDisposition; set => SetField(ref _migrationDisposition, value); }

    public ConnectionProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (!SetField(ref _selectedProfile, value) || value is null)
                return;

            ProfileName = value.Name;
            ServerUrl = value.BaseAddress.ToString().TrimEnd('/');
            CertificateSha256 = value.ServerCertificateSha256 ?? string.Empty;
            BearerToken = string.Empty;
            ManagementApiConnected = false;
            ConnectionState = "Not connected";
            WorkspaceState = IsLocalProfile
                ? "Local profile: browse and open actions use this computer."
                : "Remote profile: paths belong to the managed server; local browse/open actions are disabled.";
            RaisePropertyChanged(nameof(IsLocalProfile));
            RaiseWorkspaceSummaryProperties();
            (DeleteProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (BootstrapLocalCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string ProfileName { get => _profileName; set => SetField(ref _profileName, value); }
    public string ServerUrl { get => _serverUrl; set => SetField(ref _serverUrl, value); }
    public string BearerToken { get => _bearerToken; set => SetField(ref _bearerToken, value); }
    public string CertificateSha256 { get => _certificateSha256; set => SetField(ref _certificateSha256, value); }
    public string DiscoveryStateText { get => _discoveryStateText; private set => SetField(ref _discoveryStateText, value); }
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
    public string DashboardWorldText { get => _dashboardWorldText; private set => SetField(ref _dashboardWorldText, value); }
    public string DashboardBackupText { get => _dashboardBackupText; private set => SetField(ref _dashboardBackupText, value); }
    public string DashboardGuildText { get => _dashboardGuildText; private set => SetField(ref _dashboardGuildText, value); }
    public string DashboardModText { get => _dashboardModText; private set => SetField(ref _dashboardModText, value); }
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
            RaisePropertyChanged(nameof(HealthStateColor));
        }
    }
    public string DashboardHealthDetail { get => _dashboardHealthDetail; private set => SetField(ref _dashboardHealthDetail, value); }
    public bool IsHealthGlowAmber => IsServerTransitioning || (!IsServerTransitioning && DashboardHealthText == "DEGRADED");
    public bool IsHealthGlowRed => !IsServerTransitioning && DashboardHealthText == "ATTENTION";
    public bool IsHealthGlowGreen => !IsServerTransitioning && !IsHealthGlowRed && DashboardHealthText == "READY";
    public bool IsHealthGlowNeutral => !IsHealthGlowAmber && !IsHealthGlowRed && !IsHealthGlowGreen;
    // Text color follows the same state the card's glow follows, so the label never reads
    // green on a red card (or vice versa) -- matches whichever glow is currently active.
    public string HealthStateColor => IsHealthGlowRed ? "#FF8A93" : IsHealthGlowAmber ? "#FFD467" : IsHealthGlowGreen ? "#65D6A0" : "#9FB4C8";
    public string DashboardWorldPulseText { get => _dashboardWorldPulseText; private set => SetField(ref _dashboardWorldPulseText, value); }
    public string DashboardWorldNicknameText { get => _dashboardWorldNicknameText; private set => SetField(ref _dashboardWorldNicknameText, value); }
    public string DashboardWorldClockText { get => _dashboardWorldClockText; private set => SetField(ref _dashboardWorldClockText, value); }
    public string DashboardWorldClockDetailText { get => _dashboardWorldClockDetailText; private set => SetField(ref _dashboardWorldClockDetailText, value); }
    public string DashboardPulseSaveText { get => _dashboardPulseSaveText; private set => SetField(ref _dashboardPulseSaveText, value); }
    public string DashboardPulseBackupText { get => _dashboardPulseBackupText; private set => SetField(ref _dashboardPulseBackupText, value); }
    public string DashboardRestText { get => _dashboardRestText; private set => SetField(ref _dashboardRestText, value); }
    public string DashboardRconText { get => _dashboardRconText; private set => SetField(ref _dashboardRconText, value); }
    public string DashboardModsStripText { get => _dashboardModsStripText; private set => SetField(ref _dashboardModsStripText, value); }
    public string DashboardBackupStripText { get => _dashboardBackupStripText; private set => SetField(ref _dashboardBackupStripText, value); }
    public string DashboardServerNameText { get => _dashboardServerNameText; private set => SetField(ref _dashboardServerNameText, value); }
    public string DashboardServerDescriptionText { get => _dashboardServerDescriptionText; private set => SetField(ref _dashboardServerDescriptionText, value); }
    public string DashboardSessionText { get => _dashboardSessionText; private set => SetField(ref _dashboardSessionText, value); }
    public string DashboardPlayersSessionText { get => _dashboardPlayersSessionText; private set => SetField(ref _dashboardPlayersSessionText, value); }
    public string DashboardLastActivityText { get => _dashboardLastActivityText; private set => SetField(ref _dashboardLastActivityText, value); }
    public double CpuPercentValue { get => _cpuPercentValue; private set => SetField(ref _cpuPercentValue, value); }
    public double MemoryMbValue { get => _memoryMbValue; private set => SetField(ref _memoryMbValue, value); }
    public double MemoryScaleValue { get => _memoryScaleValue; private set => SetField(ref _memoryScaleValue, value); }
    public string SelectedHistoryRange
    {
        get => _selectedHistoryRange;
        set
        {
            if (!SetField(ref _selectedHistoryRange, value)) return;
            Dispatcher.UIThread.Post(async () => await RefreshHistoricalMetricsAsync());
        }
    }
    public string HistoryCpuSummary { get => _historyCpuSummary; private set => SetField(ref _historyCpuSummary, value); }
    public string HistoryMemorySummary { get => _historyMemorySummary; private set => SetField(ref _historyMemorySummary, value); }
    public string HistorySampleSummary { get => _historySampleSummary; private set => SetField(ref _historySampleSummary, value); }
    public string HistoryStatusText { get => _historyStatusText; private set => SetField(ref _historyStatusText, value); }

    public bool ManagementApiConnected
    {
        get => _managementApiConnected;
        private set
        {
            if (!SetField(ref _managementApiConnected, value)) return;
            (CreateBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (StartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (StopCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RestartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
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
    public bool CanManageAdmin => CurrentPrincipal is null || CurrentPrincipal.Role is "Admin" or "Owner";
    public bool CanManagePrincipals => CurrentPrincipal is null || CurrentPrincipal.Role == "Owner";

    public string AlertCenterState { get => _alertCenterState; private set => SetField(ref _alertCenterState, value); }
    public AlertRuleSetDto AlertRules { get => _alertRules; set => SetField(ref _alertRules, value ?? new()); }
    public DiskSpacePredictionDto? DiskSpacePrediction { get => _diskSpacePrediction; private set => SetField(ref _diskSpacePrediction, value); }
    public string FleetState { get => _fleetState; private set => SetField(ref _fleetState, value); }
    public string CloneNewProfileId { get => _cloneNewProfileId; set { if (SetField(ref _cloneNewProfileId, value ?? string.Empty)) (CloneWorldCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public string CloneNewProfileName { get => _cloneNewProfileName; set => SetField(ref _cloneNewProfileName, value ?? string.Empty); }
    public string CloneWorldStatusText { get => _cloneWorldStatusText; private set => SetField(ref _cloneWorldStatusText, value); }
    public string ActivityFileText { get => _activityFileText; private set => SetField(ref _activityFileText, value); }
    public string ActivityDetail { get => _activityDetail; private set => SetField(ref _activityDetail, value); }
    public string PlayerAdminStatusText { get => _playerAdminStatusText; private set => SetField(ref _playerAdminStatusText, value); }
    public string ModerationProviderStatusText { get => _moderationProviderStatusText; private set => SetField(ref _moderationProviderStatusText, value); }
    public string PlayerRegistrySummaryText { get => _playerRegistrySummaryText; private set => SetField(ref _playerRegistrySummaryText, value); }
    public string PlayerActionMessage { get => _playerActionMessage; set => SetField(ref _playerActionMessage, value); }
    public string PlayerActionItem { get => _playerActionItem; set => SetField(ref _playerActionItem, value); }

    public string ConnectionState
    {
        get => _connectionState;
        private set
        {
            if (!SetField(ref _connectionState, value)) return;
            (CreateBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (StartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (StopCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RestartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(IsServerGlowGreen));
            RaisePropertyChanged(nameof(IsServerGlowRed));
            RaisePropertyChanged(nameof(IsServerGlowAmber));
            RaisePropertyChanged(nameof(IsHealthGlowGreen));
            RaisePropertyChanged(nameof(IsHealthGlowRed));
            RaisePropertyChanged(nameof(IsHealthGlowAmber));
            RaisePropertyChanged(nameof(IsHealthGlowNeutral));
            RaisePropertyChanged(nameof(HealthStateColor));
        }
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
    public bool IsServerGlowAmber => IsServerTransitioning;
    public bool IsServerGlowGreen => !IsServerTransitioning && ServerState.Contains("Running", StringComparison.OrdinalIgnoreCase);
    public bool IsServerGlowRed => !IsServerTransitioning && !IsServerGlowGreen;
    public string ServiceState { get => _serviceState; private set => SetField(ref _serviceState, value); }
    public string Detail { get => _detail; private set => SetField(ref _detail, value); }
    public string NativePidText { get => _nativePidText; private set => SetField(ref _nativePidText, value); }
    public string ListenerText { get => _listenerText; private set => SetField(ref _listenerText, value); }
    public string LastObservedText { get => _lastObservedText; private set => SetField(ref _lastObservedText, value); }
    public string LastTransitionText { get => _lastTransitionText; private set => SetField(ref _lastTransitionText, value); }
    public string UptimeText { get => _uptimeText; private set => SetField(ref _uptimeText, value); }
    public string PlayersState { get => _playersState; private set => SetField(ref _playersState, value); }
    public string OnlinePlayerCountText { get => _onlinePlayerCountText; private set => SetField(ref _onlinePlayerCountText, value); }
    public string CpuText { get => _cpuText; private set => SetField(ref _cpuText, value); }
    public string MemoryText { get => _memoryText; private set => SetField(ref _memoryText, value); }
    public string ThreadCountText { get => _threadCountText; private set => SetField(ref _threadCountText, value); }
    public string MonitoringDetail { get => _monitoringDetail; private set => SetField(ref _monitoringDetail, value); }
    public string LogFileText { get => _logFileText; private set => SetField(ref _logFileText, value); }
    public string ConsoleSeverityFilter { get => _consoleSeverityFilter; set { if (SetField(ref _consoleSeverityFilter, value ?? "All")) ApplyConsoleFilter(); } }
    public string ConsoleCategoryFilter { get => _consoleCategoryFilter; set { if (SetField(ref _consoleCategoryFilter, value ?? "All")) ApplyConsoleFilter(); } }
    public string ConsoleSearchText { get => _consoleSearchText; set { if (SetField(ref _consoleSearchText, value ?? string.Empty)) ApplyConsoleFilter(); } }
    public bool HideRoutineRest { get => _hideRoutineRest; set { if (SetField(ref _hideRoutineRest, value)) ApplyConsoleFilter(); } }
    public bool ConsolePaused { get => _consolePaused; private set => SetField(ref _consolePaused, value); }
    public string ConsoleViewState { get => _consoleViewState; private set => SetField(ref _consoleViewState, value); }
    public string RconState { get => _rconState; private set => SetField(ref _rconState, value); }
    public string RconDetail { get => _rconDetail; private set => SetField(ref _rconDetail, value); }
    public bool RconConnected { get => _rconConnected; private set => SetField(ref _rconConnected, value); }
    public string RconCommandText { get => _rconCommandText; set { if (SetField(ref _rconCommandText, value ?? string.Empty)) (RconSendCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public string SelectedRconPreset { get => _selectedRconPreset; set { if (SetField(ref _selectedRconPreset, value ?? "Info")) RconCommandText = _selectedRconPreset; } }
    public BackupItemDto? SelectedBackup { get => _selectedBackup; set { if (SetField(ref _selectedBackup, value)) { BackupRestoreConfirmed = false; RaiseBackupCommandStates(); } } }
    public string BackupState { get => _backupState; private set => SetField(ref _backupState, value); }
    public string BackupTotalSizeText { get => _backupTotalSizeText; private set => SetField(ref _backupTotalSizeText, value); }
    public string BackupDetail { get => _backupDetail; private set => SetField(ref _backupDetail, value); }
    public string BackupRootPath { get => _backupRootPath; private set { if (SetField(ref _backupRootPath, value)) (OpenBackupRootCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }
    public bool BackupRestoreConfirmed { get => _backupRestoreConfirmed; set { if (SetField(ref _backupRestoreConfirmed, value)) RaiseBackupCommandStates(); } }
    public int BackupRetentionKeepLatest { get => _backupRetentionKeepLatest; set { if (SetField(ref _backupRetentionKeepLatest, Math.Clamp(value, 1, 1000))) InvalidateRetentionPreview(); } }
    public int BackupRetentionMaxAgeDays { get => _backupRetentionMaxAgeDays; set { if (SetField(ref _backupRetentionMaxAgeDays, Math.Clamp(value, 1, 3650))) InvalidateRetentionPreview(); } }
    public string BackupRetentionToken { get => _backupRetentionToken; private set { if (SetField(ref _backupRetentionToken, value)) (ApplyBackupRetentionCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public string BackupRetentionState { get => _backupRetentionState; private set => SetField(ref _backupRetentionState, value); }

    public bool ConfigLoaded { get => _configLoaded; private set => SetField(ref _configLoaded, value); }
    public bool ConfigApiEnabled { get => _configApiEnabled; set => SetField(ref _configApiEnabled, value); }
    public string ConfigBindAddress { get => _configBindAddress; set => SetField(ref _configBindAddress, value); }
    public int ConfigPort { get => _configPort; set => SetField(ref _configPort, value); }
    public int StartupTimeoutSeconds { get => _startupTimeoutSeconds; set => SetField(ref _startupTimeoutSeconds, value); }
    public int StopTimeoutSeconds { get => _stopTimeoutSeconds; set => SetField(ref _stopTimeoutSeconds, value); }
    public int ServicePollSeconds { get => _servicePollSeconds; set => SetField(ref _servicePollSeconds, value); }
    public int RecoveryBackoffSeconds { get => _recoveryBackoffSeconds; set => SetField(ref _recoveryBackoffSeconds, value); }
    public int MaximumRecoveryAttempts { get => _maximumRecoveryAttempts; set => SetField(ref _maximumRecoveryAttempts, value); }
    public int RecoveryWindowSeconds { get => _recoveryWindowSeconds; set => SetField(ref _recoveryWindowSeconds, value); }
    public string ConfigServerRoot { get => _configServerRoot; set => SetField(ref _configServerRoot, value); }
    public string ConfigSteamCmdPath { get => _configSteamCmdPath; set => SetField(ref _configSteamCmdPath, value); }
    public string ConfigBackupRoot { get => _configBackupRoot; set => SetField(ref _configBackupRoot, value); }
    public string ConfigRuntimeRoot { get => _configRuntimeRoot; set => SetField(ref _configRuntimeRoot, value); }
    public string ConfigLaunchArguments { get => _configLaunchArguments; set => SetField(ref _configLaunchArguments, value); }
    public string ConfigSecurityText { get => _configSecurityText; private set => SetField(ref _configSecurityText, value); }
    public string ConfigState { get => _configState; private set => SetField(ref _configState, value); }
    public string WorkspaceState { get => _workspaceState; private set => SetField(ref _workspaceState, value); }
    public string DoctorStatus { get => _doctorStatus; private set => SetField(ref _doctorStatus, value); }
    public string DoctorSummary { get => _doctorSummary; private set => SetField(ref _doctorSummary, value); }
    public string DoctorCheckedAt { get => _doctorCheckedAt; private set => SetField(ref _doctorCheckedAt, value); }
    public string DoctorExportPath { get => _doctorExportPath; private set => SetField(ref _doctorExportPath, value); }
    public DiagnosticsReportDto? LatestDiagnosticsReport { get => _latestDiagnosticsReport; private set => SetField(ref _latestDiagnosticsReport, value); }
    public string DiagnosticsReportDetail { get => _diagnosticsReportDetail; private set => SetField(ref _diagnosticsReportDetail, value); }
    public string DistributionState { get => _distributionState; private set => SetField(ref _distributionState, value); }
    public string DistributionDetail { get => _distributionDetail; private set => SetField(ref _distributionDetail, value); }
    public string SteamCmdState { get => _steamCmdState; private set => SetField(ref _steamCmdState, value); }
    public string ServerInstallState { get => _serverInstallState; private set => SetField(ref _serverInstallState, value); }
    public string DistributionPlatform { get => _distributionPlatform; private set => SetField(ref _distributionPlatform, value); }
    public string DistributionPlanText { get => _distributionPlanText; private set => SetField(ref _distributionPlanText, value); }
    public string DistributionOutputText { get => _distributionOutputText; private set => SetField(ref _distributionOutputText, value); }
    public bool ValidateServerFiles { get => _validateServerFiles; set => SetField(ref _validateServerFiles, value); }
    public string WorldExplorerState { get => _worldExplorerState; private set => SetField(ref _worldExplorerState, value); }
    public string WorldExplorerDetail { get => _worldExplorerDetail; private set => SetField(ref _worldExplorerDetail, value); }
    public string ActiveWorldIdText { get => _activeWorldIdText; private set => SetField(ref _activeWorldIdText, value); }
    public string WorldCountText { get => _worldCountText; private set => SetField(ref _worldCountText, value); }
    public string WorldFileCountText { get => _worldFileCountText; private set => SetField(ref _worldFileCountText, value); }
    public string WorldPlayerSaveCountText { get => _worldPlayerSaveCountText; private set => SetField(ref _worldPlayerSaveCountText, value); }
    public string WorldSizeText { get => _worldSizeText; private set => SetField(ref _worldSizeText, value); }
    public string WorldSaveRootText { get => _worldSaveRootText; private set => SetField(ref _worldSaveRootText, value); }
    public string WorldSaveDataCountText { get => _worldSaveDataCountText; private set => SetField(ref _worldSaveDataCountText, value); }
    public string WorldDiagnosticCountText { get => _worldDiagnosticCountText; private set => SetField(ref _worldDiagnosticCountText, value); }
    public string WorldEmptyCountText { get => _worldEmptyCountText; private set => SetField(ref _worldEmptyCountText, value); }
    public string WorldAgeRangeText { get => _worldAgeRangeText; private set => SetField(ref _worldAgeRangeText, value); }
    public string WorldIntegrityState { get => _worldIntegrityState; private set => SetField(ref _worldIntegrityState, value); }
    public string WorldInspectorPlayerCountText { get => _worldInspectorPlayerCountText; private set => SetField(ref _worldInspectorPlayerCountText, value); }
    public string WorldInspectorGuildCountText { get => _worldInspectorGuildCountText; private set => SetField(ref _worldInspectorGuildCountText, value); }
    public string WorldInspectorBaseCountText { get => _worldInspectorBaseCountText; private set => SetField(ref _worldInspectorBaseCountText, value); }
    public WorldFileDto? SelectedWorldFile { get => _selectedWorldFile; set => SetField(ref _selectedWorldFile, value); }
    public string WorldValidationState { get => _worldValidationState; private set => SetField(ref _worldValidationState, value); }
    public string WorldTransactionState { get => _worldTransactionState; private set => SetField(ref _worldTransactionState, value); }
    public IReadOnlyList<string> WorldTransactionModes { get; } = ["world-import", "player-recovery"];
    public string WorldTransactionMode { get => _worldTransactionMode; set => SetField(ref _worldTransactionMode, value ?? "world-import"); }
    public string WorldPreviewToken { get => _worldPreviewToken; private set { if (SetField(ref _worldPreviewToken, value)) (ApplyWorldTransactionCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public bool WorldTransactionConfirmed { get => _worldTransactionConfirmed; set { if (SetField(ref _worldTransactionConfirmed, value)) (ApplyWorldTransactionCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public string PlayerGuildState { get => _playerGuildState; private set => SetField(ref _playerGuildState, value); }
    public string PlayerGuildDetail { get => _playerGuildDetail; private set => SetField(ref _playerGuildDetail, value); }
    public string SemanticStateText { get => _semanticStateText; private set => SetField(ref _semanticStateText, value); }
    public string SemanticSourceText { get => _semanticSourceText; private set => SetField(ref _semanticSourceText, value); }
    public string PlayerRecordCountText { get => _playerRecordCountText; private set => SetField(ref _playerRecordCountText, value); }
    public string GuildRecordCountText { get => _guildRecordCountText; private set => SetField(ref _guildRecordCountText, value); }
    public PlayerExplorerItemDto? SelectedExplorerPlayer
    {
        get => _selectedExplorerPlayer;
        set
        {
            if (!SetField(ref _selectedExplorerPlayer, value)) return;
            (KickSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (BanSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (WhisperSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (PromoteSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (GiveItemSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }
    public GuildExplorerItemDto? SelectedExplorerGuild
    {
        get => _selectedExplorerGuild;
        set
        {
            if (!SetField(ref _selectedExplorerGuild, value)) return;
            (OpenSelectedGuildLeaderCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (PreviewGuildOwnershipCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            GuildOperationPreviewToken = string.Empty;
            GuildOperationConfirmed = false;
            GuildOperationStatusText = "Select a guild, choose an operation, and enter the target player's ID.";
        }
    }
    public BaseExplorerItemDto? SelectedExplorerBase
    {
        get => _selectedExplorerBase;
        set
        {
            if (!SetField(ref _selectedExplorerBase, value)) return;
            (PreviewBaseTransferCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            BaseTransferPreviewToken = string.Empty;
            BaseTransferConfirmed = false;
            BaseTransferStatusText = "Select a base above and enter the target guild's ID.";
            (PreviewBaseRecoveryCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            BaseRecoveryPreviewToken = string.Empty;
            BaseRecoveryConfirmed = false;
            BaseRecoveryStatusText = "Select a base above, then preview to see how many owned records will be removed.";
        }
    }
    public string GuildSearchText { get => _guildSearchText; set { if (SetField(ref _guildSearchText, value ?? string.Empty)) ApplyGuildFilters(); } }
    public string SelectedGuildStatusFilter { get => _selectedGuildStatusFilter; set { if (SetField(ref _selectedGuildStatusFilter, value ?? "All Guilds")) ApplyGuildFilters(); } }
    public string BaseSearchText { get => _baseSearchText; set { if (SetField(ref _baseSearchText, value ?? string.Empty)) ApplyBaseFilters(); } }
    public string SelectedBaseStatusFilter { get => _selectedBaseStatusFilter; set { if (SetField(ref _selectedBaseStatusFilter, value ?? "All Bases")) ApplyBaseFilters(); } }
    public string GuildVisibleCountText { get => _guildVisibleCountText; private set => SetField(ref _guildVisibleCountText, value); }
    public string BaseVisibleCountText { get => _baseVisibleCountText; private set => SetField(ref _baseVisibleCountText, value); }
    public IReadOnlyList<string> GuildOperationTypes { get; } = ["Claim Orphaned Guild", "Transfer Leadership", "Add Player to Guild", "Remove Broken Member"];
    public string GuildOperationType
    {
        get => _guildOperationType;
        set
        {
            if (!SetField(ref _guildOperationType, value ?? "Claim Orphaned Guild")) return;
            GuildOperationPreviewToken = string.Empty;
            GuildOperationConfirmed = false;
            GuildOperationStatusText = "Select a guild, choose an operation, and enter the target player's ID.";
        }
    }
    public string GuildOperationPlayerId
    {
        get => _guildOperationPlayerId;
        set { if (SetField(ref _guildOperationPlayerId, value ?? string.Empty)) (PreviewGuildOwnershipCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }
    public string GuildOperationPreviewToken
    {
        get => _guildOperationPreviewToken;
        private set { if (SetField(ref _guildOperationPreviewToken, value)) (ApplyGuildOwnershipCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }
    public bool GuildOperationConfirmed
    {
        get => _guildOperationConfirmed;
        set { if (SetField(ref _guildOperationConfirmed, value)) (ApplyGuildOwnershipCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }
    public string GuildOperationStatusText { get => _guildOperationStatusText; private set => SetField(ref _guildOperationStatusText, value); }
    private static string GuildOperationApiName(string displayName) => displayName switch
    {
        "Transfer Leadership" => "transfer-leadership",
        "Add Player to Guild" => "add-player",
        "Remove Broken Member" => "remove-broken-member",
        _ => "claim"
    };
    public string BaseTransferTargetGuildId
    {
        get => _baseTransferTargetGuildId;
        set { if (SetField(ref _baseTransferTargetGuildId, value ?? string.Empty)) (PreviewBaseTransferCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }
    public string BaseTransferPreviewToken
    {
        get => _baseTransferPreviewToken;
        private set { if (SetField(ref _baseTransferPreviewToken, value)) (ApplyBaseTransferCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }
    public bool BaseTransferConfirmed
    {
        get => _baseTransferConfirmed;
        set { if (SetField(ref _baseTransferConfirmed, value)) (ApplyBaseTransferCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }
    public string BaseTransferStatusText { get => _baseTransferStatusText; private set => SetField(ref _baseTransferStatusText, value); }
    public string BaseRecoveryPreviewToken
    {
        get => _baseRecoveryPreviewToken;
        private set { if (SetField(ref _baseRecoveryPreviewToken, value)) (ApplyBaseRecoveryCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }
    public bool BaseRecoveryConfirmed
    {
        get => _baseRecoveryConfirmed;
        set { if (SetField(ref _baseRecoveryConfirmed, value)) (ApplyBaseRecoveryCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }
    public string BaseRecoveryStatusText { get => _baseRecoveryStatusText; private set => SetField(ref _baseRecoveryStatusText, value); }
    public string ModState { get => _modState; private set => SetField(ref _modState, value); }
    public string ModSummary { get => _modSummary; private set => SetField(ref _modSummary, value); }
    public string ModHealth { get => _modHealth; private set => SetField(ref _modHealth, value); }
    public string ModInstalledText { get => _modInstalledText; private set => SetField(ref _modInstalledText, value); }
    public string ModConfirmedText { get => _modConfirmedText; private set => SetField(ref _modConfirmedText, value); }
    public string ModUnverifiedText { get => _modUnverifiedText; private set => SetField(ref _modUnverifiedText, value); }
    public string ModDisabledText { get => _modDisabledText; private set => SetField(ref _modDisabledText, value); }
    public string ModIssuesText { get => _modIssuesText; private set => SetField(ref _modIssuesText, value); }
    public string Ue4ssHealth { get => _ue4ssHealth; private set => SetField(ref _ue4ssHealth, value); }
    public string Ue4ssDetection { get => _ue4ssDetection; private set => SetField(ref _ue4ssDetection, value); }
    public string Ue4ssActiveRoot { get => _ue4ssActiveRoot; private set => SetField(ref _ue4ssActiveRoot, value); }
    public string Ue4ssRuntimeRoot { get => _ue4ssRuntimeRoot; private set => SetField(ref _ue4ssRuntimeRoot, value); }
    public string Ue4ssWarning { get => _ue4ssWarning; private set => SetField(ref _ue4ssWarning, value); }
    public string Ue4ssInstalledVersion { get => _ue4ssInstalledVersion; private set => SetField(ref _ue4ssInstalledVersion, value); }
    public IReadOnlyList<string> Ue4ssForkOptions { get; } = ["Palworld Fork", "Official Upstream"];
    public string SelectedUe4ssFork { get => _selectedUe4ssFork; set => SetField(ref _selectedUe4ssFork, value ?? "Palworld Fork"); }
    public ModItemDto? SelectedMod { get => _selectedMod; set { if (SetField(ref _selectedMod, value)) { (DeleteSelectedModCommand as AsyncCommand)?.RaiseCanExecuteChanged(); (RollbackSelectedModCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } } }
    public IReadOnlyList<string> ModInstallTypes { get; } = ["PAK", "UE4SS"];
    public string ModInstallType { get => _modInstallType; set => SetField(ref _modInstallType, value ?? "PAK"); }
    public string ModInstallPackage { get => _modInstallPackage; set => SetField(ref _modInstallPackage, value ?? string.Empty); }
    public WorkshopItemDto? SelectedWorkshopItem { get => _selectedWorkshopItem; set { if (SetField(ref _selectedWorkshopItem, value)) (ImportSelectedWorkshopModCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }
    public string WorkshopScanState { get => _workshopScanState; private set => SetField(ref _workshopScanState, value); }

    public string StatusBarText { get => _statusBarText; private set => SetField(ref _statusBarText, value); }
    public string StatusBarObservedText { get => _statusBarObservedText; private set => SetField(ref _statusBarObservedText, value); }

    public bool AutoRefreshEnabled
    {
        get => _autoRefreshEnabled;
        set => SetField(ref _autoRefreshEnabled, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetField(ref _isBusy, value))
                return;

            StatusBarText = value ? "Working…" : (ConnectionState == "Connected" ? $"Connected — {ServerState}" : ConnectionState);

            (ConnectCommand as AsyncCommand)?.RaiseCanExecuteChanged();
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
            (InstallMissingEnvironmentCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (CheckEnvironmentUpdatesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (CreateDefaultServerSettingsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (KickSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (BanSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (WhisperSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (PromoteSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (GiveItemSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (LoadPlayerMetadataCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (SavePlayerNotesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (AddPlayerWarningCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (OpenSelectedGuildLeaderCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RefreshBackupsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (CreateBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RestoreBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (VerifySelectedBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (VerifyAllBackupsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (PreviewBackupRetentionCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ApplyBackupRetentionCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DeleteSelectedModCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RollbackSelectedModCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (EnableAllModsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (DisableAllModsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RepairModsCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ValidateActiveWorldCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ApplyWorldTransactionCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (LoadConfigurationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (SaveConfigurationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (LoadPalworldConfigurationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (SavePalworldConfigurationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (StartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (StopCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (RestartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    public NavigationPage SelectedPage
    {
        get => _selectedPage;
        private set
        {
            if (!SetField(ref _selectedPage, value))
                return;

            RaisePropertyChanged(nameof(PageTitle));
            RaisePropertyChanged(nameof(PageSubtitle));
            RaisePageVisibility();
        }
    }

    public string PageTitle => SelectedPage switch
    {
        NavigationPage.ServerSetup => "Server Setup", NavigationPage.ModDashboard => "MOD Dashboard", NavigationPage.ModLibrary => "MOD Library",
        NavigationPage.Ue4ss => "UE4SS", NavigationPage.UpdateCenter => "Update Center", NavigationPage.Doctor => "Server Doctor",
        NavigationPage.CrashAnalyzer => "Crash Analyzer", NavigationPage.SaveTools => "Palworld Save Tools", NavigationPage.DiagnosticsCenter => "Diagnostics Center",
        NavigationPage.WorldTransactions => "World Validator & Recovery", NavigationPage.ActivityAudit => "Activity & Audit", NavigationPage.Notifications => "Notifications",
        NavigationPage.Automation => "Automation", NavigationPage.Security => "Security", NavigationPage.AlertCenter => "Alert Center",
        NavigationPage.Fleet => "Fleet", _ => SelectedPage.ToString()
    };
    public string PageSubtitle => SelectedPage switch
    {
        NavigationPage.Dashboard => "Monitor resources, control the server, and follow live activity.",
        NavigationPage.ServerSetup => "Install, verify, and maintain the components required to run your Palworld dedicated server.",
        NavigationPage.Configuration => "Server configuration and lifecycle settings",
        NavigationPage.Backups => "Create, inspect, delete and safely restore backups",
        NavigationPage.Console => "Runtime monitoring and PalServer log output",
        NavigationPage.Workspace => "Server workspace paths and managed configuration",
        NavigationPage.Inspector => "Read-only active-world and save-file inventory",
        NavigationPage.WorldTransactions => "Validate, preview, back up, transact, verify and audit world recovery",
        NavigationPage.Players => "Live sessions, known saves, player notes, warnings, and audited administration",
        NavigationPage.Bases => "Authoritative guild-owned base references and recovery capability planning",
        NavigationPage.Guilds => "Guild identity, leadership, membership, health, and base-reference evidence",
        NavigationPage.ModDashboard => "MOD / UE4SS health and runtime evidence",
        NavigationPage.ModLibrary => "Installed MOD inventory and controls",
        NavigationPage.Ue4ss => "UE4SS runtime evidence and management",
        NavigationPage.UpdateCenter => "Palworld server installation evidence and SteamCMD update/validation",
        NavigationPage.Doctor => "Production health evidence and repair recommendations",
        NavigationPage.CrashAnalyzer => "Evidence-backed crash history, stability review and isolation guidance",
        NavigationPage.SaveTools => "Server-side Python, converter, Oodle and read-only save diagnostics",
        NavigationPage.DiagnosticsCenter => "Network connectivity, firewall and socket ownership diagnostics",
        NavigationPage.Settings => "Connection profiles and desktop settings",
        NavigationPage.ActivityAudit => "Runtime activity and audit evidence", NavigationPage.Notifications => "Persistent alerts, read state, pins and dismissals",
        NavigationPage.Automation => "Scheduled backups, lifecycle actions and notifications",
        NavigationPage.Security => "API principals, roles and access",
        NavigationPage.AlertCenter => "Threshold alerts and disk-space prediction",
        NavigationPage.Fleet => "Every configured server profile, with fleet-wide backup, doctor and update actions",
        _ => string.Empty
    };

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
    public bool IsServerGroupSelected => SelectedPage is NavigationPage.ServerSetup or NavigationPage.Configuration or NavigationPage.Backups or NavigationPage.Console or NavigationPage.Workspace;
    public bool IsWorldGroupSelected => SelectedPage is NavigationPage.Inspector or NavigationPage.WorldTransactions or NavigationPage.Players or NavigationPage.Bases or NavigationPage.Guilds;
    public bool IsModsGroupSelected => SelectedPage is NavigationPage.ModDashboard or NavigationPage.ModLibrary or NavigationPage.Ue4ss;
    public bool IsToolsGroupSelected => SelectedPage is NavigationPage.UpdateCenter or NavigationPage.Doctor or NavigationPage.CrashAnalyzer or NavigationPage.SaveTools or NavigationPage.DiagnosticsCenter;
    public bool IsSystemGroupSelected => SelectedPage is NavigationPage.Settings or NavigationPage.Notifications or NavigationPage.ActivityAudit or NavigationPage.Automation or NavigationPage.Security or NavigationPage.AlertCenter;
    public bool IsV5HomeCategory => SelectedPage == NavigationPage.Dashboard;
    public bool IsV5ServerCategory => SelectedPage is NavigationPage.ServerSetup or NavigationPage.Configuration or NavigationPage.Console or NavigationPage.Workspace;
    public bool IsV5WorldCategory => SelectedPage is NavigationPage.Inspector or NavigationPage.WorldTransactions or NavigationPage.Players or NavigationPage.Bases or NavigationPage.Guilds;
    public bool IsV5BackupsCategory => SelectedPage == NavigationPage.Backups;
    public bool IsV5ModsCategory => IsModsGroupSelected;
    public bool IsV5ToolsCategory => IsToolsGroupSelected;
    public bool IsV5SystemCategory => SelectedPage is NavigationPage.Settings or NavigationPage.ActivityAudit or NavigationPage.Notifications or NavigationPage.Automation or NavigationPage.Security or NavigationPage.AlertCenter;

    public ICommand ConnectCommand { get; }
    public ICommand DiscoverServicesCommand { get; }
    public ICommand RefreshMonitoringCommand { get; }
    public ICommand RefreshConsoleViewCommand { get; }
    public ICommand PauseConsoleCommand { get; }
    public ICommand ClearConsoleViewCommand { get; }
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
    public ICommand RefreshFleetCommand { get; }
    public ICommand CloneWorldCommand { get; }
    public ICommand BackupAllCommand { get; }
    public ICommand DoctorAllCommand { get; }
    public ICommand UpdateAllCommand { get; }
    public ICommand AnalyzeCrashesCommand { get; }
    public ICommand RefreshCrashHistoryCommand { get; }
    public ICommand RefreshSaveToolsCommand { get; }
    public ICommand RunSaveToolsSelfTestCommand { get; }
    public ICommand RefreshPlayersCommand { get; }
    public ICommand LoadPlayerMetadataCommand { get; }
    public ICommand SavePlayerNotesCommand { get; }
    public ICommand PreviewCharacterMigrationCommand { get; }
    public ICommand ApplyCharacterMigrationCommand { get; }
    public ICommand AddPlayerWarningCommand { get; }
    public ICommand KickSelectedPlayerCommand { get; }
    public ICommand BanSelectedPlayerCommand { get; }
    public ICommand WhisperSelectedPlayerCommand { get; }
    public ICommand PromoteSelectedPlayerCommand { get; }
    public ICommand GiveItemSelectedPlayerCommand { get; }
    public ICommand RefreshBackupsCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand DeleteBackupCommand { get; }
    public ICommand RestoreBackupCommand { get; }
    public ICommand VerifySelectedBackupCommand { get; }
    public ICommand VerifyAllBackupsCommand { get; }
    public ICommand PreviewBackupRetentionCommand { get; }
    public ICommand ApplyBackupRetentionCommand { get; }
    public ICommand OpenBackupRootCommand { get; }
    public ICommand LoadConfigurationCommand { get; }
    public ICommand SaveConfigurationCommand { get; }
    public ICommand LoadPalworldConfigurationCommand { get; }
    public ICommand SavePalworldConfigurationCommand { get; }
    public ICommand ShowSimpleConfigCommand { get; }
    public ICommand ShowAdvancedConfigCommand { get; }
    public ICommand ApplyConfigPresetCommand { get; }
    public ICommand ResetConfigChangesCommand { get; }
    public ICommand GenerateServerNameCommand { get; }
    public ICommand GenerateSetupServerNameCommand { get; }
    public ICommand RunDoctorCommand { get; }
    public ICommand RecheckDiagnosticCommand { get; }
    public ICommand FixDiagnosticCommand { get; }
    public ICommand RefreshEnvironmentCommand { get; }
    public ICommand VerifyEnvironmentCommand { get; }
    public ICommand InstallMissingEnvironmentCommand { get; }
    public ICommand CheckEnvironmentUpdatesCommand { get; }
    public ICommand CreateDefaultServerSettingsCommand { get; }
    public ICommand EnvironmentActionCommand { get; }
    public ICommand RefreshDistributionCommand { get; }
    public ICommand PreviewDistributionPlanCommand { get; }
    public ICommand UpdatePalworldServerCommand { get; }
    public ICommand RefreshWorldExplorerCommand { get; }
    public ICommand ValidateActiveWorldCommand { get; }
    public ICommand ApplyWorldTransactionCommand { get; }
    public ICommand RefreshOperationsCommand { get; }
    public ICommand RefreshPlayerGuildExplorerCommand { get; }
    public ICommand OpenSelectedGuildLeaderCommand { get; }
    public ICommand PreviewGuildOwnershipCommand { get; }
    public ICommand ApplyGuildOwnershipCommand { get; }
    public ICommand PreviewBaseTransferCommand { get; }
    public ICommand ApplyBaseTransferCommand { get; }
    public ICommand PreviewBaseRecoveryCommand { get; }
    public ICommand ApplyBaseRecoveryCommand { get; }
    public ICommand RefreshModsCommand { get; }
    public ICommand VerifyModsCommand { get; }
    public ICommand EnableSelectedModCommand { get; }
    public ICommand DisableSelectedModCommand { get; }
    public ICommand DeleteSelectedModCommand { get; }
    public ICommand RollbackSelectedModCommand { get; }
    public ICommand EnableAllModsCommand { get; }
    public ICommand DisableAllModsCommand { get; }
    public ICommand RepairModsCommand { get; }
    public ICommand ScanWorkshopModsCommand { get; }
    public ICommand ImportSelectedWorkshopModCommand { get; }
    public ICommand ExportDoctorCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand SaveProfileCommand { get; }
    public ICommand NewProfileCommand { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand NavigateCommand { get; }

    public string CrashAnalyzerState { get => _crashAnalyzerState; private set => SetField(ref _crashAnalyzerState, value); }
    public string SaveToolsState { get => _saveToolsState; private set => SetField(ref _saveToolsState, value); }
    public string SaveToolsPaths { get => _saveToolsPaths; private set => SetField(ref _saveToolsPaths, value); }
    public SaveFileDto? SelectedSaveFile { get => _selectedSaveFile; set => SetField(ref _selectedSaveFile, value); }

    public string PalworldConfigState { get => _palworldConfigState; private set => SetField(ref _palworldConfigState, value); }
    public string PalworldConfigPath { get => _palworldConfigPath; private set => SetField(ref _palworldConfigPath, value); }
    public bool PalworldConfigLoaded
    {
        get => _palworldConfigLoaded;
        private set
        {
            if (!SetField(ref _palworldConfigLoaded, value)) return;
            (SavePalworldConfigurationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (ApplyConfigPresetCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ResetConfigChangesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateServerNameCommand as RelayCommand)?.RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(WorkspaceHealthText));
        }
    }

    public bool IsConfigSimpleView => _isConfigSimpleView;
    public bool IsConfigAdvancedView => !_isConfigSimpleView;
    public PalworldSettingDto? ServerNameSetting => PalworldSettings.FirstOrDefault(x => x.Name.Equals("ServerName", StringComparison.OrdinalIgnoreCase));
    public PalworldSettingDto? ServerDescriptionSetting => PalworldSettings.FirstOrDefault(x => x.Name.Equals("ServerDescription", StringComparison.OrdinalIgnoreCase));
    public string ConfigSearchText
    {
        get => _configSearchText;
        set { if (SetField(ref _configSearchText, value)) ApplyPalworldConfigurationFilter(); }
    }
    public string SelectedConfigCategory
    {
        get => _selectedConfigCategory;
        set { if (SetField(ref _selectedConfigCategory, value ?? "All Categories")) ApplyPalworldConfigurationFilter(); }
    }
    public string SelectedConfigPreset
    {
        get => _selectedConfigPreset;
        set { if (SetField(ref _selectedConfigPreset, value ?? "Custom")) (ApplyConfigPresetCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }
    public string PalworldConfigDirtyText { get => _palworldConfigDirtyText; private set => SetField(ref _palworldConfigDirtyText, value); }
    public string PalworldConfigValidationText { get => _palworldConfigValidationText; private set => SetField(ref _palworldConfigValidationText, value); }
    public bool PalworldConfigHasValidationErrors { get => _palworldConfigHasValidationErrors; private set => SetField(ref _palworldConfigHasValidationErrors, value); }
    public bool PalworldConfigIsDirty => PalworldSettings.Any(x => x.IsDirty);

    public string EnvironmentHealthText { get => _environmentHealthText; private set => SetField(ref _environmentHealthText, value); }
    public string SetupOperationState { get => _setupOperationState; private set => SetField(ref _setupOperationState, value); }
    public string SetupOperationTitle { get => _setupOperationTitle; private set => SetField(ref _setupOperationTitle, value); }
    public string SetupOperationDetail { get => _setupOperationDetail; private set => SetField(ref _setupOperationDetail, value); }
    public string SetupRecentActivity { get => _setupRecentActivity; private set => SetField(ref _setupRecentActivity, value); }
    public double SetupOperationProgress { get => _setupOperationProgress; private set { if (SetField(ref _setupOperationProgress, value)) RaisePropertyChanged(nameof(SetupOperationProgressText)); } }
    public string SetupOperationProgressText => $"{SetupOperationProgress:0}%";
    public string SetupServerName { get => _setupServerName; set => SetField(ref _setupServerName, value ?? string.Empty); }
    public string SetupServerDescription { get => _setupServerDescription; set => SetField(ref _setupServerDescription, value ?? string.Empty); }
    public string SetupAdminPassword { get => _setupAdminPassword; set => SetField(ref _setupAdminPassword, value ?? string.Empty); }
    public string SetupServerPassword { get => _setupServerPassword; set => SetField(ref _setupServerPassword, value ?? string.Empty); }
    public string SetupMaximumPlayers { get => _setupMaximumPlayers; set => SetField(ref _setupMaximumPlayers, value ?? string.Empty); }
    public string SetupGamePort { get => _setupGamePort; set => SetField(ref _setupGamePort, value ?? string.Empty); }
    public string SetupRestPort { get => _setupRestPort; set => SetField(ref _setupRestPort, value ?? string.Empty); }
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

    public string BackupArchiveCountText => BackupItems.Count.ToString();
    public string BackupVerifiedCountText => BackupItems.Count(x => x.VerificationText.Contains("Verified", StringComparison.OrdinalIgnoreCase)).ToString();
    public string BackupPendingCountText => BackupItems.Count(x => !x.VerificationText.Contains("Verified", StringComparison.OrdinalIgnoreCase)).ToString();

    public string WorkspaceDeploymentMode => !IsLocalProfile ? "REMOTE" : AppContext.BaseDirectory.Contains("artifacts", StringComparison.OrdinalIgnoreCase) ? "PORTABLE / DEVELOPMENT" : "INSTALLED";
    public string WorkspaceHealthText => ConfigLoaded ? "CONFIGURATION LOADED" : "NOT VALIDATED";
    public string WorkspaceDiscoveryText => EnvironmentItems.Count == 0 ? "NOT CHECKED" : EnvironmentItems.All(x => x.IsReady) ? "SERVER READY" : "ATTENTION REQUIRED";
    public string WorkspaceExecutableRoot => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    public string WorkspaceApplicationData => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MystTiqPalworldServer");
    public string WorkspaceRoot => Path.Combine(WorkspaceApplicationData, "Workspace");
    public string WorkspaceDownloadsRoot => Path.Combine(WorkspaceRoot, "Downloads");
    public string WorkspaceExportsRoot => Path.Combine(WorkspaceApplicationData, "Exports");
    public string WorkspaceLogsRoot => Path.Combine(WorkspaceApplicationData, "Logs");

    public string NetworkHealth { get => _networkHealth; private set => SetField(ref _networkHealth, value); }
    public string LocalDiagnosticsState { get => _localDiagnosticsState; private set => SetField(ref _localDiagnosticsState, value); }
    public string NetworkRuntime { get => _networkRuntime; private set => SetField(ref _networkRuntime, value); }
    public string NetworkPort { get => _networkPort; private set => SetField(ref _networkPort, value); }
    public string NetworkBinding { get => _networkBinding; private set => SetField(ref _networkBinding, value); }
    public string NetworkLanEndpoint { get => _networkLanEndpoint; private set => SetField(ref _networkLanEndpoint, value); }
    public string NetworkRecommendation { get => _networkRecommendation; private set => SetField(ref _networkRecommendation, value); }
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
    public ICommand ValidateWorkspaceCommand { get; }
    public ICommand BootstrapLocalCommand { get; }

    public bool TryGetLocalWorkspacePath(string key, out string path)
    {
        path = key switch
        {
            "server" => ConfigServerRoot,
            "steamcmd" => ConfigSteamCmdPath,
            "backup" => ConfigBackupRoot,
            "runtime" => ConfigRuntimeRoot,
            "executable" => WorkspaceExecutableRoot,
            "workspace" => WorkspaceRoot,
            "appdata" => WorkspaceApplicationData,
            "downloads" => WorkspaceDownloadsRoot,
            "exports" => WorkspaceExportsRoot,
            "logs" => WorkspaceLogsRoot,
            _ => string.Empty
        };
        if (!IsLocalProfile)
        {
            WorkspaceState = "Open is unavailable for remote profiles because server paths do not belong to this computer.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(path))
        {
            WorkspaceState = "The selected workspace path is empty.";
            return false;
        }
        return true;
    }

    private void RaiseWorkspaceSummaryProperties()
    {
        RaisePropertyChanged(nameof(WorkspaceDeploymentMode));
        RaisePropertyChanged(nameof(WorkspaceHealthText));
        RaisePropertyChanged(nameof(WorkspaceDiscoveryText));
    }

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
            NetworkReportText, "", "Workspace", $"State: {WorkspaceState}",
            $"Server root: {ConfigServerRoot}", $"SteamCMD: {ConfigSteamCmdPath}", $"Backup root: {ConfigBackupRoot}", $"Runtime root: {ConfigRuntimeRoot}"
        });
    }

    private static bool IsAbsoluteServerPath(string value) =>
        value.StartsWith("/", StringComparison.Ordinal) || value.StartsWith("\\\\", StringComparison.Ordinal) ||
        (value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && (value[2] == '\\' || value[2] == '/'));

    private void ValidateWorkspacePaths()
    {
        var paths = new[] { ("Server root", ConfigServerRoot), ("SteamCMD", ConfigSteamCmdPath), ("Backup root", ConfigBackupRoot), ("Runtime root", ConfigRuntimeRoot) };
        var invalid = paths.Where(x => string.IsNullOrWhiteSpace(x.Item2) || !IsAbsoluteServerPath(x.Item2.Trim())).Select(x => x.Item1).ToArray();
        WorkspaceState = invalid.Length == 0
            ? "Path syntax is valid. Save Paths uses the authenticated headless configuration route for authoritative validation and rollback-safe persistence."
            : $"Path validation failed: {string.Join(", ", invalid)} must use absolute server paths.";
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

    private async Task RepairFirewallAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var result = await _api.RepairNetworkFirewallAsync(SelectedProfile, BearerToken);
            NetworkRecommendation = result.Message;
            await RunNetworkDiagnosticsAsync();
        }
        catch (Exception ex) { NetworkRecommendation = ex.Message; }
        finally { IsBusy = false; }
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
            WanRouterDescription = report.RouterDescription ?? "—";
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

    private void Navigate(string? pageName)
    {
        if (!Enum.TryParse<NavigationPage>(pageName, ignoreCase: true, out var page))
            return;

        SelectedPage = page;
        Dispatcher.UIThread.Post(async () => await RefreshPageForNavigationAsync(page));
    }

    private async Task RefreshPageForNavigationAsync(NavigationPage page)
    {
        if (IsBusy) return;
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
                await LoadConfigurationAsync();
                if (!IsBusy) await LoadPalworldConfigurationAsync();
                break;
            case NavigationPage.Workspace:
                await LoadConfigurationAsync();
                break;
            case NavigationPage.Backups:
                await RefreshBackupsAsync();
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
                break;
            case NavigationPage.Fleet:
                await RefreshFleetAsync();
                break;
            case NavigationPage.Inspector:
                await RefreshWorldExplorerAsync();
                break;
            case NavigationPage.WorldTransactions:
                await RefreshWorldTransactionsAsync();
                break;
            case NavigationPage.Bases:
            case NavigationPage.Guilds:
                await RefreshPlayerGuildExplorerAsync();
                break;
            case NavigationPage.ModDashboard:
            case NavigationPage.ModLibrary:
            case NavigationPage.Ue4ss:
                await RefreshModsAsync();
                break;
            case NavigationPage.Doctor:
                await RunDoctorAsync();
                break;
            case NavigationPage.DiagnosticsCenter:
                await RunNetworkDiagnosticsAsync();
                break;
            case NavigationPage.Settings:
                break;
            case NavigationPage.CrashAnalyzer:
                await RefreshCrashHistoryAsync();
                break;
            case NavigationPage.SaveTools:
                await RefreshSaveToolsAsync(false);
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
            Detail = $"Connection profile '{profile.Name}' saved. Bearer token was not persisted.";
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
        ManagementApiConnected = false;
        ConnectionState = "Not connected";
        Detail = "Enter the MystTiq management address and security details, then save the new server profile.";
        RaisePropertyChanged(nameof(IsLocalProfile));
        (DeleteProfileCommand as RelayCommand)?.RaiseCanExecuteChanged();
        Navigate(nameof(NavigationPage.Settings));
    }

    private void DeleteSelectedProfile()
    {
        var selected = SelectedProfile;
        if (selected is null || selected.Id == ConnectionProfile.LocalDefault.Id)
            return;

        Profiles.Remove(selected);
        _profileStore.Save(Profiles);
        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == ConnectionProfile.LocalDefault.Id) ?? Profiles.FirstOrDefault();
        Detail = $"Connection profile '{selected.Name}' deleted.";
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

    private async Task DiscoverServicesAsync()
    {
        IsBusy = true;
        DiscoveryStateText = "Scanning local IPv4 networks for MystTiq services…";
        try
        {
            var port = ConfigPort is > 0 and <= 65535 ? ConfigPort : 8213;
            var discovered = await _serviceDiscovery.DiscoverAsync(port);
            DiscoveredServices.Clear();
            foreach (var service in discovered)
                DiscoveredServices.Add(service);

            DiscoveryStateText = discovered.Count switch
            {
                0 => $"No MystTiq services answered /healthz on port {port}.",
                1 => "1 MystTiq service discovered.",
                _ => $"{discovered.Count} MystTiq services discovered."
            };

            // The local profile must prefer loopback. LAN services are available for explicit selection
            // in Settings, but must never displace a healthy local sidecar automatically.
            var preferred = SelectedProfile?.Id == ConnectionProfile.LocalDefault.Id
                ? discovered.FirstOrDefault(item => System.Net.IPAddress.TryParse(item.Address, out var candidateAddress) && System.Net.IPAddress.IsLoopback(candidateAddress)) ?? discovered.FirstOrDefault()
                : discovered.FirstOrDefault();
            if (preferred is not null)
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
        catch (Exception ex)
        {
            DiscoveryStateText = $"LAN discovery failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

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
            ServerInstallState = distribution.ServerExecutableExists ? "Installed" : "Missing";
            DistributionState = distribution.ServerExecutableExists ? "Ready" : "Setup required";
            DistributionDetail = distribution.Detail;
        }
        catch (Exception ex) { DistributionDetail = $"Distribution: {ex.Message}"; }

        try
        {
            var backups = await _api.GetBackupsAsync(profile, BearerToken);
            BackupItems.Clear();
            foreach (var item in backups.Items) BackupItems.Add(item);
            SelectedBackup = BackupItems.FirstOrDefault();
            BackupState = $"{backups.Count} backup(s)";
            BackupTotalSizeText = $"{backups.TotalSizeBytes / 1024d / 1024d:F2} MB";
            DashboardBackupText = backups.Count == 0 ? "No backups" : $"{backups.Count} available";
            DashboardBackupStripText = backups.Count == 0 ? "Backup: none" : $"Backup: {backups.Count} available";
        }
        catch (Exception ex) { BackupDetail = $"Dashboard backup load: {ex.Message}"; }

        try
        {
            var world = await _api.GetWorldExplorerAsync(profile, BearerToken);
            ActiveWorldIdText = world.ActiveWorldId ?? "Not resolved";
            WorldCountText = world.WorldCount.ToString();
            WorldPlayerSaveCountText = world.PlayerSaveCount.ToString();
            WorldSizeText = $"{world.TotalSizeBytes / 1024d / 1024d:F2} MB";
            DashboardWorldNicknameText = BuildWorldNickname(world.ActiveWorldId);
            DashboardWorldText = world.Available ? ActiveWorldIdText : "Unavailable";
            DashboardWorldPulseText = world.Available ? $"{WorldPlayerSaveCountText} player saves · {WorldSizeText}" : "World data unavailable";
        }
        catch (Exception ex) { WorldExplorerDetail = $"Dashboard world load: {ex.Message}"; }

        try
        {
            var identities = await _api.GetPlayerGuildExplorerAsync(profile, BearerToken);
            PlayerRecordCountText = identities.Players.Count.ToString();
            GuildRecordCountText = identities.Guilds.Count.ToString();
            DashboardGuildText = $"{identities.Guilds.Count} guild(s) · {identities.Players.Count} known player(s)";
        }
        catch (Exception ex) { PlayerGuildDetail = $"Dashboard identity load: {ex.Message}"; }

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
                DashboardHealthDetail = $"{LatestDiagnosticsReport.Failures} Doctor finding(s) need attention. Open Doctor.";
            }
            else if (LatestDiagnosticsReport.Warnings > 0 && DashboardHealthText == "READY")
            {
                DashboardHealthText = "DEGRADED";
                DashboardHealthDetail = $"{LatestDiagnosticsReport.Warnings} Doctor finding(s) are warnings. Open Doctor.";
            }
        }
        catch (Exception ex) { DiagnosticsReportDetail = $"Dashboard diagnostics load: {ex.Message}"; }

        try
        {
            var mods = await _api.GetModsAsync(profile, BearerToken);
            ApplyModInventory(mods);
            DashboardModText = $"{mods.Installed} installed · {mods.ConfirmedIssues} confirmed issue(s)";
        }
        catch (Exception ex) { ModSummary = $"Dashboard MOD load: {ex.Message}"; }
    }

    private async Task RefreshStatusPollingAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { StatusBarText = ex.Message; return; }

        try
        {
            var snapshot = await _api.GetStatusPollingAsync(profile, 120, BearerToken);
            ApplyStatus(snapshot.Status);
            ApplyServiceStatus(snapshot.Service);
            ApplyPlayers(snapshot.Players);
            ApplyMetrics(snapshot.Metrics);
            ApplyLogs(snapshot.LogTail);
            ApplyDashboardSupport(snapshot);
            ManagementApiConnected = true;
            ConnectionState = "Connected";
            StatusBarText = $"Connected — {ServerState} — {snapshot.Players.OnlineCount} player(s)";
            StatusBarObservedText = $"Updated {snapshot.ObservedAt.ToLocalTime():HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusBarText = $"Status unavailable — {ex.Message}";
            StatusBarObservedText = $"Failed {DateTimeOffset.Now:HH:mm:ss}";
        }
    }

    private async Task RefreshAsync() => await RefreshAsync(silent: false);

    private async Task RefreshAsync(bool silent)
    {
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
                throw new UnauthorizedAccessException("This MystTiq management API requires a bearer token. The desktop-owned local sidecar should not require remote credentials.");

            // Initial connection uses the same aggregate endpoint as periodic status polling. This
            // prevents connection establishment from fanning out into independent status/service calls.
            var poll = await _api.GetStatusPollingAsync(profile, 120, BearerToken);
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
            await RefreshHistoricalMetricsAsync(force: true);

            if (!silent)
                await RefreshDashboardBackendAsync(profile);
        }
        catch (Exception ex)
        {
            ManagementApiConnected = false;
            ConnectionState = "Connection failed";
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

    private async Task RefreshPlayersPageAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { PlayersPageDetail = ex.Message; return; }

        var selectedId = SelectedPlayerRecord?.PlayerId;
        IsBusy = true;
        PlayersPageState = "Discovering live and saved players…";
        try
        {
            // One aggregate status request supplies all live state. Save discovery remains an
            // explicit headless endpoint so remote desktops never inspect server-side paths.
            var pollTask = _api.GetStatusPollingAsync(profile, 120, BearerToken);
            var explorerTask = _api.GetPlayerGuildExplorerAsync(profile, BearerToken);
            await Task.WhenAll(pollTask, explorerTask);

            var poll = await pollTask;
            ApplyStatus(poll.Status);
            ApplyServiceStatus(poll.Service);
            ApplyPlayers(poll.Players);
            ApplyMetrics(poll.Metrics);
            ApplyLogs(poll.LogTail);
            ApplyDashboardSupport(poll);

            var snapshot = await explorerTask;
            PlayerRecords.Clear();
            foreach (var player in snapshot.Players.OrderByDescending(x => x.Online).ThenBy(x => x.PlayerName).ThenBy(x => x.PlayerId))
                PlayerRecords.Add(player);

            ApplyPlayerFilters();
            SelectedPlayerRecord = !string.IsNullOrWhiteSpace(selectedId)
                ? PlayerRecords.FirstOrDefault(x => string.Equals(x.PlayerId, selectedId, StringComparison.OrdinalIgnoreCase))
                : FilteredPlayerRecords.FirstOrDefault();

            PlayersPageState = snapshot.Available ? $"Ready · {poll.Players.OnlineCount} online" : "Player discovery unavailable";
            PlayersPageDetail = snapshot.Detail;
            ManagementApiConnected = true;
            ConnectionState = "Connected";
            StatusBarObservedText = $"Updated {poll.ObservedAt.ToLocalTime():HH:mm:ss}";

            if (SelectedPlayerRecord is not null)
                await LoadSelectedPlayerMetadataCoreAsync(profile, SelectedPlayerRecord.PlayerId);

            try
            {
                var providers = await _api.GetPlayerModerationProvidersAsync(profile, BearerToken);
                ModerationProviderStatusText = providers.Count == 0
                    ? string.Empty
                    : string.Join("   ", providers.Select(x => $"{x.DisplayName}: {x.Health}"));
            }
            catch { ModerationProviderStatusText = string.Empty; }
        }
        catch (Exception ex)
        {
            PlayersPageState = "Unavailable";
            PlayersPageDetail = ex.Message;
        }
        finally { IsBusy = false; }
    }

    private void ApplyPlayerFilters()
    {
        var selectedId = SelectedPlayerRecord?.PlayerId;
        var search = PlayerSearchText.Trim();
        var filtered = PlayerRecords.Where(player =>
        {
            if (!string.IsNullOrWhiteSpace(search) &&
                !player.PlayerId.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !player.PlayerName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !player.GuildName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !player.Platform.Contains(search, StringComparison.OrdinalIgnoreCase))
                return false;

            if (SelectedPlayerViewFilter == "Online" && !player.Online) return false;
            if (SelectedPlayerViewFilter == "Known Saves" && !player.SaveExists) return false;
            if (SelectedPlayerViewFilter == "Missing Saves" && player.SaveExists) return false;
            if (SelectedPlayerAdminFilter == "Actionable Online" && !player.Online) return false;
            if (SelectedPlayerAdminFilter == "Needs Review" && player.SaveExists) return false;
            return true;
        }).ToArray();

        FilteredPlayerRecords.Clear();
        foreach (var player in filtered) FilteredPlayerRecords.Add(player);
        PlayerVisibleCountText = $"{filtered.Length} visible / {PlayerRecords.Count} known";

        if (!string.IsNullOrWhiteSpace(selectedId))
            SelectedPlayerRecord = filtered.FirstOrDefault(x => string.Equals(x.PlayerId, selectedId, StringComparison.OrdinalIgnoreCase));
        SelectedPlayerRecord ??= filtered.FirstOrDefault();
    }

    private async Task LoadSelectedPlayerMetadataAsync()
    {
        if (SelectedPlayerRecord is null) return;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { PlayerMetadataState = ex.Message; return; }

        IsBusy = true;
        try { await LoadSelectedPlayerMetadataCoreAsync(profile, SelectedPlayerRecord.PlayerId); }
        catch (Exception ex) { PlayerMetadataState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task LoadSelectedPlayerMetadataCoreAsync(ConnectionProfile profile, string playerId)
    {
        var metadata = await _api.GetPlayerMetadataAsync(profile, playerId, BearerToken);
        if (!string.Equals(SelectedPlayerRecord?.PlayerId, playerId, StringComparison.OrdinalIgnoreCase)) return;
        ApplyPlayerMetadata(metadata, "Metadata loaded");

        try
        {
            var registry = await _api.GetPlayerRegistryAsync(profile, BearerToken);
            if (!string.Equals(SelectedPlayerRecord?.PlayerId, playerId, StringComparison.OrdinalIgnoreCase)) return;
            var record = registry.FirstOrDefault(r => string.Equals(r.PlayerId, playerId, StringComparison.OrdinalIgnoreCase));
            PlayerRegistrySummaryText = record is null
                ? "No registry history yet — first observed on the next status poll."
                : $"First seen {record.FirstSeenUtc.ToLocalTime():yyyy-MM-dd} · Last seen {record.LastSeenUtc.ToLocalTime():yyyy-MM-dd HH:mm} · {record.TotalSessions} session(s) · {record.TotalPlaytimeMinutes / 60:F1}h tracked playtime";
        }
        catch { PlayerRegistrySummaryText = string.Empty; }
    }

    private async Task PreviewCharacterMigrationAsync()
    {
        if (MigrationSourcePlayer is null || MigrationDestinationPlayer is null) return;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { MigrationState = ex.Message; return; }

        IsBusy = true;
        MigrationState = "Previewing…";
        try
        {
            var preview = await _api.PreviewCharacterMigrationAsync(profile,
                new CharacterMigrationPreviewRequestDto(MigrationSourcePlayer.PlayerId, MigrationDestinationPlayer.PlayerId), BearerToken);
            MigrationPreview = preview;
            MigrationState = string.Join(" ", preview.Findings);
        }
        catch (Exception ex) { MigrationState = ex.Message; MigrationPreview = null; }
        finally { IsBusy = false; }
    }

    private async Task ApplyCharacterMigrationAsync()
    {
        if (MigrationPreview is not { CanApply: true }) return;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { MigrationState = ex.Message; return; }

        IsBusy = true;
        MigrationState = "Applying migration…";
        try
        {
            var result = await _api.ApplyCharacterMigrationAsync(profile,
                new CharacterMigrationApplyRequestDto(MigrationPreview.PreviewToken, true), BearerToken);
            MigrationState = result.Message;
            if (result.Success && result.SourcePlayerId is not null)
            {
                // Reset is intentionally still selectable (not decorative-disabled) so the server's
                // own real "not yet supported" rejection is what the operator sees, rather than a
                // client-side substitution silently changing their choice.
                var dispositionText = MigrationDisposition.Split(' ')[0];
                var dispositionResult = await _api.DisposeSourceCharacterAsync(profile, result.SourcePlayerId,
                    new CharacterDispositionRequestDto(dispositionText), BearerToken);
                MigrationState = $"{result.Message} {dispositionResult.Message}";
            }
            MigrationPreview = null;
            await RefreshPlayersPageAsync();
        }
        catch (Exception ex) { MigrationState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task SaveSelectedPlayerNotesAsync()
    {
        if (SelectedPlayerRecord is null) return;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { PlayerMetadataState = ex.Message; return; }

        IsBusy = true;
        PlayerMetadataState = "Saving notes…";
        try
        {
            var metadata = await _api.SavePlayerNotesAsync(profile, SelectedPlayerRecord.PlayerId, SelectedPlayerNotes, BearerToken);
            ApplyPlayerMetadata(metadata, "Notes saved and audited by the headless service");
        }
        catch (Exception ex) { PlayerMetadataState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task AddSelectedPlayerWarningAsync()
    {
        if (SelectedPlayerRecord is null || string.IsNullOrWhiteSpace(NewPlayerWarning)) return;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { PlayerMetadataState = ex.Message; return; }

        IsBusy = true;
        PlayerMetadataState = "Adding warning…";
        try
        {
            var metadata = await _api.AddPlayerWarningAsync(profile, SelectedPlayerRecord.PlayerId, NewPlayerWarning, BearerToken);
            NewPlayerWarning = string.Empty;
            ApplyPlayerMetadata(metadata, "Warning added and audited by the headless service");
        }
        catch (Exception ex) { PlayerMetadataState = ex.Message; }
        finally { IsBusy = false; }
    }

    private void ApplyPlayerMetadata(PlayerMetadataDto metadata, string state)
    {
        SelectedPlayerNotes = metadata.Notes;
        SelectedPlayerWarnings.Clear();
        foreach (var warning in metadata.Warnings.OrderByDescending(x => x.CreatedAt))
            SelectedPlayerWarnings.Add(warning);
        PlayerMetadataState = $"{state} · {metadata.Warnings.Count} warning(s) · {metadata.UpdatedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
    }

    public string ExportVisiblePlayersCsv()
    {
        static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
        var builder = new StringBuilder();
        builder.AppendLine("PlayerId,PlayerName,Online,Platform,Ping,GuildId,GuildName,Role,SaveExists,SaveSizeBytes,SaveLastWriteUtc,Evidence");
        foreach (var player in FilteredPlayerRecords)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Csv(player.PlayerId), Csv(player.PlayerName), Csv(player.Online.ToString()), Csv(player.Platform), Csv(player.Ping),
                Csv(player.GuildId), Csv(player.GuildName), Csv(player.Role), Csv(player.SaveExists.ToString()),
                Csv(player.SaveSizeBytes.ToString()), Csv(player.SaveLastWriteUtc?.ToString("O")), Csv(player.Evidence)
            }));
        }
        return builder.ToString();
    }

    private void RaisePlayerCommandStates()
    {
        (KickSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (BanSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (WhisperSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (PromoteSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (GiveItemSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (LoadPlayerMetadataCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (SavePlayerNotesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (AddPlayerWarningCommand as AsyncCommand)?.RaiseCanExecuteChanged();
    }

    private async Task RefreshMonitoringAsyncCore(ConnectionProfile profile)
    {
        try
        {
            var playersTask = _api.GetPlayersAsync(profile, BearerToken);
            var logsTask = _api.GetLogTailAsync(profile, 120, BearerToken);
            var metricsTask = _api.GetMetricsAsync(profile, BearerToken);

            await Task.WhenAll(playersTask, logsTask, metricsTask);

            ApplyPlayers(await playersTask);
            ApplyLogs(await logsTask);
            ApplyMetrics(await metricsTask);
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
        DashboardPlayersSessionText = $"{snapshot.OnlineCount} online";
        PlayersState = snapshot.Available
            ? snapshot.OnlineCount == 0 ? "REST available / 0 online" : $"{snapshot.OnlineCount} online"
            : "REST unavailable";

        MonitoringDetail = snapshot.Detail;
    }

    private void ApplyLogs(LogTailSnapshotDto snapshot)
    {
        LogLines.Clear();
        foreach (var line in snapshot.Lines)
            LogLines.Add(EnsureConsoleTimestampFirst(line, snapshot.ObservedAt));

        LogFileText = snapshot.Available
            ? snapshot.FileName ?? "Palworld log"
            : "Log unavailable";
        if (!ConsolePaused) ApplyConsoleFilter();

        DashboardActivityLines.Clear();
        foreach (var line in snapshot.Lines.Where(x => !string.IsNullOrWhiteSpace(x)).TakeLast(8))
            DashboardActivityLines.Add(EnsureConsoleTimestampFirst(line, snapshot.ObservedAt));
        DashboardLastActivityText = DashboardActivityLines.Count > 0 ? DashboardActivityLines[^1] : "No recent console activity";

        if (!snapshot.Available)
            MonitoringDetail = snapshot.Detail;
    }

    private static string EnsureConsoleTimestampFirst(string line, DateTimeOffset observedAt)
    {
        if (string.IsNullOrWhiteSpace(line)) return line;
        return System.Text.RegularExpressions.Regex.IsMatch(line, @"^\[\d{4}-\d{2}-\d{2}[ T]")
            ? line
            : $"[{observedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss.fff}] {line}";
    }

    private void ApplyConsoleFilter()
    {
        if (ConsolePaused) return;
        FilteredLogLines.Clear();
        foreach (var line in LogLines)
        {
            if (HideRoutineRest && IsRoutineRestLine(line)) continue;
            if (!ConsoleSeverityMatches(line, ConsoleSeverityFilter)) continue;
            if (!ConsoleCategoryMatches(line, ConsoleCategoryFilter)) continue;
            if (!string.IsNullOrWhiteSpace(ConsoleSearchText) && !line.Contains(ConsoleSearchText, StringComparison.OrdinalIgnoreCase)) continue;
            FilteredLogLines.Add(line);
        }
        ConsoleViewState = $"Live · {FilteredLogLines.Count}/{LogLines.Count} lines";
    }

    private static bool IsRoutineRestLine(string line) =>
        line.Contains("/api/v1/status/poll", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("/api/v1/metrics", StringComparison.OrdinalIgnoreCase) ||
        line.Contains("/api/v1/players", StringComparison.OrdinalIgnoreCase);

    private static bool ConsoleSeverityMatches(string line, string filter)
    {
        if (filter == "All") return true;
        var error = line.Contains("ERROR", StringComparison.OrdinalIgnoreCase) || line.Contains("FATAL", StringComparison.OrdinalIgnoreCase) || line.Contains("EXCEPTION", StringComparison.OrdinalIgnoreCase);
        var warning = line.Contains("WARN", StringComparison.OrdinalIgnoreCase);
        return filter switch { "Errors" => error, "Warnings" => warning, "Information" => !error && !warning, _ => true };
    }

    private static bool ConsoleCategoryMatches(string line, string filter)
    {
        if (filter == "All") return true;
        var category = line.Contains("[MYSTTIQ]", StringComparison.OrdinalIgnoreCase) ? "MystTiq"
            : line.Contains("REST", StringComparison.OrdinalIgnoreCase) || line.Contains("/api/v1/", StringComparison.OrdinalIgnoreCase) ? "REST"
            : line.Contains("PalDefender", StringComparison.OrdinalIgnoreCase) || line.Contains("AdminCommands", StringComparison.OrdinalIgnoreCase) || line.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) ? "Mods"
            : "PalServer";
        return category.Equals(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void ToggleConsolePause()
    {
        ConsolePaused = !ConsolePaused;
        if (ConsolePaused) ConsoleViewState = $"Paused · {FilteredLogLines.Count} visible lines";
        else ApplyConsoleFilter();
    }

    private void ClearConsoleView()
    {
        FilteredLogLines.Clear();
        ConsoleViewState = "View cleared · server log was not deleted";
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
            ActiveWorldIdText = world.ActiveWorldId ?? "Not resolved";
            DashboardWorldNicknameText = BuildWorldNickname(world.ActiveWorldId);
            DashboardWorldText = ActiveWorldIdText;
            WorldPlayerSaveCountText = world.PlayerSaveCount.ToString();
            WorldSizeText = $"{world.TotalSizeBytes / 1024d / 1024d:F2} MB";
            DashboardWorldPulseText = $"{world.PlayerSaveCount} player save(s) · {WorldSizeText}";
            DashboardWorldClockText = world.WorldDayNumber.HasValue
                ? $"Day {world.WorldDayNumber.Value:N0} • {world.WorldTimeText ?? "--:--"}"
                : "Day — • --:--";
            DashboardWorldClockDetailText = world.WorldDayNumber.HasValue
                ? "Exact saved world clock from GameTimeSaveData.GameDateTimeTicks"
                : "Decoded Level.sav JSON unavailable — MystTiq will not estimate the day";
            DashboardPulseSaveText = world.LastWorldSaveUtc.HasValue
                ? $"World save: {FormatAge(DateTimeOffset.Now - world.LastWorldSaveUtc.Value.ToLocalTime())} ago"
                : "World save: —";
        }
        else
        {
            DashboardWorldClockText = "Day — • --:--";
            DashboardWorldClockDetailText = "World telemetry unavailable";
            DashboardPulseSaveText = "World save: —";
        }

        var latestBackup = snapshot.BackupInventory.Items.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
        DashboardBackupText = snapshot.BackupInventory.Count == 0 ? "No backups" : $"{snapshot.BackupInventory.Count} available";
        DashboardBackupStripText = snapshot.BackupInventory.Count == 0 ? "Backup: none" : $"Backup: {snapshot.BackupInventory.Count} available";
        BackupTotalSizeText = $"{snapshot.BackupInventory.TotalSizeBytes / 1024d / 1024d:F2} MB";
        DashboardPulseBackupText = latestBackup is null
            ? "Backup: none detected"
            : $"Backup: {FormatAge(DateTimeOffset.Now - latestBackup.CreatedAt.ToLocalTime())} ago{(latestBackup.Verified ? " · Verified" : string.Empty)}";

        var serverName = snapshot.PalworldSettings.Settings.FirstOrDefault(x => x.Name.Equals("ServerName", StringComparison.OrdinalIgnoreCase))?.Value;
        var description = snapshot.PalworldSettings.Settings.FirstOrDefault(x => x.Name.Equals("ServerDescription", StringComparison.OrdinalIgnoreCase))?.Value;
        DashboardServerNameText = string.IsNullOrWhiteSpace(serverName) ? "—" : serverName;
        DashboardServerDescriptionText = string.IsNullOrWhiteSpace(description) ? "—" : description;
        var rconEnabled = snapshot.PalworldSettings.Settings.FirstOrDefault(x => x.Name.Equals("RCONEnabled", StringComparison.OrdinalIgnoreCase))?.Value;
        var rconPort = snapshot.PalworldSettings.Settings.FirstOrDefault(x => x.Name.Equals("RCONPort", StringComparison.OrdinalIgnoreCase))?.Value?.Trim('"');
        DashboardRconText = string.Equals(rconEnabled?.Trim('"'), "True", StringComparison.OrdinalIgnoreCase)
            ? $"RCON: Enabled{(string.IsNullOrWhiteSpace(rconPort) ? string.Empty : $" : {rconPort}")}"
            : "RCON: Disabled";
    }

    private static string BuildWorldNickname(string? worldId) =>
        string.IsNullOrWhiteSpace(worldId) ? "World —" : $"World {worldId[..Math.Min(8, worldId.Length)]}";

    private void ApplyMetrics(RuntimeMetricsSnapshotDto snapshot)
    {
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
                MemoryMb = Math.Max(0, memoryMb)
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
            UpdateHistorySummaries(snapshot.CpuTrend, snapshot.MemoryTrend);
            HistoryStatusText = ResourceHistory.Count == 0 ? "Collecting" : $"Updated {snapshot.ObservedAt.ToLocalTime():HH:mm:ss}";
        }
        catch (Exception ex)
        {
            HistoryStatusText = $"History unavailable: {ex.Message}";
        }
    }

    private void UpdateHistorySummaries(string? cpuTrend = null, string? memoryTrend = null)
    {
        if (ResourceHistory.Count == 0)
        {
            HistoryCpuSummary = "CPU history collecting…";
            HistoryMemorySummary = "Memory history collecting…";
            HistorySampleSummary = "0 samples";
            return;
        }

        var averageCpu = ResourceHistory.Average(x => x.CpuPercent);
        var peakCpu = ResourceHistory.Max(x => x.CpuPercent);
        var averageMemory = ResourceHistory.Average(x => x.MemoryMb);
        var peakMemory = ResourceHistory.Max(x => x.MemoryMb);
        HistoryCpuSummary = $"{cpuTrend ?? Trend(ResourceHistory.Select(x => x.CpuPercent))} Avg {averageCpu:F1}% · Peak {peakCpu:F1}%";
        HistoryMemorySummary = $"{memoryTrend ?? Trend(ResourceHistory.Select(x => x.MemoryMb))} Avg {FormatMemory(averageMemory)} · Peak {FormatMemory(peakMemory)}";
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

    private async Task RefreshSecurityAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var who = await _api.WhoAmIAsync(SelectedProfile, BearerToken);
            CurrentPrincipal = who;
            RaisePropertyChanged(nameof(CanManageAdmin));
            RaisePropertyChanged(nameof(CanManagePrincipals));

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
        if (SelectedProfile is null || string.IsNullOrWhiteSpace(CloneNewProfileId)) return;
        IsBusy = true;
        CloneWorldStatusText = "Cloning… this copies the entire server installation and can take a while.";
        try
        {
            var request = new WorldCloneRequestDto
            {
                NewProfileId = CloneNewProfileId.Trim(),
                NewProfileName = string.IsNullOrWhiteSpace(CloneNewProfileName) ? null : CloneNewProfileName.Trim()
            };
            var result = await _api.CloneWorldAsync(SelectedProfile, request, BearerToken);
            CloneWorldStatusText = result.Message;
            if (result.Success) await RefreshFleetAsync();
        }
        catch (Exception ex) { CloneWorldStatusText = ex.Message; }
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
        CrashIsolationPlan.Clear(); foreach (var step in report.IsolationPlan) CrashIsolationPlan.Add(step);
        CrashAnalyzerState = $"{report.Summary} Scanned {report.FilesScanned} file(s) and {report.LinesScanned} line(s).";
    }

    private async Task RefreshSaveToolsAsync(bool selfTest)
    {
        if (SelectedProfile is null) return; IsBusy = true; SaveToolsState = selfTest ? "Running bounded server-side self-tests…" : "Inspecting Save Tools…";
        try
        {
            var diagnostics = selfTest
                ? await _api.RunSaveToolsSelfTestAsync(SelectedProfile, BearerToken)
                : await _api.GetSaveToolsDiagnosticsAsync(SelectedProfile, BearerToken);
            var inventory = await _api.GetSaveFilesAsync(SelectedProfile, BearerToken);
            SaveToolsTests.Clear(); foreach (var test in diagnostics.Tests) SaveToolsTests.Add(test);
            SaveFiles.Clear(); foreach (var file in inventory.Items) SaveFiles.Add(file);
            SelectedSaveFile = SaveFiles.FirstOrDefault();
            SaveToolsState = $"{(diagnostics.Ready ? "READY" : "INCOMPLETE")} · {diagnostics.Detail} {inventory.Detail}";
            SaveToolsPaths = $"Python: {diagnostics.PythonPath ?? "Missing"}\nLegacy converter: {diagnostics.LegacyConverterPath ?? "Missing"}\nPlM converter: {diagnostics.PlmConverterPath ?? "Missing"}\nOodle: {diagnostics.OodlePath ?? "Missing"}\nActive Level.sav: {diagnostics.ActiveLevelSavePath ?? "Not found"}\nSignature: {diagnostics.ActiveLevelSignature}";
        }
        catch (Exception ex) { SaveToolsState = ex.Message; }
        finally { IsBusy = false; }
    }

    public string ExportCrashAnalysis() => JsonSerializer.Serialize(new { state = CrashAnalyzerState, findings = CrashFindings, isolationPlan = CrashIsolationPlan, history = CrashHistory }, new JsonSerializerOptions { WriteIndented = true });
    public string ExportSaveToolsDiagnostics() => JsonSerializer.Serialize(new { state = SaveToolsState, paths = SaveToolsPaths, tests = SaveToolsTests, files = SaveFiles }, new JsonSerializerOptions { WriteIndented = true });

    private async Task RunSelectedPlayerAdminActionAsync(string action)
    {
        if (SelectedPlayerRecord is null)
        {
            PlayerAdminStatusText = "Select a player first.";
            return;
        }
        if (!SelectedPlayerRecord.Online)
        {
            PlayerAdminStatusText = "Kick and ban require a currently online player.";
            return;
        }
        var playerId = SelectedPlayerRecord.PlayerId;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            PlayerAdminStatusText = "The selected player does not have a usable server-side identity.";
            return;
        }
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { PlayerAdminStatusText = ex.Message; return; }

        IsBusy = true;
        PlayerAdminStatusText = $"Sending {action} for {SelectedPlayerRecord.PlayerName}…";
        try
        {
            var result = await _api.RunPlayerAdminActionAsync(profile, playerId, action, PlayerActionMessage, PlayerActionItem, BearerToken);
            PlayerAdminStatusText = result.Message;
            await RefreshActivityAsync();
            if (action is "kick" or "ban") await RefreshPlayersPageAsync();
        }
        catch (Exception ex) { PlayerAdminStatusText = ex.Message; }
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
            foreach (var finding in LatestDiagnosticsReport.Findings) DiagnosticFindings.Add(finding);
            DoctorStatus = LatestDiagnosticsReport.OverallHealthText;
            DoctorSummary = $"{LatestDiagnosticsReport.Passed} passed · {LatestDiagnosticsReport.Warnings} warning(s) · {LatestDiagnosticsReport.Failures} failure(s)";
        }
        catch (Exception ex) { DoctorStatus = "Unavailable"; DoctorSummary = ex.Message; }
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
        if (finding is null || !finding.CanFix) return;
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

    private async Task RefreshBackupsAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { BackupDetail = ex.Message; return; }

        IsBusy = true;
        try
        {
            var selectedFileName = SelectedBackup?.FileName;
            var inventory = await _api.GetBackupsAsync(profile, BearerToken);
            BackupItems.Clear();
            foreach (var item in inventory.Items)
                BackupItems.Add(item);

            SelectedBackup = BackupItems.FirstOrDefault(x => x.FileName == selectedFileName) ?? BackupItems.FirstOrDefault();
            BackupState = $"{inventory.Count} backup(s)";
            BackupTotalSizeText = $"{inventory.TotalSizeBytes / 1024d / 1024d:F2} MB";
            BackupRootPath = inventory.RootPath;
            BackupDetail = inventory.Detail;
        }
        catch (Exception ex)
        {
            BackupState = "Unavailable";
            BackupDetail = ex.Message;
        }
        finally { IsBusy = false; }
    }

    private async Task CreateBackupAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { BackupDetail = ex.Message; return; }

        IsBusy = true;
        BackupState = "Creating…";
        try
        {
            var result = await _api.CreateBackupAsync(profile, BearerToken);
            BackupDetail = result.Message;
            await RefreshBackupsCoreAsync(profile);
        }
        catch (Exception ex) { BackupDetail = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task DeleteBackupAsync()
    {
        if (SelectedBackup is null) return;

        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { BackupDetail = ex.Message; return; }

        var fileName = SelectedBackup.FileName;
        IsBusy = true;
        try
        {
            var result = await _api.DeleteBackupAsync(profile, fileName, BearerToken);
            BackupDetail = result.Message;
            await RefreshBackupsCoreAsync(profile);
        }
        catch (Exception ex) { BackupDetail = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RestoreBackupAsync()
    {
        if (SelectedBackup is null) return;

        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { BackupDetail = ex.Message; return; }

        var fileName = SelectedBackup.FileName;
        IsBusy = true;
        BackupState = "Restoring…";
        try
        {
            var result = await _api.RestoreBackupAsync(profile, fileName, BearerToken);
            BackupDetail = result.Message;
            await RefreshBackupsCoreAsync(profile);
        }
        catch (Exception ex) { BackupDetail = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task VerifySelectedBackupAsync()
    {
        if (SelectedBackup is null) return;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { BackupDetail = ex.Message; return; }
        IsBusy = true;
        try
        {
            var result = await _api.VerifyBackupAsync(profile, SelectedBackup.FileName, BearerToken);
            BackupDetail = $"{result.Message} SHA-256: {result.Sha256 ?? "unavailable"}; entries: {result.EntryCount}.";
            await RefreshBackupsCoreAsync(profile);
        }
        catch (Exception ex) { BackupDetail = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task VerifyAllBackupsAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { BackupDetail = ex.Message; return; }
        IsBusy = true;
        try
        {
            var result = await _api.VerifyAllBackupsAsync(profile, BearerToken);
            BackupDetail = $"{result.Message} Passed: {result.Results.Count(x => x.Success)}; failed: {result.Results.Count(x => !x.Success)}.";
            await RefreshBackupsCoreAsync(profile);
        }
        catch (Exception ex) { BackupDetail = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task PreviewBackupRetentionAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { BackupRetentionState = ex.Message; return; }
        IsBusy = true;
        try
        {
            var preview = await _api.PreviewBackupRetentionAsync(profile, BackupRetentionKeepLatest, BackupRetentionMaxAgeDays, BearerToken);
            BackupRetentionItems.Clear();
            foreach (var item in preview.Items) BackupRetentionItems.Add(item);
            BackupRetentionToken = preview.Token;
            BackupRetentionState = $"{preview.Message} Reclaim: {preview.ReclaimBytes / 1024d / 1024d:F2} MB. Preview expires {preview.ExpiresAt.ToLocalTime():HH:mm:ss}.";
        }
        catch (Exception ex) { InvalidateRetentionPreview(); BackupRetentionState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task ApplyBackupRetentionAsync()
    {
        if (string.IsNullOrWhiteSpace(BackupRetentionToken)) return;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { BackupRetentionState = ex.Message; return; }
        var token = BackupRetentionToken;
        IsBusy = true;
        try
        {
            var result = await _api.ApplyBackupRetentionAsync(profile, token, BearerToken);
            BackupRetentionState = $"{result.Message} Deleted: {result.DeletedCount}; reclaimed: {result.ReclaimedBytes / 1024d / 1024d:F2} MB.";
            BackupRetentionToken = string.Empty;
            BackupRetentionItems.Clear();
            await RefreshBackupsCoreAsync(profile);
        }
        catch (Exception ex) { InvalidateRetentionPreview(); BackupRetentionState = ex.Message; }
        finally { IsBusy = false; }
    }

    private void InvalidateRetentionPreview()
    {
        BackupRetentionToken = string.Empty;
        BackupRetentionItems.Clear();
        BackupRetentionState = "Policy changed. Preview again before cleanup.";
    }

    private void OpenBackupRoot()
    {
        if (SelectedProfile?.Id != ConnectionProfile.LocalDefault.Id)
        {
            BackupDetail = $"Remote backup root: {BackupRootPath}. Use the server-side inventory; MystTiq will not open a path on the GUI computer.";
            return;
        }
        try
        {
            var start = OperatingSystem.IsWindows()
                ? new ProcessStartInfo("explorer.exe", BackupRootPath)
                : new ProcessStartInfo("xdg-open", BackupRootPath);
            start.UseShellExecute = true;
            Process.Start(start);
            BackupDetail = $"Opened local backup root: {BackupRootPath}";
        }
        catch (Exception ex) { BackupDetail = "Unable to open the local backup root: " + ex.Message; }
    }

    private void RaiseBackupCommandStates()
    {
        (DeleteBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RestoreBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (VerifySelectedBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
    }

    private async Task RefreshBackupsCoreAsync(ConnectionProfile profile)
    {
        var selectedFileName = SelectedBackup?.FileName;
        var inventory = await _api.GetBackupsAsync(profile, BearerToken);
        BackupItems.Clear();
        foreach (var item in inventory.Items)
            BackupItems.Add(item);
        SelectedBackup = BackupItems.FirstOrDefault(x => x.FileName == selectedFileName) ?? BackupItems.FirstOrDefault();
        BackupState = $"{inventory.Count} backup(s)";
        BackupTotalSizeText = $"{inventory.TotalSizeBytes / 1024d / 1024d:F2} MB";
        BackupRootPath = inventory.RootPath;
        RaisePropertyChanged(nameof(BackupArchiveCountText));
        RaisePropertyChanged(nameof(BackupVerifiedCountText));
        RaisePropertyChanged(nameof(BackupPendingCountText));
    }

    private static readonly HashSet<string> SimpleConfigurationNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ServerName", "ServerDescription", "ServerPassword", "AdminPassword", "PublicPort", "ServerPlayerMaxNum",
        "RESTAPIEnabled", "RESTAPIPort", "RCONEnabled", "RCONPort",
        "DayTimeSpeedRate", "NightTimeSpeedRate", "ExpRate", "PalCaptureRate", "PalSpawnNumRate",
        "PlayerDamageRateAttack", "PlayerDamageRateDefense", "PlayerStomachDecreaceRate", "PlayerStaminaDecreaceRate",
        "PlayerAutoHPRegeneRate", "PalStomachDecreaceRate", "PalStaminaDecreaceRate", "PalAutoHPRegeneRate",
        "CollectionDropRate", "CollectionObjectRespawnSpeedRate", "WorkSpeedRate", "MonsterFarmActionSpeedRate",
        "ItemWeightRate", "ItemCorruptionMultiplier", "EquipmentDurabilityDamageRate", "BaseCampWorkerMaxNum",
        "BaseCampMaxNumInGuild", "BuildObjectDeteriorationDamageRate", "SupplyDropSpan"
    };

    private void SetConfigurationView(bool simple)
    {
        if (_isConfigSimpleView == simple) return;
        _isConfigSimpleView = simple;
        RaisePropertyChanged(nameof(IsConfigSimpleView));
        RaisePropertyChanged(nameof(IsConfigAdvancedView));
        ApplyPalworldConfigurationFilter();
    }

    private void RebuildPalworldConfigurationCategories()
    {
        var selected = SelectedConfigCategory;
        PalworldConfigCategories.Clear();
        PalworldConfigCategories.Add("All Categories");
        foreach (var category in PalworldSettings.Select(x => x.Category).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
            PalworldConfigCategories.Add(category);
        SelectedConfigCategory = PalworldConfigCategories.Contains(selected) ? selected : "All Categories";
    }

    private void ApplyPalworldConfigurationFilter()
    {
        var search = ConfigSearchText.Trim();
        var category = SelectedConfigCategory;
        var items = PalworldSettings.Where(setting =>
            (!_isConfigSimpleView || SimpleConfigurationNames.Contains(setting.Name)) &&
            (string.Equals(category, "All Categories", StringComparison.OrdinalIgnoreCase) || string.Equals(setting.Category, category, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(search) || setting.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || setting.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) || setting.Category.Contains(search, StringComparison.OrdinalIgnoreCase)));
        FilteredPalworldSettings.Clear();
        foreach (var item in items) FilteredPalworldSettings.Add(item);
    }

    private void RebuildSimplePalworldSettings()
    {
        SimplePalworldSettings.Clear();
        var definitions = new (string Name, string Title, string Description, double Min, double Max, double Step, string Unit)[]
        {
            ("DayTimeSpeedRate", "Daytime Speed", "Controls how quickly daylight passes.", 0.1, 5, 0.05, "x"),
            ("NightTimeSpeedRate", "Nighttime Speed", "Controls how quickly nighttime passes.", 0.1, 5, 0.05, "x"),
            ("CollectionDropRate", "More Item Drops", "Increases resources dropped from collection objects.", 0.1, 10, 0.1, "x"),
            ("CollectionObjectRespawnSpeedRate", "Resource Respawn", "Controls how quickly collection objects return.", 0.1, 10, 0.1, "x"),
            ("SupplyDropSpan", "Supply Drop Interval", "Sets the time between supply drops.", 1, 360, 1, "min"),
            ("PlayerAutoHPRegeneRate", "Player Health Regeneration", "Increases automatic player health recovery.", 0.1, 10, 0.1, "x"),
            ("PalAutoHPRegeneRate", "Pal Health Regeneration", "Increases automatic Pal health recovery.", 0.1, 10, 0.1, "x"),
            ("ExpRate", "Experience Rate", "Controls experience earned by players and Pals.", 0.1, 20, 0.1, "x")
        };
        foreach (var definition in definitions)
        {
            var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(definition.Name, StringComparison.OrdinalIgnoreCase));
            if (setting is not null)
                SimplePalworldSettings.Add(new PalworldSimpleSettingItem(setting, definition.Title, definition.Description, definition.Min, definition.Max, definition.Step, definition.Unit));
        }
        RaisePropertyChanged(nameof(ServerNameSetting));
        RaisePropertyChanged(nameof(ServerDescriptionSetting));
    }

    private void PalworldSetting_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PalworldSettingDto.Value) && e.PropertyName != nameof(PalworldSettingDto.IsDirty)) return;
        RefreshPalworldConfigurationState();
        if (!string.IsNullOrWhiteSpace(ConfigSearchText)) ApplyPalworldConfigurationFilter();
    }

    private void RefreshPalworldConfigurationState()
    {
        var dirtyCount = PalworldSettings.Count(x => x.IsDirty);
        PalworldConfigDirtyText = dirtyCount == 0 ? "No unsaved changes" : $"{dirtyCount} unsaved setting change(s)";
        RaisePropertyChanged(nameof(PalworldConfigIsDirty));
        var errors = ValidatePalworldConfiguration();
        PalworldConfigHasValidationErrors = errors.Count > 0;
        PalworldConfigValidationText = errors.Count == 0 ? "Configuration values pass client-side validation." : string.Join(" • ", errors.Take(4));
        (SavePalworldConfigurationCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (ApplyConfigPresetCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ResetConfigChangesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        DetectAndSyncConfigPreset();
    }

    private static readonly string[] QolPresetComparisonKeys =
    [
        "DayTimeSpeedRate", "NightTimeSpeedRate", "PlayerAutoHPRegeneRate", "PalAutoHPRegeneRate",
        "PlayerStomachDecreaceRate", "PalStomachDecreaceRate", "PlayerStaminaDecreaceRate", "PalStaminaDecreaceRate",
        "ItemWeightRate", "ItemCorruptionMultiplier", "EquipmentDurabilityDamageRate", "WorkSpeedRate",
        "MonsterFarmActionSpeedRate", "CollectionDropRate", "CollectionObjectRespawnSpeedRate",
        "BaseCampWorkerMaxNum", "BaseCampMaxNumInGuild", "BuildObjectDeteriorationDamageRate", "SupplyDropSpan"
    ];

    private void DetectAndSyncConfigPreset()
    {
        if (!PalworldConfigLoaded || PalworldSettings.Count == 0) return;
        string matched = "Custom";
        foreach (var candidate in new[] { "Official / Vanilla", "Balanced QoL", "Relaxed QoL" })
        {
            if (MatchesConfigPreset(candidate)) { matched = candidate; break; }
        }
        if (!string.Equals(_selectedConfigPreset, matched, StringComparison.Ordinal))
        {
            _selectedConfigPreset = matched;
            RaisePropertyChanged(nameof(SelectedConfigPreset));
            (ApplyConfigPresetCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private bool MatchesConfigPreset(string preset)
    {
        if (preset.StartsWith("Official", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var key in QolPresetComparisonKeys)
            {
                var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (setting is not null && !ConfigValuesEqual(setting.Value, setting.DefaultValue)) return false;
            }
            return true;
        }
        foreach (var pair in GetQolPreset(preset))
        {
            var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
            if (setting is not null && !ConfigValuesEqual(setting.Value, pair.Value)) return false;
        }
        return true;
    }

    private static bool ConfigValuesEqual(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        if (double.TryParse(a, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var da) &&
            double.TryParse(b, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var db))
            return Math.Abs(da - db) < 0.0001;
        return false;
    }

    private List<string> ValidatePalworldConfiguration()
    {
        var errors = new List<string>();
        foreach (var name in new[] { "PublicPort", "RESTAPIPort", "RCONPort" })
        {
            var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (setting is not null && (!int.TryParse(setting.Value, out var port) || port is < 1 or > 65535))
                errors.Add($"{setting.DisplayName}: port must be 1-65535");
        }
        var maxPlayers = PalworldSettings.FirstOrDefault(x => x.Name.Equals("ServerPlayerMaxNum", StringComparison.OrdinalIgnoreCase));
        if (maxPlayers is not null && (!int.TryParse(maxPlayers.Value, out var max) || max < 1))
            errors.Add("Maximum Players must be at least 1");
        return errors;
    }

    private void GenerateServerName()
    {
        var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals("ServerName", StringComparison.OrdinalIgnoreCase));
        if (setting is null) { PalworldConfigState = "ServerName is not present in the active configuration."; return; }
        setting.Value = GenerateRandomServerName();
        PalworldConfigState = "Generated a new server name locally. Save Changes to publish it to PalWorldSettings.ini.";
    }

    private static string GenerateRandomServerName()
    {
        string[] adjectives = ["Misty", "Golden", "Azure", "Moonlit", "Frostbound", "Wild", "Crystal", "Ember"];
        string[] nouns = ["Pal Haven", "Pal Realm", "Pal Isles", "Adventure", "Sanctuary", "Frontier", "Expedition", "World"];
        return $"{adjectives[Random.Shared.Next(adjectives.Length)]} {nouns[Random.Shared.Next(nouns.Length)]} {Random.Shared.Next(1000, 10000)}";
    }

    private void ResetPalworldConfigurationChanges()
    {
        foreach (var setting in PalworldSettings.Where(x => x.IsDirty).ToArray()) setting.Value = setting.OriginalValue;
        PalworldConfigState = "Unsaved configuration changes were discarded.";
        RefreshPalworldConfigurationState();
    }

    private void ApplySelectedConfigurationPreset()
    {
        if (!PalworldConfigLoaded) return;
        var presetKey = SelectedConfigPreset;
        var targets = GetQolPreset(presetKey);
        if (presetKey.StartsWith("Official", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var setting in PalworldSettings.Where(x => SimpleConfigurationNames.Contains(x.Name))) setting.Value = setting.DefaultValue;
        }
        else
        {
            foreach (var pair in targets)
            {
                var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));
                if (setting is not null) setting.Value = pair.Value;
            }
        }
        PalworldConfigState = $"{presetKey} preset loaded locally. Review highlighted changes, then Save Changes.";
        RefreshPalworldConfigurationState();
    }

    private static IReadOnlyDictionary<string, string> GetQolPreset(string preset)
    {
        var p = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (preset.StartsWith("Balanced", StringComparison.OrdinalIgnoreCase))
        {
            p["DayTimeSpeedRate"]="0.85"; p["NightTimeSpeedRate"]="1.10";
            p["PlayerAutoHPRegeneRate"]="1.5"; p["PalAutoHPRegeneRate"]="1.5";
            p["PlayerStomachDecreaceRate"]="0.75"; p["PalStomachDecreaceRate"]="0.75";
            p["PlayerStaminaDecreaceRate"]="0.80"; p["PalStaminaDecreaceRate"]="0.80";
            p["ItemWeightRate"]="0.65"; p["ItemCorruptionMultiplier"]="0.50"; p["EquipmentDurabilityDamageRate"]="0.75";
            p["WorkSpeedRate"]="1.25"; p["MonsterFarmActionSpeedRate"]="1.25";
            p["CollectionDropRate"]="1.5"; p["CollectionObjectRespawnSpeedRate"]="1.25";
            p["BaseCampWorkerMaxNum"]="30"; p["BaseCampMaxNumInGuild"]="6"; p["BuildObjectDeteriorationDamageRate"]="0";
        }
        else if (preset.StartsWith("Relaxed", StringComparison.OrdinalIgnoreCase))
        {
            p["DayTimeSpeedRate"]="0.75"; p["NightTimeSpeedRate"]="1.25";
            p["PlayerAutoHPRegeneRate"]="2.0"; p["PalAutoHPRegeneRate"]="2.0";
            p["PlayerStomachDecreaceRate"]="0.60"; p["PalStomachDecreaceRate"]="0.60";
            p["PlayerStaminaDecreaceRate"]="0.65"; p["PalStaminaDecreaceRate"]="0.65";
            p["ItemWeightRate"]="0.50"; p["ItemCorruptionMultiplier"]="0.25"; p["EquipmentDurabilityDamageRate"]="0.60";
            p["WorkSpeedRate"]="1.50"; p["MonsterFarmActionSpeedRate"]="1.50";
            p["CollectionDropRate"]="2.0"; p["CollectionObjectRespawnSpeedRate"]="1.50";
            p["SupplyDropSpan"]="90"; p["BaseCampWorkerMaxNum"]="35"; p["BaseCampMaxNumInGuild"]="8"; p["BuildObjectDeteriorationDamageRate"]="0";
        }
        return p;
    }

    public string ExportPalworldConfigurationJson()
    {
        var document = new PalworldConfigurationExportDocument
        {
            SourcePath = PalworldConfigPath,
            Settings = PalworldSettings.Select(x => new PalworldConfigurationExportSetting { Name = x.Name, Value = x.Value }).ToArray()
        };
        return JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
    }

    public void ReportPalworldConfigurationFileSuccess(string message) => PalworldConfigState = message;
    public void ReportPalworldConfigurationFileError(string message) => PalworldConfigState = message;

    public void ImportPalworldConfigurationJson(string json)
    {
        var document = JsonSerializer.Deserialize<PalworldConfigurationExportDocument>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The selected file does not contain a MystTiq Palworld configuration export.");
        if (document.SchemaVersion != 1) throw new InvalidOperationException($"Unsupported configuration export schema {document.SchemaVersion}.");
        var applied = 0;
        foreach (var imported in document.Settings)
        {
            if (string.IsNullOrWhiteSpace(imported.Name)) continue;
            var setting = PalworldSettings.FirstOrDefault(x => x.Name.Equals(imported.Name, StringComparison.OrdinalIgnoreCase));
            if (setting is null) continue;
            setting.Value = imported.Value ?? string.Empty;
            applied++;
        }
        PalworldConfigState = $"Imported {applied} setting value(s) into the local editor. Save Changes to write the server configuration.";
        RefreshPalworldConfigurationState();
        ApplyPalworldConfigurationFilter();
    }

    private async Task LoadPalworldConfigurationAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { PalworldConfigState = ex.Message; return; }
        IsBusy = true;
        PalworldConfigState = "Loading PalWorldSettings.ini…";
        try
        {
            var snapshot = await _api.GetPalworldConfigurationAsync(profile, BearerToken);
            foreach (var existing in PalworldSettings) existing.PropertyChanged -= PalworldSetting_PropertyChanged;
            PalworldSettings.Clear();
            foreach (var setting in snapshot.Settings)
            {
                setting.MarkClean();
                setting.PropertyChanged += PalworldSetting_PropertyChanged;
                PalworldSettings.Add(setting);
            }
            PalworldConfigPath = snapshot.ConfigurationPath;
            PalworldConfigLoaded = snapshot.Exists;
            PalworldConfigState = snapshot.Detail;
            RebuildPalworldConfigurationCategories();
            RebuildSimplePalworldSettings();
            ApplyPalworldConfigurationFilter();
            RefreshPalworldConfigurationState();
        }
        catch (Exception ex) { PalworldConfigLoaded = false; PalworldConfigState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task SavePalworldConfigurationAsync()
    {
        if (!PalworldConfigLoaded) return;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { PalworldConfigState = ex.Message; return; }
        IsBusy = true;
        PalworldConfigState = "Saving PalWorldSettings.ini…";
        try
        {
            var result = await _api.SavePalworldConfigurationAsync(profile, PalworldSettings.ToArray(), BearerToken);
            PalworldConfigState = result.Success ? result.Message : string.Join("; ", result.ValidationErrors.DefaultIfEmpty(result.Message));
            if (result.Success) await LoadPalworldConfigurationAsync();
        }
        catch (Exception ex) { PalworldConfigState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task LoadConfigurationAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { ConfigState = ex.Message; return; }

        IsBusy = true;
        ConfigState = "Loading…";
        try
        {
            var config = await _api.GetEditableConfigurationAsync(profile, BearerToken);

            ConfigApiEnabled = config.Api.Enabled;
            ConfigBindAddress = config.Api.BindAddress;
            ConfigPort = config.Api.Port;

            StartupTimeoutSeconds = config.Lifecycle.StartupTimeoutSeconds;
            StopTimeoutSeconds = config.Lifecycle.StopTimeoutSeconds;
            ServicePollSeconds = config.Lifecycle.ServicePollSeconds;
            RecoveryBackoffSeconds = config.Lifecycle.RecoveryBackoffSeconds;
            MaximumRecoveryAttempts = config.Lifecycle.MaximumRecoveryAttempts;
            RecoveryWindowSeconds = config.Lifecycle.RecoveryWindowSeconds;

            ConfigServerRoot = config.Server.ServerRoot;
            ConfigSteamCmdPath = config.Server.SteamCmdPath;
            ConfigBackupRoot = config.Server.BackupRoot;
            ConfigRuntimeRoot = config.Server.RuntimeRoot;
            ConfigLaunchArguments = string.Join(Environment.NewLine, config.Server.LaunchArguments);

            ConfigSecurityText =
                $"Authentication: {(config.AuthenticationEnabled ? "Enabled" : "Disabled")} · TLS: {(config.TlsEnabled ? "Enabled" : "Disabled")}";

            ConfigLoaded = true;
            ConfigState = config.RestartRequired
                ? "Loaded · service restart already required"
                : "Loaded";
            ValidateWorkspacePaths();
            RaiseWorkspaceSummaryProperties();
        }
        catch (Exception ex)
        {
            ConfigLoaded = false;
            ConfigState = ex.Message;
            WorkspaceState = $"Workspace unavailable: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    private async Task SaveConfigurationAsync()
    {
        if (!ConfigLoaded) return;

        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { ConfigState = ex.Message; return; }

        var editable = new EditableConfigurationDto
        {
            Api = new EditableApiConfigurationDto
            {
                Enabled = ConfigApiEnabled,
                BindAddress = ConfigBindAddress,
                Port = ConfigPort
            },
            Lifecycle = new EditableLifecycleConfigurationDto
            {
                StartupTimeoutSeconds = StartupTimeoutSeconds,
                StopTimeoutSeconds = StopTimeoutSeconds,
                ServicePollSeconds = ServicePollSeconds,
                RecoveryBackoffSeconds = RecoveryBackoffSeconds,
                MaximumRecoveryAttempts = MaximumRecoveryAttempts,
                RecoveryWindowSeconds = RecoveryWindowSeconds
            },
            Server = new EditableServerConfigurationDto
            {
                ServerRoot = ConfigServerRoot,
                SteamCmdPath = ConfigSteamCmdPath,
                BackupRoot = ConfigBackupRoot,
                RuntimeRoot = ConfigRuntimeRoot,
                LaunchArguments = ConfigLaunchArguments
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            }
        };

        IsBusy = true;
        ConfigState = "Saving…";
        try
        {
            var result = await _api.SaveEditableConfigurationAsync(profile, editable, BearerToken);

            if (!result.Success)
            {
                ConfigState = result.ValidationErrors.Count > 0
                    ? "Validation failed: " + string.Join("; ", result.ValidationErrors)
                    : result.Message;
                return;
            }

            ConfigState = result.RestartRequired
                ? "Saved · restart MystTiq service to apply"
                : "Saved";
            WorkspaceState = result.RestartRequired
                ? "Paths saved with rollback backup · restart the MystTiq service to apply."
                : "Paths saved through the managed headless configuration service.";
        }
        catch (Exception ex) { ConfigState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RefreshEnvironmentAsync()
    {
        if (SelectedProfile is null) return;
        EnvironmentHealthText = "Checking…";
        try
        {
            var snapshot = await _api.GetEnvironmentChecklistAsync(SelectedProfile, BearerToken);
            EnvironmentItems.Clear();
            foreach (var item in snapshot.Items) EnvironmentItems.Add(item);
            var attention = Math.Max(0, snapshot.TotalCount - snapshot.ReadyCount);
            EnvironmentHealthText = attention == 0
                ? $"{snapshot.ReadyCount} / {snapshot.TotalCount} READY"
                : $"{snapshot.ReadyCount} / {snapshot.TotalCount} READY · {attention} NEED ATTENTION";
            RaisePropertyChanged(nameof(SetupReadyCountText));
            RaisePropertyChanged(nameof(SetupAttentionCountText));
            RaisePropertyChanged(nameof(SetupComponentCountText));
            RaisePropertyChanged(nameof(WorkspaceDiscoveryText));
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

    private async Task CheckEnvironmentUpdatesAsync()
    {
        SetupOperationState = "CHECKING";
        SetupOperationTitle = "Checking server distribution";
        SetupOperationDetail = "Refreshing environment readiness and the current SteamCMD update plan.";
        SetupOperationProgress = 20;
        await RefreshEnvironmentAsync();
        SetupOperationProgress = 55;
        await PreviewDistributionPlanAsync();
        SetupOperationProgress = 100;
        SetupOperationState = "COMPLETE";
        SetupOperationTitle = "Update readiness checked";
        SetupRecentActivity = $"Recent activity: Check for updates · {DateTime.Now:t}.";
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
                await LoadPalworldConfigurationAsync();
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
                Dispatcher.UIThread.Post(async () => await InstallMissingEnvironmentAsync());
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
        Dispatcher.UIThread.Post(async () => await VerifyEnvironmentAsync());
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
            ServerInstallState = status.ServerExecutableExists ? "Installed" : "Missing";
            DistributionState = status.ServerExecutableExists ? "Ready" : "Setup required";
            DistributionDetail = status.Detail;
        }
        catch (Exception ex)
        {
            DistributionState = "Unavailable";
            DistributionDetail = ex.Message;
        }
        finally { IsBusy = false; }
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
        DistributionState = ValidateServerFiles ? "Updating / validating…" : "Updating…";
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

    private async Task RefreshWorldTransactionsAsync()
    {
        await ValidateActiveWorldAsync();
        if (SelectedProfile is null) return;
        try
        {
            var history = await _api.GetWorldTransactionsAsync(SelectedProfile, BearerToken);
            WorldTransactionHistory.Clear();
            foreach (var item in history) WorldTransactionHistory.Add(item);
        }
        catch (Exception ex) { WorldTransactionState = "Transaction history: " + ex.Message; }
        await RefreshOperationsAsync();
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
        catch (Exception ex) { WorldTransactionState = "Operations: " + ex.Message; }
    }

    private async Task ValidateActiveWorldAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var report = await _api.ValidateActiveWorldAsync(SelectedProfile, BearerToken);
            WorldValidationFindings.Clear();
            foreach (var finding in report.Findings) WorldValidationFindings.Add(finding);
            WorldValidationState = report.Summary;
        }
        catch (Exception ex) { WorldValidationState = ex.Message; }
        finally { IsBusy = false; }
    }

    public async Task AnalyzeWorldArchiveAsync(Stream stream, string fileName)
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var preview = await _api.AnalyzeWorldArchiveAsync(SelectedProfile, WorldTransactionMode, stream, BearerToken);
            WorldPreviewToken = preview.PreviewToken;
            WorldTransactionConfirmed = false;
            WorldTransactionPlanSteps.Clear();
            foreach (var step in preview.Steps) WorldTransactionPlanSteps.Add(step);
            WorldTransactionState = $"{fileName}: {preview.EntryCount} entries, {preview.ArchiveBytes / 1024d / 1024d:F2} MB. Preview expires {preview.ExpiresUtc.ToLocalTime():t}.";
        }
        catch (Exception ex) { WorldPreviewToken = string.Empty; WorldTransactionState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task ApplyWorldTransactionAsync()
    {
        if (SelectedProfile is null || string.IsNullOrWhiteSpace(WorldPreviewToken)) return;
        IsBusy = true;
        try
        {
            var result = await _api.ApplyWorldTransactionAsync(SelectedProfile, WorldPreviewToken, WorldTransactionConfirmed, BearerToken);
            WorldTransactionState = result.Message + (string.IsNullOrWhiteSpace(result.SafetyBackup) ? string.Empty : $" Safety backup: {result.SafetyBackup}");
            WorldPreviewToken = string.Empty;
            WorldTransactionConfirmed = false;
            WorldTransactionPlanSteps.Clear();
            await RefreshWorldExplorerAsync();
            var history = await _api.GetWorldTransactionsAsync(SelectedProfile, BearerToken);
            WorldTransactionHistory.Clear(); foreach (var item in history) WorldTransactionHistory.Add(item);
            await RefreshOperationsAsync();
        }
        catch (Exception ex) { WorldTransactionState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task PreviewGuildOwnershipAsync()
    {
        if (SelectedProfile is null || SelectedExplorerGuild is null || string.IsNullOrWhiteSpace(GuildOperationPlayerId)) return;
        IsBusy = true;
        GuildOperationStatusText = "Previewing…";
        try
        {
            var preview = await _api.PreviewGuildOwnershipAsync(SelectedProfile, GuildOperationApiName(GuildOperationType), SelectedExplorerGuild.GuildId, GuildOperationPlayerId, BearerToken);
            GuildOperationPreviewToken = preview.CanApply ? preview.PreviewToken : string.Empty;
            GuildOperationConfirmed = false;
            GuildOperationStatusText = string.Join(" ", preview.Findings) +
                (preview.CanApply ? $" Preview expires {preview.ExpiresUtc.ToLocalTime():t}." : string.Empty);
        }
        catch (Exception ex) { GuildOperationPreviewToken = string.Empty; GuildOperationStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task ApplyGuildOwnershipAsync()
    {
        if (SelectedProfile is null || string.IsNullOrWhiteSpace(GuildOperationPreviewToken)) return;
        IsBusy = true;
        try
        {
            var result = await _api.ApplyGuildOwnershipAsync(SelectedProfile, GuildOperationPreviewToken, GuildOperationConfirmed, BearerToken);
            GuildOperationStatusText = result.Message + (string.IsNullOrWhiteSpace(result.SafetyBackup) ? string.Empty : $" Safety backup: {result.SafetyBackup}");
            GuildOperationPreviewToken = string.Empty;
            GuildOperationConfirmed = false;
            GuildOperationPlayerId = string.Empty;
            if (result.Success) await RefreshPlayerGuildExplorerAsync();
            await RefreshOperationsAsync();
        }
        catch (Exception ex) { GuildOperationStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task PreviewBaseTransferAsync()
    {
        if (SelectedProfile is null || SelectedExplorerBase is null || string.IsNullOrWhiteSpace(BaseTransferTargetGuildId)) return;
        IsBusy = true;
        BaseTransferStatusText = "Previewing…";
        try
        {
            var preview = await _api.PreviewBaseOwnershipTransferAsync(SelectedProfile, SelectedExplorerBase.BaseId, BaseTransferTargetGuildId, BearerToken);
            BaseTransferPreviewToken = preview.CanApply ? preview.PreviewToken : string.Empty;
            BaseTransferConfirmed = false;
            BaseTransferStatusText = string.Join(" ", preview.Findings) +
                (preview.CanApply ? $" Preview expires {preview.ExpiresUtc.ToLocalTime():t}." : string.Empty);
        }
        catch (Exception ex) { BaseTransferPreviewToken = string.Empty; BaseTransferStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task ApplyBaseTransferAsync()
    {
        if (SelectedProfile is null || string.IsNullOrWhiteSpace(BaseTransferPreviewToken)) return;
        IsBusy = true;
        try
        {
            var result = await _api.ApplyBaseOwnershipTransferAsync(SelectedProfile, BaseTransferPreviewToken, BaseTransferConfirmed, BearerToken);
            BaseTransferStatusText = result.Message + (string.IsNullOrWhiteSpace(result.SafetyBackup) ? string.Empty : $" Safety backup: {result.SafetyBackup}");
            BaseTransferPreviewToken = string.Empty;
            BaseTransferConfirmed = false;
            BaseTransferTargetGuildId = string.Empty;
            if (result.Success) await RefreshPlayerGuildExplorerAsync();
            await RefreshOperationsAsync();
        }
        catch (Exception ex) { BaseTransferStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task PreviewBaseRecoveryAsync()
    {
        if (SelectedProfile is null || SelectedExplorerBase is null) return;
        IsBusy = true;
        BaseRecoveryStatusText = "Previewing…";
        try
        {
            var preview = await _api.PreviewBaseRecoveryAsync(SelectedProfile, SelectedExplorerBase.BaseId, BearerToken);
            BaseRecoveryPreviewToken = preview.CanApply ? preview.PreviewToken : string.Empty;
            BaseRecoveryConfirmed = false;
            BaseRecoveryStatusText = string.Join(" ", preview.Findings) +
                (preview.CanApply ? $" Preview expires {preview.ExpiresUtc.ToLocalTime():t}." : string.Empty);
        }
        catch (Exception ex) { BaseRecoveryPreviewToken = string.Empty; BaseRecoveryStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task ApplyBaseRecoveryAsync()
    {
        if (SelectedProfile is null || string.IsNullOrWhiteSpace(BaseRecoveryPreviewToken)) return;
        IsBusy = true;
        try
        {
            var result = await _api.ApplyBaseRecoveryAsync(SelectedProfile, BaseRecoveryPreviewToken, BaseRecoveryConfirmed, BearerToken);
            BaseRecoveryStatusText = result.Message + (string.IsNullOrWhiteSpace(result.SafetyBackup) ? string.Empty : $" Safety backup: {result.SafetyBackup}");
            BaseRecoveryPreviewToken = string.Empty;
            BaseRecoveryConfirmed = false;
            if (result.Success) await RefreshPlayerGuildExplorerAsync();
            await RefreshOperationsAsync();
        }
        catch (Exception ex) { BaseRecoveryStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    public string ExportWorldValidationReport()
    {
        var lines = new List<string> { "MystTiq World Validation Report", WorldValidationState, $"Exported: {DateTimeOffset.Now:O}", string.Empty };
        lines.AddRange(WorldValidationFindings.Select(x => $"[{x.Severity}] {x.Category} / {x.Check}: {x.Detail}"));
        return string.Join(Environment.NewLine, lines);
    }

    private async Task RefreshWorldExplorerAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { WorldExplorerDetail = ex.Message; return; }

        IsBusy = true;
        WorldExplorerState = "Scanning…";
        try
        {
            var worldTask = _api.GetWorldExplorerAsync(profile, BearerToken);
            var evidenceTask = _api.GetPlayerGuildExplorerAsync(profile, BearerToken);
            await Task.WhenAll(worldTask, evidenceTask);
            var snapshot = await worldTask;
            var evidence = await evidenceTask;

            WorldCandidates.Clear();
            foreach (var world in snapshot.Worlds)
                WorldCandidates.Add(world);

            WorldFiles.Clear();
            foreach (var file in snapshot.Files)
                WorldFiles.Add(file);

            SelectedWorldFile = WorldFiles.FirstOrDefault();

            ActiveWorldIdText = snapshot.ActiveWorldId ?? "Not resolved";
            WorldCountText = snapshot.WorldCount.ToString();
            WorldFileCountText = snapshot.FileCount.ToString();
            WorldPlayerSaveCountText = snapshot.PlayerSaveCount.ToString();
            WorldSizeText = $"{snapshot.TotalSizeBytes / 1024d / 1024d:F2} MB";
            WorldSaveRootText = snapshot.SaveRoot;
            WorldSaveDataCountText = snapshot.Statistics.SaveDataFiles.ToString();
            WorldDiagnosticCountText = snapshot.Statistics.DiagnosticFiles.ToString();
            WorldEmptyCountText = snapshot.Statistics.EmptyFiles.ToString();
            WorldAgeRangeText = snapshot.Statistics.AgeRangeText;
            WorldIntegrityState = snapshot.Integrity.State;
            WorldIntegrityFindings.Clear();
            foreach (var finding in snapshot.Integrity.Findings) WorldIntegrityFindings.Add(finding);
            if (WorldIntegrityFindings.Count == 0) WorldIntegrityFindings.Add("Required world files are present and no structural issue was detected.");
            WorldInspectorPlayerCountText = evidence.Players.Count.ToString();
            WorldInspectorGuildCountText = evidence.Guilds.Count.ToString();
            WorldInspectorBaseCountText = evidence.Guilds.Sum(x => x.BaseIds.Count).ToString();
            WorldExplorerState = snapshot.Available ? "Ready" : "Unavailable";
            WorldExplorerDetail = snapshot.Detail;
        }
        catch (Exception ex)
        {
            WorldExplorerState = "Unavailable";
            WorldExplorerDetail = ex.Message;
        }
        finally { IsBusy = false; }
    }

    private async Task RefreshPlayerGuildExplorerAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { PlayerGuildDetail = ex.Message; return; }

        IsBusy = true;
        PlayerGuildState = "Scanning…";
        try
        {
            var selectedGuildId = SelectedExplorerGuild?.GuildId;
            var selectedBaseId = SelectedExplorerBase?.BaseId;
            var snapshot = await _api.GetPlayerGuildExplorerAsync(profile, BearerToken);

            ExplorerPlayers.Clear();
            foreach (var player in snapshot.Players)
                ExplorerPlayers.Add(player);

            ExplorerGuilds.Clear();
            foreach (var guild in snapshot.Guilds)
                ExplorerGuilds.Add(guild);

            ExplorerBases.Clear();
            foreach (var guild in snapshot.Guilds)
            {
                foreach (var baseId in guild.BaseIds)
                {
                    ExplorerBases.Add(new BaseExplorerItemDto
                    {
                        BaseId = baseId,
                        GuildId = guild.GuildId,
                        GuildName = guild.GuildName,
                        LeaderPlayerId = guild.LeaderPlayerId,
                        LeaderName = guild.LeaderName,
                        OwnerHealth = guild.Health,
                        Evidence = $"Decoded GroupSaveDataMap guild base_ids ownership reference for {guild.GuildName} ({guild.GuildId})."
                    });
                }
            }

            ApplyGuildFilters();
            ApplyBaseFilters();

            PlayerGuildWarnings.Clear();
            foreach (var warning in snapshot.Warnings)
                PlayerGuildWarnings.Add(warning);

            SelectedExplorerPlayer = ExplorerPlayers.FirstOrDefault();
            SelectedExplorerGuild = !string.IsNullOrWhiteSpace(selectedGuildId)
                ? ExplorerGuilds.FirstOrDefault(x => string.Equals(x.GuildId, selectedGuildId, StringComparison.OrdinalIgnoreCase))
                : FilteredExplorerGuilds.FirstOrDefault();
            SelectedExplorerBase = !string.IsNullOrWhiteSpace(selectedBaseId)
                ? ExplorerBases.FirstOrDefault(x => string.Equals(x.BaseId, selectedBaseId, StringComparison.OrdinalIgnoreCase))
                : FilteredExplorerBases.FirstOrDefault();

            PlayerRecordCountText = snapshot.Players.Count.ToString();
            GuildRecordCountText = snapshot.Guilds.Count.ToString();
            SemanticStateText = snapshot.SemanticAvailable
                ? "Decoded guild semantics available"
                : "Player-save identities only";
            SemanticSourceText = snapshot.SemanticSource;
            PlayerGuildState = snapshot.Available ? "Ready" : "Unavailable";
            PlayerGuildDetail = snapshot.Detail;
        }
        catch (Exception ex)
        {
            PlayerGuildState = "Unavailable";
            PlayerGuildDetail = ex.Message;
        }
        finally { IsBusy = false; }
    }

    private void ApplyGuildFilters()
    {
        var selectedId = SelectedExplorerGuild?.GuildId;
        var search = GuildSearchText.Trim();
        var filtered = ExplorerGuilds.Where(guild =>
        {
            if (!string.IsNullOrWhiteSpace(search) &&
                !guild.GuildId.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !guild.GuildName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !guild.LeaderPlayerId.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !guild.LeaderName.Contains(search, StringComparison.OrdinalIgnoreCase)) return false;
            if (SelectedGuildStatusFilter == "Healthy" && !guild.Health.Equals("Healthy", StringComparison.OrdinalIgnoreCase)) return false;
            if (SelectedGuildStatusFilter == "Needs Review" && guild.Health.Equals("Healthy", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }).ToArray();
        FilteredExplorerGuilds.Clear();
        foreach (var guild in filtered) FilteredExplorerGuilds.Add(guild);
        GuildVisibleCountText = $"{filtered.Length} visible / {ExplorerGuilds.Count} guilds";
        if (!string.IsNullOrWhiteSpace(selectedId))
            SelectedExplorerGuild = filtered.FirstOrDefault(x => string.Equals(x.GuildId, selectedId, StringComparison.OrdinalIgnoreCase));
        SelectedExplorerGuild ??= filtered.FirstOrDefault();
    }

    private void ApplyBaseFilters()
    {
        var selectedId = SelectedExplorerBase?.BaseId;
        var search = BaseSearchText.Trim();
        var filtered = ExplorerBases.Where(item =>
        {
            if (!string.IsNullOrWhiteSpace(search) &&
                !item.BaseId.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !item.GuildId.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !item.GuildName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !item.LeaderName.Contains(search, StringComparison.OrdinalIgnoreCase)) return false;
            if (SelectedBaseStatusFilter == "Healthy Owner" && !item.OwnerHealth.Equals("Healthy", StringComparison.OrdinalIgnoreCase)) return false;
            if (SelectedBaseStatusFilter == "Needs Review" && item.OwnerHealth.Equals("Healthy", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }).ToArray();
        FilteredExplorerBases.Clear();
        foreach (var item in filtered) FilteredExplorerBases.Add(item);
        BaseVisibleCountText = $"{filtered.Length} visible / {ExplorerBases.Count} bases";
        if (!string.IsNullOrWhiteSpace(selectedId))
            SelectedExplorerBase = filtered.FirstOrDefault(x => string.Equals(x.BaseId, selectedId, StringComparison.OrdinalIgnoreCase));
        SelectedExplorerBase ??= filtered.FirstOrDefault();
    }

    private async Task OpenSelectedGuildLeaderAsync()
    {
        var leaderId = SelectedExplorerGuild?.LeaderPlayerId;
        if (string.IsNullOrWhiteSpace(leaderId)) return;
        SelectedPage = NavigationPage.Players;
        await RefreshPlayersPageAsync();
        SelectedPlayerRecord = PlayerRecords.FirstOrDefault(x => string.Equals(x.PlayerId, leaderId, StringComparison.OrdinalIgnoreCase));
        if (SelectedPlayerRecord is not null)
            await LoadSelectedPlayerMetadataAsync();
    }

    public string ExportVisibleGuildsCsv()
    {
        static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
        var builder = new StringBuilder("GuildId,GuildName,Health,LeaderPlayerId,LeaderName,MemberCount,BaseCount,MemberPlayerIds,BaseIds\r\n");
        foreach (var guild in FilteredExplorerGuilds)
            builder.AppendLine(string.Join(',', Csv(guild.GuildId), Csv(guild.GuildName), Csv(guild.Health), Csv(guild.LeaderPlayerId), Csv(guild.LeaderName), Csv(guild.MemberCount.ToString()), Csv(guild.BaseCount.ToString()), Csv(string.Join(';', guild.MemberPlayerIds)), Csv(string.Join(';', guild.BaseIds))));
        return builder.ToString();
    }

    public string ExportVisibleBasesCsv()
    {
        static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
        var builder = new StringBuilder("BaseId,GuildId,GuildName,LeaderPlayerId,LeaderName,OwnerHealth,Evidence\r\n");
        foreach (var item in FilteredExplorerBases)
            builder.AppendLine(string.Join(',', Csv(item.BaseId), Csv(item.GuildId), Csv(item.GuildName), Csv(item.LeaderPlayerId), Csv(item.LeaderName), Csv(item.OwnerHealth), Csv(item.Evidence)));
        return builder.ToString();
    }

    private async Task RefreshModsAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { ModSummary = ex.Message; return; }
        IsBusy = true;
        try { ApplyModInventory(await _api.GetModsAsync(profile, BearerToken)); }
        catch (Exception ex) { ModState = "Unavailable"; ModSummary = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task VerifyModsAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { ModSummary = ex.Message; return; }
        IsBusy = true;
        ModState = "Verifying…";
        try
        {
            var result = await _api.VerifyModsAsync(profile, BearerToken);
            ModItems.Clear(); foreach (var mod in result.Mods) ModItems.Add(mod);
            SelectedMod = ModItems.FirstOrDefault();
            RaisePropertyChanged(nameof(ModsNeedingAttention)); RaisePropertyChanged(nameof(HasModsNeedingAttention));
            ModHealth = result.OverallHealth; ModInstalledText = result.Installed.ToString();
            ModConfirmedText = result.RuntimeConfirmed.ToString(); ModUnverifiedText = result.ActiveUnverified.ToString();
            ModDisabledText = result.Disabled.ToString(); ModIssuesText = result.Attention.ToString();
            ModState = "Verified"; ModSummary = result.Summary;
        }
        catch (Exception ex) { ModState = "Verification failed"; ModSummary = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task SetSelectedModEnabledAsync(bool enabled)
    {
        if (SelectedMod is null) return;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { ModSummary = ex.Message; return; }
        var selected = SelectedMod;
        IsBusy = true;
        try
        {
            var result = await _api.SetModEnabledAsync(profile, selected.Type, selected.Package, enabled, BearerToken);
            ModSummary = result.Message;
            ApplyModInventory(await _api.GetModsAsync(profile, BearerToken));
        }
        catch (Exception ex) { ModState = "State change failed"; ModSummary = ex.Message; }
        finally { IsBusy = false; }
    }

    public async Task InstallModZipAsync(Stream archive, string suggestedPackage)
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { ModSummary = ex.Message; return; }
        var package = string.IsNullOrWhiteSpace(ModInstallPackage) ? Path.GetFileNameWithoutExtension(suggestedPackage) : ModInstallPackage.Trim();
        IsBusy = true; ModState = "Installing…";
        try { var result = await _api.InstallModZipAsync(profile, ModInstallType, package, archive, BearerToken); ModSummary = result.Message; ApplyModInventory(await _api.GetModsAsync(profile, BearerToken)); }
        catch (Exception ex) { ModState = "Install failed"; ModSummary = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task DeleteSelectedModAsync()
    {
        if (SelectedMod is null) return; var selected = SelectedMod;
        await RunModMutationAsync((profile) => _api.DeleteModAsync(profile, selected.Type, selected.Package, BearerToken), "Delete failed");
    }

    private async Task RollbackSelectedModAsync()
    {
        if (SelectedMod is null) return; var selected = SelectedMod;
        await RunModMutationAsync((profile) => _api.RollbackModAsync(profile, selected.Type, selected.Package, BearerToken), "Rollback failed");
    }

    private async Task SetAllModsEnabledAsync(bool enabled) =>
        await RunModMutationAsync(profile => _api.SetAllModsEnabledAsync(profile, enabled, BearerToken), "Bulk state change failed");

    private async Task RepairModsAsync() =>
        await RunModMutationAsync(profile => _api.RepairModsAsync(profile, BearerToken), "Repair failed");

    private async Task ScanWorkshopModsAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { WorkshopScanState = ex.Message; return; }
        IsBusy = true;
        WorkshopScanState = "Scanning local Steam Workshop content…";
        try
        {
            var result = await _api.ScanWorkshopModsAsync(profile, BearerToken);
            WorkshopItems.Clear(); foreach (var item in result.Items) WorkshopItems.Add(item);
            SelectedWorkshopItem = WorkshopItems.FirstOrDefault();
            WorkshopScanState = result.Detail;
        }
        catch (Exception ex) { WorkshopScanState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task ImportSelectedWorkshopModAsync()
    {
        if (SelectedWorkshopItem is null) return;
        var selected = SelectedWorkshopItem;
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { WorkshopScanState = ex.Message; return; }
        IsBusy = true;
        try
        {
            var result = await _api.ImportWorkshopModAsync(profile, selected.WorkshopId, BearerToken);
            WorkshopScanState = result.Message;
            ApplyModInventory(await _api.GetModsAsync(profile, BearerToken));
            var rescan = await _api.ScanWorkshopModsAsync(profile, BearerToken);
            WorkshopItems.Clear(); foreach (var item in rescan.Items) WorkshopItems.Add(item);
            SelectedWorkshopItem = WorkshopItems.FirstOrDefault(x => x.WorkshopId == selected.WorkshopId) ?? WorkshopItems.FirstOrDefault();
        }
        catch (Exception ex) { WorkshopScanState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task RunModMutationAsync(Func<ConnectionProfile, Task<ModMutationResultDto>> operation, string failureState)
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { ModSummary = ex.Message; return; }
        IsBusy = true;
        try { var result = await operation(profile); ModSummary = result.Message; ApplyModInventory(await _api.GetModsAsync(profile, BearerToken)); }
        catch (Exception ex) { ModState = failureState; ModSummary = ex.Message; }
        finally { IsBusy = false; }
    }

    private void ApplyModInventory(ModInventoryDto inventory)
    {
        ModItems.Clear(); foreach (var mod in inventory.Mods) ModItems.Add(mod);
        SelectedMod = ModItems.FirstOrDefault();
        RaisePropertyChanged(nameof(ModsNeedingAttention)); RaisePropertyChanged(nameof(HasModsNeedingAttention));
        ModState = "Ready"; ModSummary = inventory.Summary; ModHealth = inventory.OverallHealth;
        ModInstalledText = inventory.Installed.ToString(); ModConfirmedText = inventory.RuntimeConfirmed.ToString();
        ModUnverifiedText = inventory.ActiveUnverified.ToString(); ModDisabledText = inventory.Disabled.ToString();
        ModIssuesText = inventory.ConfirmedIssues.ToString();
        Ue4ssHealth = inventory.Ue4ss.HealthState; Ue4ssDetection = inventory.Ue4ss.DetectionMethod;
        Ue4ssActiveRoot = inventory.Ue4ss.ActiveModsRoot; Ue4ssRuntimeRoot = inventory.Ue4ss.RuntimeRootText;
        Ue4ssWarning = inventory.Ue4ss.WarningMessage;
        Ue4ssInstalledVersion = inventory.Ue4ss.InstalledVersion;
    }

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
        if (SelectedProfile?.Id != ConnectionProfile.LocalDefault.Id) return false;

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

        IsBusy = true;
        ConnectionState = activity;
        LifecycleStatusText = $"{activity.TrimEnd('…')} PalServer through the MystTiq management service.";
        Detail = LifecycleStatusText;

        try
        {
            var result = await operation(profile, BearerToken, CancellationToken.None);
            if (result.Snapshot is not null)
                ApplyStatus(result.Snapshot);

            LifecycleStatusText = result.Message ?? (result.Success ? "Lifecycle operation completed." : $"Lifecycle operation failed (exit code {result.ExitCode}).");
            Detail = LifecycleStatusText;
            ManagementApiConnected = true;
            ConnectionState = result.Success ? "Connected" : "Operation failed";

            // Always reacquire one coherent authoritative sample after a mutation.
            var poll = await _api.GetStatusPollingAsync(profile, 120, BearerToken);
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
            IsBusy = false;
        }
    }

    private void ApplyStatus(ServerStatusDto status)
    {
        ServerState = status.Ready
            ? "Running / Ready"
            : status.CrashDetected
                ? "Crash detected"
                : "Stopped / Not ready";

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
        DashboardHealthText = status.Ready ? "READY" : status.CrashDetected ? "ATTENTION" : "STOPPED";
        DashboardHealthDetail = status.Ready
            ? $"PalServer healthy · {ListenerText}"
            : status.CrashDetected ? "Crash evidence detected; open Doctor." : "Stopped intentionally or awaiting start.";
        DashboardSessionText = status.Ready ? $"Session {UptimeText}" : "No active PalServer session";
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

    private static string FormatTime(DateTimeOffset value) =>
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

        return new ConnectionProfile(
            existingId ?? Guid.NewGuid().ToString("N"),
            ProfileName.Trim(),
            uri,
            pin);
    }

    private void RaisePageVisibility()
    {
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
        RaisePropertyChanged(nameof(IsModsPage));
        RaisePropertyChanged(nameof(IsDoctorPage));
        RaisePropertyChanged(nameof(IsDiagnosticsPage));
        RaisePropertyChanged(nameof(IsSettingsPage));
        RaisePropertyChanged(nameof(IsServerSetupPage));
        RaisePropertyChanged(nameof(IsUpdateCenterPage));
        RaisePropertyChanged(nameof(IsBasesPage));
        RaisePropertyChanged(nameof(IsGuildsPage));
        RaisePropertyChanged(nameof(IsConsolePage));
        RaisePropertyChanged(nameof(IsActivityAuditPage));
        RaisePropertyChanged(nameof(IsNotificationsPage));
        RaisePropertyChanged(nameof(IsAutomationPage));
        RaisePropertyChanged(nameof(IsSecurityPage));
        RaisePropertyChanged(nameof(IsAlertCenterPage));
        RaisePropertyChanged(nameof(IsFleetPage));
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
    }
}

internal sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

internal sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;

    public RelayCommand(Action<T?> execute) => _execute = execute;

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter)
    {
        if (parameter is T typed)
            _execute(typed);
        else
            _execute(default);
    }

    event EventHandler? ICommand.CanExecuteChanged
    {
        add { }
        remove { }
    }
}

internal sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _running;

    public AsyncCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => !_running && (_canExecute?.Invoke() ?? true);
    public event EventHandler? CanExecuteChanged;

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter))
            return;

        _running = true;
        RaiseCanExecuteChanged();
        try
        {
            await _execute();
        }
        finally
        {
            _running = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
