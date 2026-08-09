using PalworldManager.Models;
using PalworldManager.Services;
using PalworldManager.Services.Infrastructure;

namespace PalworldManager;

public partial class MainWindow : Window
{
    private const int WorldImportPhase = 6;
    private readonly SettingsStore store=new();
    private readonly AppSettings settings;
    private readonly ServerService server;
    private readonly NavigationCoordinator navigation;
    private readonly ServerStatusPresentationService statusPresentation = new();
    private readonly PageOperationCoordinator pageOperations = new();
    private readonly NotificationService infrastructureNotifications = new();
    private readonly ScanCache scanCache = new();
    private readonly ActiveWorldContextService activeWorldContext;
    private readonly WorldDiscoverySnapshotService worldDiscovery;
    private readonly BackupService backups;
    private readonly ConfigService config;
    private readonly ModService mods;
    private readonly ModHealthEvaluationService modHealthEvaluation = new();
    private readonly ModVerificationService modVerification;
    private readonly ModRepairRecommendationEngine modRepairRecommendations = new();
    private readonly ModLifecycleCoordinator modLifecycle;
    private readonly ModVerificationReportExportService modVerificationReportExporter = new();
    private readonly ModCompatibilityService modCompatibility;
    private readonly SessionLogService sessionLog;
    private readonly CrashAnalyzerService crashAnalyzer;
    private readonly ServerDoctorService serverDoctor;
    private readonly Ue4ssReleaseService ue4ssReleases = new();
    private readonly RconClient rcon = new();
    private readonly PlayerHistoryService playerHistory;
    private readonly PlayerAdministrationService playerAdministration;
    private readonly PlayerHealthService playerHealth;
    private readonly GuildService guilds;
    private readonly GuildTransactionService guildTransactions;
    private GuildRepairPlan guildRepairPlan = new();
    private ObservableCollection<GuildRow> guildRows = [];
    private ObservableCollection<GuildMemberRow> guildMemberRows = [];
    private ObservableCollection<GuildBaseRow> guildBaseRows = [];
    private ObservableCollection<GuildWorldPlayerRow> guildWorldPlayerRows = [];
    private ICollectionView? guildView;
    private GuildWorldSnapshot? currentGuildSnapshot;
    private bool showAllKnownPlayers = true;
    private readonly List<string> consoleLines = [];
    private readonly System.Collections.Concurrent.ConcurrentQueue<(string Display, string Persistent)> pendingUiLogs = new();
    private int logFlushScheduled;
    private readonly List<string> rconHistory = [];
    private readonly HttpClient modMetadataClient = new() { Timeout = ApplicationConstants.Network.StandardRequestTimeout };
    private int rconHistoryIndex;
    private bool consolePaused;
    private volatile bool adminCommandsRuntimeLoaded;
    private readonly object modLoadSync = new();
    private readonly Dictionary<string, string> modLoadStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string[]> modLoadAliases = new(StringComparer.OrdinalIgnoreCase);
    private int modLoadSessionGeneration;
    private readonly DispatcherTimer uiHeartbeatTimer = new() { Interval = ApplicationConstants.Timing.UiHeartbeatInterval };
    private System.Threading.Timer? idleWatchdogTimer;
    private DateTime lastUiHeartbeatUtc = DateTime.UtcNow;
    private DateTime lastConsoleViewRefreshUtc = DateTime.MinValue;
    private int watchdogTickRunning;
    private CancellationTokenSource? stabilityTestCts;
    private readonly ObservableCollection<StabilitySampleRow> stabilitySamples = [];
    private DateTime stabilityTestStartedUtc;
    private string stabilityIsolationMode = "Current";
    private List<ModRow>? stabilitySavedModStates;
    private bool? stabilitySavedUe4ssEnabled;

    private readonly EnvironmentService environment;
    private readonly Ue4ssRuntimeResolver ue4ssRuntimeResolver;
    private readonly RuntimeStateService runtimeState;
    private readonly ModInventorySnapshotService modInventory;
    private readonly InstallerService installer;
    private ObservableCollection<EnvironmentComponentRow> environmentRows = [];
    private ObservableCollection<LocalModRow> localModRows = [];
    private ObservableCollection<ModDashboardRow> modDashboardRows = [];
    private static readonly string ModScanResultsPath = Path.Combine(ApplicationPathService.Current.ActivityRoot, "mod-scan-results.json");

    private readonly DispatcherTimer monitorTimer = new() { Interval = ApplicationConstants.Timing.MonitorInterval };
    private volatile bool restPollingSuspended = true;
    private CancellationTokenSource? restResumeCts;
    private readonly SemaphoreSlim monitorRefreshGate = new(1, 1);
    private readonly SemaphoreSlim operation = new(1, 1);
    private CancellationTokenSource? activeOperationCts;
    private ObservableCollection<SettingRow> configRows=[];
    private ICollectionView? configView;
    private CancellationTokenSource? logTailCts;
    private Task? logTailTask;
    private long logTailGeneration;
    private int activePalLogReaders;
    private bool syncingPasswordFields;
    private bool syncingSimpleSettings;
    private bool closeApproved;
    private bool closeShutdownInProgress;
    private readonly ObservableCollection<QolOption> qolOptions = [];
    private readonly ObservableCollection<string> qolSummary = [];
    private string? currentServerLogPath;
    private DateTime lastLogTailWarningUtc = DateTime.MinValue;
    private bool logTailWaitingMessageShown;
    private readonly DateTime managerStartedUtc = DateTime.UtcNow;
    private volatile bool restartInProgress;
    private volatile bool cancelPendingRestart;
    private readonly DispatcherTimer automationTimer = new() { Interval = ApplicationConstants.Timing.AutomationInterval };
    private DateTime lastScheduledRestartDate = DateTime.MinValue;
    private readonly Queue<DateTime> crashRecoveryAttempts = new();
    private static readonly string WindowPlacementPath = Path.Combine(ApplicationPathService.Current.ActivityRoot, "window-placement.json");


    public MainWindow()
    {
        BrandingMigrationService.MigrateLegacyApplicationData();
        InitializeComponent();
        Title = ApplicationVersion.WindowTitle;
        ApplicationVersionText.Text = ApplicationVersion.DisplayVersion;
        navigation = new NavigationCoordinator(this, Tabs);
        InitializeNotificationCenter();
        infrastructureNotifications.Published += HandleInfrastructureNotification;
        pageOperations.ProgressChanged += HandlePageOperationProgress;
        InitializeActivityAudit();
        RestoreWindowPlacement();
        settings=store.Load();
        sessionLog = new SessionLogService(settings.LogsRoot);
        ue4ssRuntimeResolver = new Ue4ssRuntimeResolver(settings);
        runtimeState = new RuntimeStateService();
        runtimeState.StateChanged += state => Log($"[RUNTIME STATE] Revision {state.Revision} • Session {(state.SessionActive ? state.SessionId : "Inactive")} • Loaded aliases {state.LoadedCount} • Errors {state.ErrorCount}");
        foreach (var line in ue4ssRuntimeResolver.BuildDiagnosticLines())
            Log(line);
        var applicationPaths = ApplicationPathService.Current;
        Log(applicationPaths.IsPortable
            ? $"[WORKSPACE] Portable mode enabled. Workspace: {applicationPaths.WorkspaceRoot}"
            : $"[WORKSPACE] Installed mode. Application data: {applicationPaths.DataRoot}");
        activeWorldContext = new ActiveWorldContextService(settings);
        worldDiscovery = new WorldDiscoverySnapshotService(settings, activeWorldContext);
        activeWorldContext.Changed += ActiveWorldContext_Changed;
        playerHistory = new PlayerHistoryService(store.Root, settings);
        playerAdministration = new PlayerAdministrationService(store.Root, settings);
        playerHealth = new PlayerHealthService(settings);
        historicalAnalytics = new HistoricalAnalyticsService(store.Root, settings);
        playerHistory.DiscoverWorldPlayerSaves();
        RestoreRconPreset();
        server=new(settings);
        server.OutputReceived += HandleServerOutput;
        server.ServerExited += HandleServerExit;
        backups=new(settings);
        config=new(settings);
        mods=new(settings, ue4ssRuntimeResolver, runtimeState);
        modVerification=new(settings, runtimeState, modHealthEvaluation);
        modLifecycle = new ModLifecycleCoordinator(mods, modRepairRecommendations);
        modCompatibility=new(settings);
        environment=new(settings);
        modInventory = new ModInventorySnapshotService(mods, environment);
        installer=new(settings);
        crashAnalyzer = new CrashAnalyzerService(settings);
        serverDoctor = new ServerDoctorService(settings);
        guilds = new GuildService(settings, activeWorldContext, worldDiscovery);
        guildTransactions = new GuildTransactionService(settings);
        SyncApiPasswordFromServerConfiguration(logChanges: true);
        InitializeSetupPasswordDefaults();
        InitializeQolOptions();
        LoadSettings(); RefreshBackups(); RefreshMods(); RefreshEnvironment(); ReloadConfig(); RefreshCrashAnalyzer(); RefreshUpdateCenter(); RefreshModRuntime();
        InitializeTransactionCenter();
        InitializeDiagnosticsCenter();
        ScheduledRestartCheck.IsChecked = settings.ScheduledRestartEnabled;
        ScheduledRestartTimeBox.Text = settings.ScheduledRestartTime;
        AutoCrashRecoveryCheck.IsChecked = settings.AutoCrashRecovery;
        UpdateAdminCommandsConsoleState();
        InitializeWindowLifecycle();
    }



    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (closeApproved)
        {
            SaveWindowPlacement();
            return;
        }

        if (closeShutdownInProgress)
        {
            e.Cancel = true;
            return;
        }

        if (!server.IsRunning())
        {
            closeApproved = true;
            SaveWindowPlacement();
            return;
        }

        var result = AppDialog.Show(
            "The Palworld server is currently running.\n\n" +
            "Closing the Server Manager will FORCE the server to shut down immediately. " +
            "Connected players may lose unsaved progress.\n\n" +
            "Are you sure you want to close the manager and force-stop the server?",
            "Server Is Running",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
        {
            e.Cancel = true;
            return;
        }

        e.Cancel = true;
        closeShutdownInProgress = true;
        IsEnabled = false;

        try
        {
            Log("Manager close confirmed. Force-stopping the Palworld server...");
            await StopSessionLogTailAsync(ApplicationConstants.Timing.ShutdownLogTailTimeout);
            await server.ForceStopAsync();
            Log("Palworld server was force-stopped. Closing the manager.");

            closeApproved = true;
            SaveWindowPlacement();
            Close();
        }
        catch (Exception ex)
        {
            closeShutdownInProgress = false;
            IsEnabled = true;
            Log($"Unable to force-stop the server while closing: {ex.Message}");

            AppDialog.Show(
                "The manager could not confirm that the Palworld server stopped. " +
                "The manager will remain open.\n\n" + ex.Message,
                "Server Shutdown Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RestoreWindowPlacement()
    {
        try
        {
            var workArea = SystemParameters.WorkArea;
            var placement = File.Exists(WindowPlacementPath)
                ? JsonSerializer.Deserialize<WindowPlacement>(File.ReadAllText(WindowPlacementPath))
                : null;

            if (placement is not null && placement.Width >= MinWidth && placement.Height >= MinHeight)
            {
                Width = Math.Min(placement.Width, workArea.Width);
                Height = Math.Min(placement.Height, workArea.Height);

                var left = placement.Left;
                var top = placement.Top;
                if (left + 100 < workArea.Left || left > workArea.Right - 100 ||
                    top + 50 < workArea.Top || top > workArea.Bottom - 50)
                {
                    Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
                    Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
                }
                else
                {
                    Left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
                    Top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));
                }

                WindowStartupLocation = WindowStartupLocation.Manual;
                if (placement.IsMaximized)
                    WindowState = WindowState.Maximized;
                return;
            }

            Width = Math.Min(1680, Math.Max(MinWidth, workArea.Width * 0.94));
            Height = Math.Min(1000, Math.Max(MinHeight, workArea.Height * 0.94));
            Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
            Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
        catch
        {
            // The XAML defaults remain in effect if placement data is unavailable.
        }
    }

    private void SaveWindowPlacement()
    {
        try
        {
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
            var placement = new WindowPlacement
            {
                Left = bounds.Left,
                Top = bounds.Top,
                Width = bounds.Width,
                Height = bounds.Height,
                IsMaximized = WindowState == WindowState.Maximized
            };

            var directory = Path.GetDirectoryName(WindowPlacementPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(WindowPlacementPath, JsonSerializer.Serialize(placement, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
        }
        catch
        {
            // Window placement should never prevent the manager from closing.
        }
    }

    private sealed class WindowPlacement
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMaximized { get; set; }
    }

    /// <summary>
    /// Compatibility entry point used by dashboard cards and cross-page links.
    /// Navigation behavior itself is owned by NavigationCoordinator.
    /// </summary>
    private void NavigateToPage(int index) => navigation.TryNavigate(index);

    private async void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton button || !navigation.TryGetIndex(button.Tag, out var index))
            return;

        if (!navigation.TryNavigate(index))
            return;

        switch (index)
        {
            case MainPageIndex.Dashboard:
                RefreshDashboardIntelligence();
                _ = Dispatcher.BeginInvoke(new Action(() => DashboardLog?.ScrollToEnd()), DispatcherPriority.Background);
                break;
            case MainPageIndex.Console:
                lastConsoleViewRefreshUtc = DateTime.UtcNow;
                RefreshConsoleView();
                if (!consolePaused && ConsoleAutoScrollCheckBox?.IsChecked != false)
                    ServerLogBox?.ScrollToEnd();
                break;
            case MainPageIndex.Players:
                PlayerHistorySummaryText.Text = "Scanning players...";
                await RefreshPlayersAsync(silent: false);
                break;
            case MainPageIndex.Guilds:
                activeWorldContext.Invalidate();
                RefreshGuilds();
                break;
            case MainPageIndex.ModRuntime:
                RefreshModRuntime();
                break;
            case MainPageIndex.Recovery:
                RefreshPlayerRecovery();
                RefreshGuildBaseRecovery();
                break;
            case MainPageIndex.BaseManager:
                activeWorldContext.Invalidate();
                RefreshBaseManager();
                break;
            case MainPageIndex.Notifications:
                notificationView?.Refresh();
                RefreshNotificationCenterSummary();
                break;
            case MainPageIndex.WorldValidator:
                RefreshWorldValidator(forceRefresh: true);
                break;
            case MainPageIndex.Workspace:
                RefreshWorkspaceManager();
                break;
            case MainPageIndex.Diagnostics:
                DiagnosticsStatusText.Text = lastDiagnosticsSnapshot is null
                    ? "Diagnostics are ready. Run the full audit when you want a current health snapshot."
                    : $"Last diagnostics score: {lastDiagnosticsSnapshot.Score}% ({lastDiagnosticsSnapshot.OverallStatus}).";
                break;
        }
    }
    private void RefreshGuilds()
    {
        currentGuildSnapshot = guilds.LoadSnapshot();
        ApplySharedBaseDisplayProjection(currentGuildSnapshot);
        guildRows = new ObservableCollection<GuildRow>(currentGuildSnapshot.Guilds);
        guildView = CollectionViewSource.GetDefaultView(guildRows);
        guildView.Filter = GuildFilter;
        GuildsGrid.ItemsSource = guildView;
        GuildMembersGrid.ItemsSource = guildMemberRows;
        GuildBasesGrid.ItemsSource = guildBaseRows;
        guildWorldPlayerRows = BuildGuildWorldPlayerRows(currentGuildSnapshot);
        GuildWorldPlayersGrid.ItemsSource = guildWorldPlayerRows;
        GuildSourceText.Text = string.IsNullOrWhiteSpace(currentGuildSnapshot.SourcePath) ? "No world found" : currentGuildSnapshot.SourcePath;
        GuildWarningText.Text = string.Join("  ", currentGuildSnapshot.Warnings);
        guildView.Refresh();
        UpdateGuildDashboard();
    }


    private void ApplySharedBaseDisplayProjection(GuildWorldSnapshot snapshot)
    {
        try
        {
            var summary = new BaseManagerService(settings, activeWorldContext, worldDiscovery).Scan(snapshot.WorldPath);
            var projected = summary.Bases
                .Where(b => !string.IsNullOrWhiteSpace(b.BaseId))
                .GroupBy(b => NormalizeIdentifier(b.BaseId), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var guild in snapshot.Guilds)
            {
                foreach (var guildBase in guild.Bases)
                {
                    var key = NormalizeIdentifier(guildBase.BaseId);
                    if (!projected.TryGetValue(key, out var baseRow))
                    {
                        guildBase.GuildName = guild.Name;
                        guildBase.Health = "Unresolved";
                        continue;
                    }

                    guildBase.Name = baseRow.Name;
                    guildBase.InternalName = baseRow.InternalName;
                    guildBase.Location = baseRow.Location;
                    guildBase.OwnerGuildId = baseRow.GuildId;
                    guildBase.GuildName = baseRow.GuildName;
                    guildBase.PalboxDisplay = baseRow.PalboxDisplay;
                    guildBase.Health = baseRow.Health;
                }
            }
        }
        catch (Exception ex)
        {
            snapshot.Warnings.Add($"Base display projection unavailable: {ex.Message}");
        }
    }

    private static string NormalizeIdentifier(string value) =>
        new((value ?? string.Empty).Where(Uri.IsHexDigit).ToArray());

    private bool GuildFilter(object item)
    {
        if (item is not GuildRow guild) return false;
        var status = (GuildStatusFilterCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All guilds";
        if (status.Equals("Healthy", StringComparison.OrdinalIgnoreCase) && guild.IsOrphaned) return false;
        if (status.Equals("Orphaned", StringComparison.OrdinalIgnoreCase) && !guild.IsOrphaned) return false;

        var query = GuildSearchBox?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query)) return true;
        return guild.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || guild.GuildId.Contains(query, StringComparison.OrdinalIgnoreCase)
            || guild.LeaderName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || guild.LeaderUid.Contains(query, StringComparison.OrdinalIgnoreCase)
            || guild.Members.Any(m => m.PlayerName.Contains(query, StringComparison.OrdinalIgnoreCase) || m.PlayerUid.Contains(query, StringComparison.OrdinalIgnoreCase))
            || guild.Bases.Any(b => b.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || b.BaseId.Contains(query, StringComparison.OrdinalIgnoreCase) || b.Location.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateGuildDashboard()
    {
        if (currentGuildSnapshot is null) return;
        var diagnostics = new GuildDiagnosticsService().Analyze(currentGuildSnapshot);
        var total = guildRows.Count;
        var orphaned = guildRows.Count(g => g.IsOrphaned);
        var healthy = total - orphaned;
        var members = guildRows.Sum(g => g.MemberCount);
        var bases = guildRows.Sum(g => g.BaseCount);
        var visible = guildView?.Cast<object>().Count() ?? total;

        GuildTotalCardText.Text = total.ToString(CultureInfo.InvariantCulture);
        GuildHealthyCardText.Text = healthy.ToString(CultureInfo.InvariantCulture);
        GuildOrphanedCardText.Text = orphaned.ToString(CultureInfo.InvariantCulture);
        GuildMembersCardText.Text = members.ToString(CultureInfo.InvariantCulture);
        GuildBasesCardText.Text = bases.ToString(CultureInfo.InvariantCulture);
        GuildFindingsCardText.Text = diagnostics.Count.ToString(CultureInfo.InvariantCulture);
        GuildSummaryText.Text = $"Showing {visible} of {total} guilds • {guildWorldPlayerRows.Count} world players • {members} memberships • {bases} bases";
        GuildDiagnosticsText.Text = diagnostics.Count == 0
            ? "No guild ownership problems were detected. The decoded leader, membership, base and player-save relationships are internally consistent."
            : string.Join(Environment.NewLine, diagnostics.Take(8).Select((finding, index) => $"{index + 1}. {finding}"))
              + (diagnostics.Count > 8 ? $"{Environment.NewLine}…and {diagnostics.Count - 8} more finding(s)." : "");
    }

    private void GuildSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        guildView?.Refresh();
        UpdateGuildDashboard();
    }

    private void GuildStatusFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        guildView?.Refresh();
        UpdateGuildDashboard();
    }

    private void ClearGuildFilters_Click(object sender, RoutedEventArgs e)
    {
        GuildSearchBox.Text = "";
        GuildStatusFilterCombo.SelectedIndex = 0;
        guildView?.Refresh();
        UpdateGuildDashboard();
    }

    private ObservableCollection<GuildWorldPlayerRow> BuildGuildWorldPlayerRows(GuildWorldSnapshot snapshot)
    {
        var players = new Dictionary<string, GuildWorldPlayerRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var guild in snapshot.Guilds)
        {
            foreach (var member in guild.Members)
            {
                if (string.IsNullOrWhiteSpace(member.PlayerUid)) continue;
                players[member.PlayerUid] = new GuildWorldPlayerRow
                {
                    PlayerUid = member.PlayerUid,
                    PlayerName = string.IsNullOrWhiteSpace(member.PlayerName) ? member.PlayerUid : member.PlayerName,
                    GuildName = guild.Name,
                    Role = member.Role,
                    Source = "Guild data"
                };
            }
        }

        var worldDirectory = File.Exists(snapshot.SourcePath) ? Path.GetDirectoryName(snapshot.SourcePath) : snapshot.SourcePath;
        var playerDirectory = string.IsNullOrWhiteSpace(worldDirectory) ? "" : Path.Combine(worldDirectory!, "Players");
        if (Directory.Exists(playerDirectory))
        {
            foreach (var candidate in new PlayerSaveDiscoveryService().DiscoverFromPlayersDirectory(playerDirectory).Accepted)
            {
                var savePath = candidate.Path;
                var uid = candidate.PlayerId;
                if (string.IsNullOrWhiteSpace(uid) || players.ContainsKey(uid)) continue;
                players[uid] = new GuildWorldPlayerRow
                {
                    PlayerUid = uid,
                    PlayerName = "Unknown Player",
                    GuildName = "Unassigned",
                    Role = "Unassigned",
                    Source = "Player save"
                };
            }
        }

        return new ObservableCollection<GuildWorldPlayerRow>(players.Values
            .OrderBy(p => p.GuildName.Equals("Unassigned", StringComparison.OrdinalIgnoreCase))
            .ThenBy(p => p.GuildName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.PlayerName, StringComparer.OrdinalIgnoreCase));
    }

    private void RefreshGuilds_Click(object sender, RoutedEventArgs e) => RefreshGuilds();

    private void GuildSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        guildMemberRows.Clear();
        guildBaseRows.Clear();
        if (GuildsGrid.SelectedItem is not GuildRow guild)
        {
            GuildDetailText.Text = "Select a guild to inspect its members and bases.";
            GuildIdentityText.Text = "Guild identifiers and ownership status will appear here.";
            return;
        }
        foreach (var member in guild.Members.OrderByDescending(m => m.IsLeader).ThenBy(m => m.PlayerName, StringComparer.OrdinalIgnoreCase)) guildMemberRows.Add(member);
        foreach (var guildBase in guild.Bases.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)) guildBaseRows.Add(guildBase);
        GuildDetailText.Text = $"{guild.Name} • {guild.Status}";
        GuildIdentityText.Text = $"Guild ID: {guild.GuildId}{Environment.NewLine}Leader: {guild.LeaderName} ({guild.LeaderUid}){Environment.NewLine}Members: {guild.MemberCount} • Bases: {guild.BaseCount} • Leader save: {(guild.LeaderSaveExists ? "Found" : "Missing or unresolved")}";
    }

    private void CopyGuildId_Click(object sender, RoutedEventArgs e)
    {
        var guild = SelectedGuild;
        if (guild is null || string.IsNullOrWhiteSpace(guild.GuildId))
        {
            AppDialog.Show("Select a guild with a valid guild ID first.", "Guild Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Clipboard.SetText(guild.GuildId);
        GuildRepairStatusText.Text = "Guild ID copied to the clipboard.";
        GuildRepairStatusText.Foreground = Brushes.LightGreen;
    }

    private void OpenGuildPlayer_Click(object sender, RoutedEventArgs e)
    {
        var member = SelectedGuildMember;
        if (member is null)
        {
            AppDialog.Show("Select a guild member first.", "Guild Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        NavigateToPage(4);
        PlayerSearchBox.Text = string.IsNullOrWhiteSpace(member.PlayerUid) ? member.PlayerName : member.PlayerUid;
    }


    private void CopyWorldDiscoveryDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = worldDiscovery.BuildDiagnosticsReport(forceRefresh: true);
            Clipboard.SetText(report);
            GuildWarningText.Text = "World discovery diagnostics copied to the clipboard.";
            Log("World discovery diagnostics copied to clipboard.");
        }
        catch (Exception ex)
        {
            AppDialog.Show(ex.Message, "World Discovery Diagnostics", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    private void ExportGuildSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (currentGuildSnapshot is null) RefreshGuilds();
        if (currentGuildSnapshot is null) return;
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "JSON files (*.json)|*.json", FileName = $"GuildSnapshot_{DateTime.Now:yyyyMMdd_HHmmss}.json" };
        if (dialog.ShowDialog() != true) return;
        guilds.ExportSnapshot(currentGuildSnapshot, dialog.FileName);
        AppDialog.Show("Guild snapshot exported successfully.", "Guilds", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ValidateGuildRepair_Click(object sender, RoutedEventArgs e)
    {
        if (currentGuildSnapshot is null) RefreshGuilds();
        try { guildTransactions.ValidatePlan(currentGuildSnapshot!, guildRepairPlan); GuildRepairStatusText.Text = $"Plan valid • {guildRepairPlan.Operations.Count} operation(s)"; GuildRepairStatusText.Foreground = Brushes.LightGreen; }
        catch(Exception ex) { GuildRepairStatusText.Text = "Validation failed: " + ex.Message; GuildRepairStatusText.Foreground = Brushes.IndianRed; }
    }

    private void BackupGuildWorld_Click(object sender, RoutedEventArgs e)
    {
        if (currentGuildSnapshot is null) RefreshGuilds();
        try { var path=guildTransactions.CreateBackup(Path.GetDirectoryName(currentGuildSnapshot!.SourcePath) ?? currentGuildSnapshot.SourcePath); GuildRepairStatusText.Text="Backup created: "+path; GuildRepairStatusText.Foreground=Brushes.LightGreen; }
        catch(Exception ex) { GuildRepairStatusText.Text="Backup failed: "+ex.Message; GuildRepairStatusText.Foreground=Brushes.IndianRed; }
    }

    private GuildRow? SelectedGuild => GuildsGrid.SelectedItem as GuildRow;
    private GuildMemberRow? SelectedGuildMember => GuildMembersGrid.SelectedItem as GuildMemberRow;

    private void AddPlayerToGuild_Click(object sender, RoutedEventArgs e)
    {
        var guild=SelectedGuild; if(guild is null){ AppDialog.Show("Select a guild first."); return; }
        var uid=Microsoft.VisualBasic.Interaction.InputBox("Enter the player UID to add:","Add Player to Guild",""); if(string.IsNullOrWhiteSpace(uid)) return;
        var name=Microsoft.VisualBasic.Interaction.InputBox("Enter the player display name:","Add Player to Guild",uid);
        if(!guild.Members.Any(m=>m.PlayerUid.Equals(uid,StringComparison.OrdinalIgnoreCase))) guild.Members.Add(new GuildMemberRow{PlayerUid=uid,PlayerName=string.IsNullOrWhiteSpace(name)?uid:name});
        guildRepairPlan.Operations.Add(new GuildRepairOperation{Type=GuildRepairOperationType.AddPlayerToGuild,GuildId=guild.GuildId,PlayerUid=uid,Description=$"Add {uid} to {guild.Name}"}); GuildSelection_Changed(this,new SelectionChangedEventArgs(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, Array.Empty<object>(), Array.Empty<object>())); RefreshGuildsGridView();
    }

    private void TransferGuildLeadership_Click(object sender, RoutedEventArgs e)
    {
        var guild=SelectedGuild; var member=SelectedGuildMember; if(guild is null || member is null){AppDialog.Show("Select a guild and member first.");return;}
        foreach(var m in guild.Members)m.IsLeader=false; member.IsLeader=true; guild.LeaderUid=member.PlayerUid; guild.LeaderName=member.PlayerName;
        guildRepairPlan.Operations.Add(new GuildRepairOperation{Type=GuildRepairOperationType.TransferLeadership,GuildId=guild.GuildId,PlayerUid=member.PlayerUid,Description=$"Transfer leadership to {member.PlayerName}"}); RefreshGuildsGridView(); GuildSelection_Changed(this,new SelectionChangedEventArgs(System.Windows.Controls.Primitives.Selector.SelectionChangedEvent, Array.Empty<object>(), Array.Empty<object>()));
    }

    private void ClaimOrphanedGuild_Click(object sender, RoutedEventArgs e)
    {
        var guild=SelectedGuild; if(guild is null){AppDialog.Show("Select a guild first.");return;} if(!guild.IsOrphaned){AppDialog.Show("The selected guild is not orphaned.");return;}
        AddPlayerToGuild_Click(sender,e); var member=guild.Members.LastOrDefault(); if(member is null)return; foreach(var m in guild.Members)m.IsLeader=false; member.IsLeader=true; guild.LeaderUid=member.PlayerUid; guild.LeaderName=member.PlayerName; guildRepairPlan.Operations.Add(new GuildRepairOperation{Type=GuildRepairOperationType.ClaimOrphanedGuild,GuildId=guild.GuildId,PlayerUid=member.PlayerUid,Description=$"Claim orphaned guild {guild.Name}"}); RefreshGuildsGridView();
    }

    private void RepairGuildMappings_Click(object sender, RoutedEventArgs e)
    {
        var guild=SelectedGuild; if(guild is null){AppDialog.Show("Select a guild first.");return;}
        guild.Members= guild.Members.GroupBy(m=>m.PlayerUid,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
        if(!string.IsNullOrWhiteSpace(guild.LeaderUid) && !guild.Members.Any(m=>m.PlayerUid.Equals(guild.LeaderUid,StringComparison.OrdinalIgnoreCase))) guild.Members.Add(new GuildMemberRow{PlayerUid=guild.LeaderUid,PlayerName=guild.LeaderName,IsLeader=true});
        guildRepairPlan.Operations.Add(new GuildRepairOperation{Type=GuildRepairOperationType.RepairOwnershipMappings,GuildId=guild.GuildId,Description=$"Normalize ownership mappings for {guild.Name}"}); RefreshGuildsGridView();
    }

    private void ApplyGuildRepair_Click(object sender, RoutedEventArgs e)
    {
        if(currentGuildSnapshot is null || guildRepairPlan.Operations.Count==0){AppDialog.Show("No guild repair operations are staged.");return;}
        try { var result=new GuildRepairExecutor(settings).Execute(currentGuildSnapshot,guildRepairPlan); GuildRepairStatusText.Text=$"{result.Message} Backup: {result.BackupPath}"; GuildRepairStatusText.Foreground=Brushes.LightGreen; guildRepairPlan=new GuildRepairPlan{WorldPath=currentGuildSnapshot.WorldPath}; RefreshGuilds(); }
        catch(Exception ex){GuildRepairStatusText.Text="Repair failed: "+ex.Message; GuildRepairStatusText.Foreground=Brushes.IndianRed;}
    }

    private void RefreshGuildsGridView(){ GuildsGrid.Items.Refresh(); guildView?.Refresh(); UpdateGuildDashboard(); GuildRepairStatusText.Text=$"{guildRepairPlan.Operations.Count} change(s) staged. Validate and apply when ready."; GuildRepairStatusText.Foreground=Brushes.Gold; }

    private void RepairImportedWorldWizard_Click(object sender, RoutedEventArgs e)
    {
        RefreshGuilds();
        if(currentGuildSnapshot is null || currentGuildSnapshot.Guilds.Count==0){AppDialog.Show("No decoded guild data was found. Export the imported Level.sav to Level.sav.json or provide Guilds.json, then refresh.","Repair Imported World",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        var orphaned=currentGuildSnapshot.Guilds.Where(g=>g.IsOrphaned).ToList();
        var summary=$"World: {currentGuildSnapshot.SourcePath}\nGuilds: {currentGuildSnapshot.Guilds.Count}\nOrphaned guilds: {orphaned.Count}\n\nThe wizard will validate IDs, remove duplicate memberships, repair leader membership links, create a full backup, and write the repaired decoded guild data. Continue?";
        if(AppDialog.Show(summary,"Repair Imported World — Review",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        foreach(var guild in currentGuildSnapshot.Guilds)
        {
            guild.Members=guild.Members.Where(m=>!string.IsNullOrWhiteSpace(m.PlayerUid)).GroupBy(m=>m.PlayerUid,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToList();
            if(!string.IsNullOrWhiteSpace(guild.LeaderUid) && !guild.Members.Any(m=>m.PlayerUid.Equals(guild.LeaderUid,StringComparison.OrdinalIgnoreCase))) guild.Members.Add(new GuildMemberRow{PlayerUid=guild.LeaderUid,PlayerName=guild.LeaderName,IsLeader=true});
            foreach(var member in guild.Members) member.IsLeader=member.PlayerUid.Equals(guild.LeaderUid,StringComparison.OrdinalIgnoreCase);
            guildRepairPlan.Operations.Add(new GuildRepairOperation{Type=GuildRepairOperationType.RepairOwnershipMappings,GuildId=guild.GuildId,Description=$"Wizard normalization for {guild.Name}"});
        }
        try
        {
            guildTransactions.ValidatePlan(currentGuildSnapshot,guildRepairPlan); var world=File.Exists(currentGuildSnapshot.SourcePath) ? Path.GetDirectoryName(currentGuildSnapshot.SourcePath)! : currentGuildSnapshot.SourcePath; var backup=guildTransactions.CreateBackup(world); guilds.SaveSnapshot(currentGuildSnapshot);
            var report=Path.Combine(world,$"MystGuildRepairReport_{DateTime.Now:yyyyMMdd_HHmmss}.json"); File.WriteAllText(report,System.Text.Json.JsonSerializer.Serialize(new{completedUtc=DateTime.UtcNow,backup,operations=guildRepairPlan.Operations,warnings=currentGuildSnapshot.Warnings},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            AppDialog.Show($"Imported-world guild repair completed.\n\nBackup: {backup}\nReport: {report}","Repair Imported World",MessageBoxButton.OK,MessageBoxImage.Information); guildRepairPlan=new GuildRepairPlan{WorldPath=world}; RefreshGuilds();
        }
        catch(Exception ex){AppDialog.Show("The wizard could not apply the repair. The original world was not intentionally removed.\n\n"+ex.Message,"Repair Imported World",MessageBoxButton.OK,MessageBoxImage.Error);}
    }

    private void HandleServerOutput(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        var normalized = NormalizeServerOutput(line);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        crashAnalyzer.Observe(normalized);
        ObserveExplicitModLoad(normalized);
        ObserveAdminCommandsRuntime(normalized);
        Log("[SERVER] " + normalized);
    }


    private void ObserveAdminCommandsRuntime(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;

        // Match common package/display variants such as AdminCommands,
        // Admin Commands, [AdminCommands], and admin_commands.
        var compact = Regex.Replace(line, "[^a-zA-Z0-9]", string.Empty);
        if (!compact.Contains("admincommands", StringComparison.OrdinalIgnoreCase)) return;

        var success =
            line.Contains("loaded successfully", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("successfully loaded", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("initialized successfully", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("registered successfully", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("started successfully", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("entry point executed", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("hook registered", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("hooks registered", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("heartbeat", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("functionally verified", StringComparison.OrdinalIgnoreCase);
        var failure =
            line.Contains("failed to load", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("failed loading", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("load failed", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("unhandled exception", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("fatal", StringComparison.OrdinalIgnoreCase);

        if (!success && !failure) return;
        var loaded = success && !failure;
        if (adminCommandsRuntimeLoaded == loaded) return;

        adminCommandsRuntimeLoaded = loaded;
        Dispatcher.BeginInvoke(new Action(UpdateAdminCommandsConsoleState));
    }

    private static string NormalizeServerOutput(string line)
    {
        var value = line.Replace("\0", string.Empty).TrimEnd();
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        // Palworld occasionally emits a short mojibake prefix before a valid REST/log
        // message. Preserve the meaningful suffix rather than filling the console with
        // unreadable characters.
        foreach (var marker in new[] { "REST API started on port", "Running Palworld dedicated server", "[LOG]" })
        {
            var index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                var prefix = value[..index];
                var suspicious = prefix.Count(ch => ch == '\uFFFD' || ch > 0x7E) >= Math.Max(3, prefix.Length / 3);
                if (suspicious)
                    value = value[index..];
            }
        }

        return value;
    }

    private void HandleServerExit(int exitCode)
    {
        SuspendRestPolling("server stopped");
        adminCommandsRuntimeLoaded = false;
        runtimeState.EndSession();
        Interlocked.Increment(ref modLoadSessionGeneration);
        Dispatcher.BeginInvoke(new Action(UpdateAdminCommandsConsoleState));
        StopSessionLogTail();
        var report = crashAnalyzer.RecordExit(exitCode, server.LastExitWasExpected, mods.Scan());
        Log($"[SERVER] Process exited with code {exitCode}. {report.Summary}");
        Log($"[DIAGNOSTIC] Report saved: {report.ReportPath}");
        ObserveTask(ResetRconForServerSessionAsync("server exit"), "server session RCON cleanup");
        Dispatcher.BeginInvoke(new Action(RefreshCrashAnalyzer));
        if (!server.LastExitWasExpected && settings.AutoCrashRecovery)
        {
            var now = DateTime.UtcNow;
            while (crashRecoveryAttempts.Count > 0 && now - crashRecoveryAttempts.Peek() > TimeSpan.FromMinutes(15)) crashRecoveryAttempts.Dequeue();
            if (crashRecoveryAttempts.Count < 3)
            {
                crashRecoveryAttempts.Enqueue(now);
                Log($"[AUTOMATION] Unexpected exit detected. Guarded recovery will attempt restart in {settings.CrashRecoveryDelaySeconds} seconds ({crashRecoveryAttempts.Count}/3 in 15 min).");
                _ = Dispatcher.InvokeAsync(async () => { await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, settings.CrashRecoveryDelaySeconds))); if (!server.IsRunning()) Start_Click(this, new RoutedEventArgs()); });
            }
            else Log("[AUTOMATION] Crash recovery suppressed: 3 restart attempts occurred within 15 minutes.");
        }
    }

    private void RefreshCrashAnalyzer()
    {
        var reports = crashAnalyzer.LoadRecentReports();
        CrashHistoryGrid.ItemsSource = reports;

        if (reports.Count == 0)
        {
            CrashAnalyzerSummaryText.Text = "No structured v1.9.1 diagnostic reports have been captured yet. Start and stop the server normally or reproduce a failure to create a report.";
            CrashHistoryGrid.SelectedItem = null;
            ShowCrashReport(null);
            return;
        }

        var abnormal = reports.Count(report => !report.Result.Equals("Clean Exit", StringComparison.OrdinalIgnoreCase) &&
                                               !report.Result.Equals("Requested Shutdown", StringComparison.OrdinalIgnoreCase));
        var shutdownFailures = reports.Count(report => report.Result.Equals("Shutdown Failure", StringComparison.OrdinalIgnoreCase));
        CrashAnalyzerSummaryText.Text = abnormal == 0
            ? $"Crash Analyzer: {reports.Count} recent server exit(s), all recorded as clean."
            : $"Crash Analyzer: {reports.Count} recent exit(s), {abnormal} abnormal, including {shutdownFailures} shutdown failure(s). Select an entry for evidence.";

        if (CrashHistoryGrid.SelectedItem is not CrashDiagnosticReport)
            CrashHistoryGrid.SelectedItem = reports[0];
    }

    private void RefreshCrashAnalyzer_Click(object sender, RoutedEventArgs e) => RefreshCrashAnalyzer();

    private void CrashHistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ShowCrashReport(CrashHistoryGrid.SelectedItem as CrashDiagnosticReport);

    private void ShowCrashReport(CrashDiagnosticReport? report)
    {
        if (report is null)
        {
            CrashResultText.Text = "No report selected";
            CrashPhaseText.Text = string.Empty;
            CrashDetailText.Text = "Select a captured server exit to view the diagnostic summary.";
            CrashContributorText.Text = "—";
            CrashConfidenceText.Text = string.Empty;
            CrashEnabledModsText.Text = "—";
            CrashTriggerText.Text = string.Empty;
            CrashEvidenceText.Text = string.Empty;
            return;
        }

        CrashResultText.Text = report.Result;
        var healthyExit = report.Result.Equals("Clean Exit", StringComparison.OrdinalIgnoreCase) ||
                          report.Result.Equals("Requested Shutdown", StringComparison.OrdinalIgnoreCase);
        var warningExit = report.Result.Equals("Unexpected Exit", StringComparison.OrdinalIgnoreCase) ||
                          report.Result.Equals("Unexpected Clean Exit", StringComparison.OrdinalIgnoreCase);
        CrashResultText.Foreground = new SolidColorBrush(healthyExit
            ? Color.FromRgb(53, 211, 107)
            : warningExit ? Color.FromRgb(240, 178, 70) : Color.FromRgb(240, 91, 87));
        CrashPhaseText.Text = $"{report.TimestampDisplay}  •  {report.Phase}  •  Exit code {report.ExitCode}";
        CrashDetailText.Text = report.Summary;
        CrashContributorText.Text = report.LikelyContributor;
        CrashConfidenceText.Text = $"Confidence: {report.Confidence}. {report.ConfidenceReason}";
        CrashEnabledModsText.Text = report.EnabledModsDisplay;
        CrashTriggerText.Text = $"Runtime layer: {report.RuntimeLayer}\nActive context: {report.ActiveContext}\nNearby activity: {report.NearbyActivity}\nRanked suspects: {(report.RankedSuspects.Count == 0 ? "None" : string.Join(", ", report.RankedSuspects))}\n\nTrigger evidence:\n{report.TriggerEvidence}";
        CrashEvidenceText.Text = string.Join(Environment.NewLine, report.RecentEvidence);
    }

    private void OpenDiagnosticsFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(crashAnalyzer.DiagnosticsDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = crashAnalyzer.DiagnosticsDirectory,
            UseShellExecute = true
        });
    }

    private async Task RunExclusive(Func<CancellationToken, Task> work)
    {
        if (!await operation.WaitAsync(0))
        {
            Log("Another operation is already running. Wait for it to finish or cancel the current restart.");
            return;
        }

        using var cts = new CancellationTokenSource();
        activeOperationCts = cts;
        try
        {
            await work(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Log("Operation cancelled.");
        }
        catch (Exception ex)
        {
            var diagnosticPath = WriteManagerExceptionDiagnostic(ex, "Exclusive operation");
            Log($"ERROR: {ex.GetType().Name}: {ex.Message}");
            Log($"[DIAGNOSTIC] Manager exception details saved: {diagnosticPath}");
            AppDialog.Show(
                ex.Message + "\n\nDiagnostic details were saved to:\n" + diagnosticPath,
                "Palworld Server Manager", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            activeOperationCts = null;
            operation.Release();
        }
    }

    private string WriteManagerExceptionDiagnostic(Exception ex, string context)
    {
        try
        {
            var directory = Path.Combine(settings.LogsRoot, "Diagnostics");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"ManagerException_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.txt");
            var io = server.GetSessionIoDiagnostics();
            var lines = new[]
            {
                "MystTiq Palworld Server - Manager Exception",
                $"Timestamp: {DateTime.Now:O}",
                $"Context: {context}",
                $"Exception: {ex.GetType().FullName}",
                $"Message: {ex.Message}",
                $"Server running: {server.IsRunning()}",
                $"Active session: {server.HasActiveSession}",
                $"Session ID: {io.SessionId}",
                $"stdout readers: {io.StdOutReaders}",
                $"stderr readers: {io.StdErrReaders}",
                $"Pal.log readers: {io.PalLogReaders}",
                $"REST pollers: {io.RestPollers}",
                $"Player pollers: {io.PlayerPollers}",
                $"Cleanup in progress: {io.CleanupInProgress}",
                string.Empty,
                ex.ToString()
            };
            File.WriteAllLines(path, lines);
            return path;
        }
        catch (Exception diagnosticEx)
        {
            return "Unable to write diagnostic report: " + diagnosticEx.Message;
        }
    }

    private void ObserveTask(Task task, string context)
    {
        _ = task.ContinueWith(completed =>
        {
            if (completed.Exception is not null)
                Log($"[{context.ToUpperInvariant()}] Background task failed: {completed.Exception.GetBaseException().Message}");
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    private void Log(string text)
    {
        var classification = SessionLogService.Classify(text);
        var persistentLine = sessionLog.Write(classification.Severity, classification.Category, text);
        pendingUiLogs.Enqueue(($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}", persistentLine));
        ScheduleLogFlush();
    }

    private void ScheduleLogFlush()
    {
        if (Interlocked.CompareExchange(ref logFlushScheduled, 1, 0) != 0)
            return;

        Dispatcher.BeginInvoke(new Action(FlushPendingUiLogs), DispatcherPriority.Background);
    }

    private void FlushPendingUiLogs()
    {
        try
        {
            var dashboardBatch = new StringBuilder();
            var count = 0;
            while (count < 150 && pendingUiLogs.TryDequeue(out var item))
            {
                var routineRest = item.Persistent.Contains("REST accessed endpoint /v1/api/info OK", StringComparison.OrdinalIgnoreCase) ||
                                  item.Persistent.Contains("REST accessed endpoint /v1/api/players OK", StringComparison.OrdinalIgnoreCase);
                if (!routineRest)
                    dashboardBatch.Append(item.Display);
                consoleLines.Add(item.Persistent);
                count++;
            }

            if (dashboardBatch.Length > 0)
                DashboardLog.AppendText(dashboardBatch.ToString());

            if (DashboardLog.LineCount > 900)
                DashboardLog.Text = string.Join(Environment.NewLine, DashboardLog.Text.Split(Environment.NewLine).TakeLast(600));
            if (dashboardBatch.Length > 0)
                DashboardLog.ScrollToEnd();

            if (consoleLines.Count > 10000)
                consoleLines.RemoveRange(0, Math.Min(1000, consoleLines.Count - 9000));

            // Do not rebuild thousands of console lines for every background log batch.
            // Refresh automatically only while the Console tab is visible, and cap it to
            // roughly four refreshes per second. Search/filter changes still rebuild on demand.
            if (count > 0 && !consolePaused && Tabs.SelectedIndex == 3 &&
                DateTime.UtcNow - lastConsoleViewRefreshUtc >= TimeSpan.FromMilliseconds(250))
            {
                lastConsoleViewRefreshUtc = DateTime.UtcNow;
                RefreshConsoleView();
            }
        }
        finally
        {
            Interlocked.Exchange(ref logFlushScheduled, 0);
            if (!pendingUiLogs.IsEmpty)
                ScheduleLogFlush();
        }
    }
    private void IdleWatchdogTick()
    {
        if (Interlocked.Exchange(ref watchdogTickRunning, 1) != 0)
            return;
        try
        {
            var heartbeatAge = Math.Max(0, (DateTime.UtcNow - lastUiHeartbeatUtc).TotalSeconds);
            using var current = Process.GetCurrentProcess();
            var io = server.GetSessionIoDiagnostics();
            var detail = $"UI heartbeat={heartbeatAge:F1}s; memory={current.WorkingSet64 / 1024d / 1024d:F1} MB; handles={current.HandleCount}; threads={current.Threads.Count}; session=#{io.SessionId}; process={(io.ProcessRunning ? 1 : 0)}; stdout={io.StdOutReaders}; stderr={io.StdErrReaders}; Pal.log={io.PalLogReaders}; REST={io.RestPollers}; players={io.PlayerPollers}; cleanup={(io.CleanupInProgress ? 1 : 0)}";
            sessionLog.Write(heartbeatAge >= 5 ? "WARNING" : "INFO", "WATCHDOG", detail);

            var leak = io.StdOutReaders > 1 || io.StdErrReaders > 1 || io.PalLogReaders > 1 || io.RestPollers > 1 || io.PlayerPollers > 1;
            if (heartbeatAge >= 5 || leak)
                Log($"[WATCHDOG] {(heartbeatAge >= 5 ? "UI stall" : "session resource leak")} detected. {detail}");
        }
        catch (Exception ex)
        {
            try { sessionLog.Write("WARNING", "WATCHDOG", "Watchdog sample failed: " + ex.Message); } catch { }
        }
        finally
        {
            Interlocked.Exchange(ref watchdogTickRunning, 0);
        }
    }

    private void UpdateAdminCommandsConsoleState()
    {
        if (AdminCommandsStatusText is null) return;
        AdminCommandsStatusText.Text = adminCommandsRuntimeLoaded ? "Loaded successfully — use !commands in-game" : "Not loaded — enable Admin Commands and start the server";
        AdminCommandsStatusText.Foreground = adminCommandsRuntimeLoaded ? Brushes.LightGreen : Brushes.Gold;
    }

    private void RefreshModRuntime(IEnumerable<ModRow>? currentMods = null)
    {
        if (ModRuntimeStatusText is null) return;
        try
        {
            var state = environment.GetUe4ssRuntimeState();
            var identity = environment.GetUe4ssRuntimeIdentity();
            ModRuntimeStatusText.Text = !state.Installed ? "NOT INSTALLED" : state.Enabled ? "ENABLED" : "DISABLED";
            ModRuntimeStatusText.Foreground = !state.Installed ? Brushes.IndianRed : state.Enabled ? Brushes.LightGreen : Brushes.Gold;
            ModRuntimeVersionText.Text = $"Version: {identity.Version}";
            ModRuntimeFlavorText.Text = $"Runtime profile: {identity.Profile}" + (identity.MemberVariableLayoutPresent ? "  •  MemberVariableLayout.ini detected" : "  •  MemberVariableLayout.ini not detected");
            ModRuntimePathText.Text = "Path: " + environment.GetUe4ssRuntimeFolder();
            ModRuntimeToggleButton.Content = state.Enabled ? "■  DISABLE" : state.Installed ? "▶  ENABLE" : "NOT INSTALLED";
            ModRuntimeToggleButton.IsEnabled = state.Installed;
            var latest = environment.GetLatestUe4ssRuntimeSnapshot();
            ModRuntimeBackupText.Text = latest is null ? "Last runtime snapshot: None" : $"Last runtime snapshot: {Path.GetFileName(latest)}";

            var runtimeInfo = ue4ssRuntimeResolver.Refresh();
            runtimeState.Observe(runtimeInfo);
            var runtimeMods = (currentMods ?? mods.Scan()).ToList();
            runtimeState.ApplyTo(runtimeMods);
            var runtimeSnapshot = runtimeState.Current;
            var compatibility = modCompatibility.Scan(runtimeMods);
            var enabled = runtimeMods.Count(mod => mod.Enabled);
            var ue4ssMods = runtimeMods.Count(mod =>
                mod.Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) ||
                mod.Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase));
            var compatible = compatibility.Results.Count(result => result.OverallState == ModCompatibilityState.Compatible);
            var attention = compatibility.Results.Count(result => result.OverallState == ModCompatibilityState.Attention);
            var conflicts = compatibility.Results.Count(result => result.OverallState == ModCompatibilityState.Conflict);
            var failed = compatibility.Results.Count(result => result.OverallState == ModCompatibilityState.Failed);

            var runtimeAssessment = !state.Installed && ue4ssMods > 0
                ? "UE4SS-dependent mods are installed, but the runtime is missing."
                : state.Installed && !state.Enabled && ue4ssMods > 0
                    ? "UE4SS-dependent mods are installed, but the runtime is disabled."
                    : state.Installed && state.Enabled
                        ? "The UE4SS runtime is installed and enabled."
                        : "No enabled UE4SS runtime is currently required by the detected inventory.";

            var issues = compatibility.Results
                .Where(result => result.OverallState != ModCompatibilityState.Compatible)
                .Take(5)
                .Select(result => $"• {result.Name}: {result.OverallStatus} — {result.Details}")
                .ToList();

            var lines = new List<string>
            {
                runtimeAssessment,
                $"UE4SS Root: {runtimeInfo.Ue4ssRoot}",
                $"Active Mods Root: {runtimeInfo.ActiveModsRoot}",
                $"Legacy Mods Root: {runtimeInfo.LegacyModsRoot}",
                $"Runtime Mods Root: {runtimeInfo.RuntimeModsRoot ?? "Not reported"}",
                $"Path health: {runtimeInfo.HealthState}" + (runtimeInfo.HasPathMismatch ? " — MANAGER/RUNTIME MISMATCH" : ""),
                $"Root inventory: {runtimeInfo.ActiveModDirectoryCount} active • {runtimeInfo.LegacyModDirectoryCount} legacy • {runtimeSnapshot.LoadedCount} current-session runtime aliases",
                $"Runtime session: {(runtimeSnapshot.SessionActive ? runtimeSnapshot.SessionId : "Inactive")} • revision {runtimeSnapshot.Revision} • last observed {(runtimeSnapshot.LastObservedAt?.ToString("T") ?? "N/A")}",
                $"Detected mods: {runtimeMods.Count} total • {enabled} enabled • {ue4ssMods} UE4SS/Lua",
                $"Compatibility: {compatible} compatible • {attention} attention • {conflicts} conflict • {failed} failed",
                identity.MemberVariableLayoutPresent
                    ? "MemberVariableLayout.ini is present."
                    : "MemberVariableLayout.ini was not detected; verify whether the selected runtime/mod combination requires it."
            };

            if (issues.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("Items requiring review:");
                lines.AddRange(issues);
                if (compatibility.Results.Count(result => result.OverallState != ModCompatibilityState.Compatible) > issues.Count)
                    lines.Add("• Additional items require review in the MOD Dashboard.");
            }
            else if (runtimeMods.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add("No dependency, file-overlap, or local-version issues were detected by the current scan.");
            }

            lines.Add(string.Empty);
            lines.Add("Change UE4SS only while PalServer is stopped, then run Verify All Mods after the next server start.");
            ModRuntimeCompatibilityText.Text = string.Join(Environment.NewLine, lines);
            if (ModRuntimeCompatibilityUpdatedText is not null)
                ModRuntimeCompatibilityUpdatedText.Text = $"Updated {DateTime.Now:t}";
        }
        catch (Exception ex)
        {
            ModRuntimeStatusText.Text = "CHECK FAILED";
            ModRuntimeStatusText.Foreground = Brushes.IndianRed;
            ModRuntimeCompatibilityText.Text = "Runtime inspection failed: " + ex.Message;
            if (ModRuntimeCompatibilityUpdatedText is not null)
                ModRuntimeCompatibilityUpdatedText.Text = "Update failed";
        }
    }

    private void RefreshModRuntime_Click(object sender, RoutedEventArgs e) => RefreshModRuntime();

    private void OpenModRuntimeFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = environment.GetUe4ssRuntimeFolder();
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "Open Runtime Folder", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void VerifyModRuntime_Click(object sender, RoutedEventArgs e)
    {
        var result = environment.VerifyComponent("UE4SS Runtime");
        RefreshModRuntime();
        AppDialog.Show(result.Message, "UE4SS Runtime Verification", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void ToggleModRuntime_Click(object sender, RoutedEventArgs e)
    {
        if (server.IsRunning())
        {
            AppDialog.Show("Stop PalServer before changing the UE4SS runtime state.", "MOD Runtime", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            var state = environment.GetUe4ssRuntimeState();
            if (!state.Installed) throw new InvalidOperationException("UE4SS is not installed.");
            var message = state.Enabled ? environment.DisableUe4ssRuntime() : environment.EnableUe4ssRuntime();
            Log("[UE4SS] " + message);
            RefreshEnvironment();
            RefreshModRuntime();
            AppDialog.Show(message, "MOD Runtime", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log("[UE4SS] Runtime state change failed: " + ex.Message);
            AppDialog.Show(ex.Message, "MOD Runtime", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BackupModRuntime_Click(object sender, RoutedEventArgs e)
    {
        if (server.IsRunning())
        {
            AppDialog.Show("Stop PalServer before taking or changing a UE4SS runtime snapshot.", "MOD Runtime", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            var path = environment.CreateUe4ssRuntimeSnapshot();
            Log("[UE4SS] Runtime snapshot created: " + path);
            RefreshModRuntime();
            AppDialog.Show("Runtime snapshot created:\n\n" + path, "MOD Runtime", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "Runtime Snapshot", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ImportModRuntimeZip_Click(object sender, RoutedEventArgs e)
    {
        if (server.IsRunning())
        {
            AppDialog.Show("Stop PalServer before importing a different UE4SS runtime.", "MOD Runtime", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select UE4SS Runtime ZIP",
            Filter = "ZIP archives (*.zip)|*.zip",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        if (AppDialog.Show(
            "MystTiq will snapshot the current UE4SS runtime, then import compatible runtime files from this ZIP. Managed user-mod folders are preserved. Continue?",
            "Change UE4SS Runtime", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            var message = environment.ImportUe4ssRuntimeZip(dialog.FileName);
            Log("[UE4SS] " + message);
            RefreshEnvironment();
            RefreshModRuntime();
            AppDialog.Show(message + "\n\nRun Verify All Mods before starting the server.", "Runtime Imported", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log("[UE4SS] Runtime import failed: " + ex.Message);
            AppDialog.Show(ex.Message, "Runtime Import Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestoreModRuntimeSnapshot_Click(object sender, RoutedEventArgs e)
    {
        if (server.IsRunning())
        {
            AppDialog.Show("Stop PalServer before restoring a UE4SS runtime snapshot.", "MOD Runtime", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var root = Path.Combine(settings.BackupRoot, "UE4SS-Runtimes");
        Directory.CreateDirectory(root);
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Restore UE4SS Runtime Snapshot",
            Filter = "UE4SS runtime snapshots (*.zip)|*.zip",
            InitialDirectory = root,
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        if (AppDialog.Show("Restore the selected runtime snapshot? User-mod folders will be preserved.", "Restore Runtime", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            var message = environment.RestoreUe4ssRuntimeSnapshot(dialog.FileName);
            Log("[UE4SS] " + message);
            RefreshEnvironment();
            RefreshModRuntime();
            AppDialog.Show(message, "Runtime Restored", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log("[UE4SS] Runtime restore failed: " + ex.Message);
            AppDialog.Show(ex.Message, "Runtime Restore Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private ApiClient Api()=>new(settings);

    private string GetSteamServerBuildDisplay()
    {
        try
        {
            var candidates = new List<string>();

            void AddCandidate(string? path)
            {
                if (!string.IsNullOrWhiteSpace(path) && !candidates.Contains(path, StringComparer.OrdinalIgnoreCase))
                    candidates.Add(path);
            }

            AddCandidate(Path.Combine(settings.ServerRoot, "steamapps", "appmanifest_2394010.acf"));
            AddCandidate(Path.Combine(settings.ServerRoot, "appmanifest_2394010.acf"));

            var serverParent = Directory.GetParent(settings.ServerRoot)?.FullName;
            if (!string.IsNullOrWhiteSpace(serverParent))
                AddCandidate(Path.Combine(serverParent, "steamapps", "appmanifest_2394010.acf"));

            var steamCmdFolder = Path.GetDirectoryName(settings.SteamCmdPath);
            if (!string.IsNullOrWhiteSpace(steamCmdFolder))
                AddCandidate(Path.Combine(steamCmdFolder, "steamapps", "appmanifest_2394010.acf"));

            foreach (var manifestPath in candidates)
            {
                if (!File.Exists(manifestPath))
                    continue;

                var manifestText = File.ReadAllText(manifestPath);
                var match = System.Text.RegularExpressions.Regex.Match(
                    manifestText,
                    "\\\"buildid\\\"\\s+\\\"(?<id>\\d+)\\\"",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (match.Success)
                    return $"Build {match.Groups["id"].Value}";
            }
        }
        catch (Exception ex)
        {
            Log("[STEAM] Could not read server build version: " + ex.Message);
        }

        return "Build: Unknown";
    }

    private async Task MonitorTickAsync()
    {
        // Keep exactly one monitor pass active. REST/player polling is state-driven:
        // stopped/stopping/restarting sessions only receive lightweight process/resource checks.
        if (!await monitorRefreshGate.WaitAsync(0))
            return;

        try
        {
            var processRunning = await Task.Run(server.IsRunning);
            if (processRunning && !server.HasActiveSession)
            {
                var adopted = await Task.Run(server.TryAdoptRunningServer);
                if (adopted)
                {
                    Log("[SESSION] Externally-started PalServer process adopted by the monitor.");
                    if (logTailTask is null)
                        StartSessionLogTail();
                    if (restPollingSuspended)
                        ScheduleRestPollingResume();
                }
            }

            await RefreshStatusAsync(includeRest: !restPollingSuspended);
            if (!restPollingSuspended && processRunning)
                await RefreshPlayersAsync(silent: true);
            await ProcessExpiredTemporaryBansAsync();
        }
        catch (Exception ex)
        {
            Log("[MONITOR] Health refresh failed: " + ex.Message);
        }
        finally
        {
            monitorRefreshGate.Release();
        }
    }

    private void SuspendRestPolling(string reason)
    {
        restPollingSuspended = true;
        restResumeCts?.Cancel();
        restResumeCts?.Dispose();
        restResumeCts = null;
        UpdateServerIoCounters();
        Log($"[MONITOR] REST/player polling suspended: {reason}.");
    }

    private void ScheduleRestPollingResume()
    {
        restResumeCts?.Cancel();
        restResumeCts?.Dispose();
        restResumeCts = new CancellationTokenSource();
        var token = restResumeCts.Token;

        ObserveTask(Task.Run(async () =>
        {
            // Wait for the process and REST listener to be genuinely available before
            // resuming the normal 10-second REST/player monitor.
            for (var attempt = 0; attempt < 90 && !token.IsCancellationRequested; attempt++)
            {
                if (!server.IsRunning())
                {
                    await Task.Delay(1000, token).ConfigureAwait(false);
                    continue;
                }

                if (server.IsPortListening(8212))
                {
                    restPollingSuspended = false;
                    UpdateServerIoCounters();
                    Log("[MONITOR] REST API is available. Normal monitoring resumed.");
                    return;
                }

                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }, token), "REST monitor resume");
    }

    private void UpdateServerIoCounters()
    {
        server.SetExternalIoCounts(
            Volatile.Read(ref activePalLogReaders),
            restPollingSuspended ? 0 : 1,
            restPollingSuspended ? 0 : 1);
    }

    private async Task RefreshStatusAsync(bool includeRest = true)
    {
        var isInstalled = File.Exists(settings.ServerExe);
        var health = isInstalled
            ? await Task.Run(server.GetHealthSnapshot)
            : new ServerHealthSnapshot(ServerLifecycleState.NotInstalled, false, false, 0, "Server is not installed.", DateTime.Now);
        var isRunning = health.State is ServerLifecycleState.Running or ServerLifecycleState.Starting or ServerLifecycleState.Stopping or ServerLifecycleState.Hung;

        var presentation = statusPresentation.Describe(health.State);
        StatusText.Text = presentation.HeaderText;
        StatusText.Foreground = presentation.Brush;
        SidebarStatusText.Text = presentation.SidebarText;
        SidebarStatusText.Foreground = presentation.Brush;
        SidebarStatusDot.Fill = presentation.Brush;
        SidebarManagerUptimeText.Text = $"Manager uptime: {(DateTime.UtcNow - managerStartedUtc).ToString(@"hh\:mm\:ss")}";
        SidebarSteamVersionText.Text = isInstalled ? GetSteamServerBuildDisplay() : "Not Installed";

        DashboardStartButton.Content = isInstalled ? "▶  START SERVER" : "↓  INSTALL SERVER";
        DashboardStartButton.IsEnabled = !isRunning;
        DashboardRestartButton.IsEnabled = isInstalled && isRunning;
        DashboardStopButton.IsEnabled = isInstalled && isRunning;
        UpdateServerButton.IsEnabled = isInstalled;

        DashboardHealthText.Text = presentation.HealthText;
        DashboardHealthText.Foreground = presentation.Brush;
        RefreshModernDashboard(health, isInstalled);
        DashboardHealthRcon.Text = rcon.IsConnected ? "RCON: Connected" : "RCON: Reconnect on command";
        DashboardHealthMods.Text = modDashboardRows.Count == 0
            ? "Mods: None"
            : $"Mods: {modDashboardRows.Count(x => IsDashboardModHealthy(x.Health))} working / {modDashboardRows.Count} installed";
        var latestBackup = backups.List().FirstOrDefault();
        if (latestBackup is null)
        {
            DashboardHealthBackup.Text = "Backup: None";
        }
        else
        {
            var backupAge = DateTime.Now - latestBackup.Created;
            var backupDisplay = backupAge.TotalHours < 1
                ? $"{Math.Max(0, (int)backupAge.TotalMinutes)}m ago"
                : latestBackup.Created.ToString("MMM d HH:mm");
            var backupStatus = latestBackup.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase)
                ? "Verified"
                : latestBackup.Status;
            DashboardHealthBackup.Text = $"Backup: {backupDisplay} • {backupStatus}";
        }
        var operationText = UpdateStatusText.Text ?? string.Empty;
        var operationDisplay = operationText.Replace("Current operation:", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        CurrentOperationText.Text = string.IsNullOrWhiteSpace(operationDisplay) ? "Ready" : operationDisplay;

        if (!isInstalled)
        {
            InfoText.Text = $"Palworld Dedicated Server was not found at {settings.ServerExe}";
            MemoryText.Text = "--";
            MemoryBar.Value = 0;
            CpuText.Text = "--";
            CpuBar.Value = 0;
            ResourceUpdatedText.Text = "Server not installed";
            PlayersText.Text = "Players: --";
            DashboardPlayerCountText.Text = "--";
            SidebarPlayersText.Text = "No server";
            DashboardPlayersList.ItemsSource = new[] { "Install the dedicated server to view players" };
            return;
        }

        var resources = await Task.Run(server.GetResourceUsage);
        MemoryText.Text = resources.MemoryMb >= 1024
            ? $"{resources.MemoryMb / 1024d:N2} GB"
            : $"{resources.MemoryMb:N0} MB";
        MemoryBar.Maximum = Math.Max(4096d, Math.Ceiling(resources.MemoryMb / 4096d) * 4096d);
        MemoryBar.Value = Math.Min(resources.MemoryMb, MemoryBar.Maximum);
        CpuText.Text = $"{resources.CpuPercent:N1}%";
        CpuBar.Value = resources.CpuPercent;
        ResourceUpdatedText.Text = $"Updated: {DateTime.Now:HH:mm:ss}";
        historicalAnalytics?.Record(
            resources,
            playerHistory.Snapshot().Count(x => x.IsOnline),
            playerHistory.Snapshot().Count,
            backups.List().Count,
            SaveInspector.FindActiveWorldPath(),
            DateTime.UtcNow - managerStartedUtc);
        RefreshHistoricalAnalytics();
        if (!includeRest)
        {
            DashboardHealthRest.Text = isRunning ? "REST: Polling suspended" : "REST: Offline";
            if (!isRunning)
            {
                InfoText.Text = "REST API: Offline";
                PlayersText.Text = "Players: —";
                DashboardPlayerCountText.Text = "—";
                SidebarPlayersText.Text = "Offline";
                DashboardPlayersList.ItemsSource = new[] { "Server is stopped" };
            }
            return;
        }

        try
        {
            using var api = Api();
            using var info = await api.GetAsync("info");
            var root = info.RootElement;
            var version = Get(root, "version");
            var name = Get(root, "servername");
            InfoText.Text = $"REST API: Connected  |  {name}  |  {version}";
            DashboardHealthRest.Text = "REST: Connected";

            // Player data is refreshed by RefreshPlayersAsync in the guarded monitor loop.
        }
        catch (Exception ex)
        {
            InfoText.Text = "REST API: " + ex.Message;
            DashboardHealthRest.Text = "REST: Unavailable";
            PlayersText.Text = "Players: —";
            DashboardPlayerCountText.Text = "—";
            SidebarPlayersText.Text = "Unavailable";
            DashboardPlayersList.ItemsSource = new[] { "Player list unavailable" };
        }
    }
    private static string Get(JsonElement e,string name){foreach(var p in e.EnumerateObject())if(p.Name.Equals(name,StringComparison.OrdinalIgnoreCase))return p.Value.ToString();return "—";}

    private void ScanServerProcesses_Click(object sender, RoutedEventArgs e)
    {
        var processes = server.ScanServerProcesses();
        if (processes.Count == 0)
        {
            AppDialog.Show("No Palworld server processes are currently detected.", "Server Process Scan", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var lines = processes.Select(item =>
            $"PID {item.ProcessId}  {item.Name}\n" +
            $"Path: {(string.IsNullOrWhiteSpace(item.ExecutablePath) ? "Unavailable" : item.ExecutablePath)}\n" +
            $"Configured server: {(item.InConfiguredServerRoot ? "Yes" : "No")}");
        AppDialog.Show(string.Join("\n\n", lines), "Server Process Scan", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ForceCleanup_Click(object sender, RoutedEventArgs e)
    {
        var processes = server.ScanServerProcesses();
        if (processes.Count == 0)
        {
            Log("Force Cleanup: no Palworld server processes were detected.");
            await RefreshStatusAsync();
            return;
        }

        var answer = AppDialog.Show(
            $"Force-close Palworld processes belonging to the configured server?\n\nDetected processes: {processes.Count}\n\nUse this only when the server is hung or orphaned.",
            "Force Server Cleanup", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        try
        {
            Log("Force Cleanup: terminating orphaned/hung Palworld process tree...");
            await server.ForceStopAsync();
            Log("Force Cleanup complete. No configured Palworld server processes remain.");
            await rcon.DisconnectAsync();
            RconStatusText.Text = "Disconnected";
            RconStatusText.Foreground = Brushes.Gold;
            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            Log("Force Cleanup failed: " + ex.Message);
            AppDialog.Show(ex.Message, "Force Cleanup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool ReconcileModStateBeforeStart()
    {
        ModLifecycleReport report;
        try
        {
            report = modLifecycle.ReconcileBeforeStart();
        }
        catch (Exception ex)
        {
            Log("[MOD LIFECYCLE] Pre-start reconciliation failed: " + ex.Message);
            AppDialog.Show(
                "MystTiq could not complete the pre-start MOD reconciliation. The normal modded startup has been blocked to avoid launching with an unknown runtime state.\n\n" +
                ex.Message + "\n\nUse Repair States / Verify & Scan All MODs, correct the reported issue, then start again. You can still use Start Without MODs for isolation testing.",
                "MOD Startup Health Gate", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var repair = report.Reconciliation;
        Log($"[MOD LIFECYCLE] Pre-start reconciliation complete: {repair.RepairedMarkers} enabled.txt override(s) neutralized; {repair.EntriesAdded} mods.txt entr{(repair.EntriesAdded == 1 ? "y" : "ies")} added; {report.Mods.Count} MOD(s) scanned.");
        foreach (var warning in report.Warnings.Take(5))
            Log("[MOD LIFECYCLE] Warning: " + warning);
        foreach (var recommendation in report.Recommendations.Where(x => x.Severity == "Blocking").Take(5))
            Log($"[MOD LIFECYCLE] Recommendation for {recommendation.Name}: {recommendation.Action} — {recommendation.Reason}");

        if (report.CanStart)
        {
            Log($"[MOD LIFECYCLE] Startup health gate: {report.GateStatus}. Normal modded startup may continue.");
            return true;
        }

        Log($"[MOD LIFECYCLE] Startup health gate: BLOCKED ({report.BlockingReasons.Count} reason(s)).");
        foreach (var reason in report.BlockingReasons.Take(8))
            Log("[MOD LIFECYCLE] BLOCK: " + reason);

        var details = string.Join("\n", report.BlockingReasons.Take(6).Select(x => "• " + x));
        if (report.BlockingReasons.Count > 6)
            details += $"\n• …and {report.BlockingReasons.Count - 6} more issue(s).";
        AppDialog.Show(
            "MystTiq stopped the normal server start because the reconciled MOD state is not safe to launch.\n\n" +
            details +
            "\n\nRun Repair States and Verify & Scan All MODs, address the recommendations, then start again. Start Without MODs remains available for isolation testing.",
            "MOD Startup Health Gate", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private void BeginModLoadTracking()
    {
        var generation = Interlocked.Increment(ref modLoadSessionGeneration);
        var enabled = mods.Scan().Where(m => m.Enabled && m.Deployed).ToList();
        lock (modLoadSync)
        {
            modLoadStates.Clear();
            modLoadAliases.Clear();
            foreach (var mod in enabled)
            {
                var name = string.IsNullOrWhiteSpace(mod.Name) ? mod.Package : mod.Name;
                if (string.IsNullOrWhiteSpace(name)) continue;
                modLoadStates[name] = "ENABLED - LOAD NOT CONFIRMED";
                modLoadAliases[name] = new[] { mod.Name, mod.Package, Regex.Replace(mod.Name ?? "", "[^a-zA-Z0-9]", ""), Regex.Replace(mod.Package ?? "", "[^a-zA-Z0-9]", "") }
                    .OfType<string>()
                    .Where(v => !string.IsNullOrWhiteSpace(v) && v.Length >= 3)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
        }

        var ue4ss = environment.GetUe4ssRuntimeState();
        Log("[MOD LOAD] ===== STARTUP MOD LOAD TRACKING =====");
        Log($"[MOD LOAD] UE4SS Runtime: {(ue4ss.Enabled ? "ENABLED" : ue4ss.Installed ? "DISABLED" : "NOT INSTALLED")}");
        foreach (var item in enabled)
        {
            var name = string.IsNullOrWhiteSpace(item.Name) ? item.Package : item.Name;
            Log($"[MOD LOAD] {name}: ENABLED - LOAD NOT CONFIRMED");
        }
        if (enabled.Count == 0) Log("[MOD LOAD] No enabled user mods detected.");

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(45));
            if (generation != Volatile.Read(ref modLoadSessionGeneration)) return;
            await Dispatcher.InvokeAsync(() => WriteModLoadSummary(generation));
        });
    }

    private void ObserveExplicitModLoad(string line)
    {
        var callbackObserved = line.Contains("hook callback", StringComparison.OrdinalIgnoreCase) ||
                               line.Contains("heartbeat", StringComparison.OrdinalIgnoreCase) ||
                               line.Contains("functionally verified", StringComparison.OrdinalIgnoreCase);
        var hooksRegistered = line.Contains("hook registered", StringComparison.OrdinalIgnoreCase) ||
                              line.Contains("hooks registered", StringComparison.OrdinalIgnoreCase) ||
                              line.Contains("reflection initialized", StringComparison.OrdinalIgnoreCase);
        var entryExecuted = line.Contains("entry point executed", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("initializing", StringComparison.OrdinalIgnoreCase);
        var success = callbackObserved || hooksRegistered ||
                      line.Contains("loaded successfully", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("successfully loaded", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("initialized successfully", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("registered successfully", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("started successfully", StringComparison.OrdinalIgnoreCase);
        var failure = line.Contains("failed to load", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("failed loading", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("load failed", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("fatal", StringComparison.OrdinalIgnoreCase) ||
                      line.Contains("unhandled exception", StringComparison.OrdinalIgnoreCase);
        if (!success && !failure) return;

        List<(string Name, string State)> changed = [];
        lock (modLoadSync)
        {
            foreach (var pair in modLoadAliases)
            {
                if (!pair.Value.Any(alias => line.Contains(alias, StringComparison.OrdinalIgnoreCase))) continue;
                var state = failure ? "LOAD FAILED" : callbackObserved ? "ACTIVE - CALLBACK/HEARTBEAT OBSERVED" : hooksRegistered ? "ACTIVE - HOOKS/REFLECTION READY" : entryExecuted ? "ENTRY POINT OBSERVED" : "LOADED - CONFIRMED";
                if (!modLoadStates.TryGetValue(pair.Key, out var previous) || previous == state) continue;
                modLoadStates[pair.Key] = state;
                changed.Add((pair.Key, state));
            }
        }
        foreach (var item in changed)
            Log($"[MOD LOAD] {item.Name}: {item.State}");
    }

    private void WriteModLoadSummary(int generation)
    {
        if (generation != Volatile.Read(ref modLoadSessionGeneration)) return;
        List<KeyValuePair<string, string>> snapshot;
        lock (modLoadSync) snapshot = modLoadStates.OrderBy(p => p.Key).ToList();
        var ue4ss = environment.GetUe4ssRuntimeState();
        Log("[MOD LOAD] ===== 45-SECOND RUNTIME SUMMARY =====");
        Log($"[MOD LOAD] UE4SS Runtime: {(ue4ss.Enabled ? "ENABLED/ACTIVE FILES" : ue4ss.Installed ? "INSTALLED/DISABLED" : "NOT INSTALLED")}");
        foreach (var pair in snapshot) Log($"[MOD LOAD] {pair.Key}: {pair.Value}");
        var confirmed = snapshot.Count(p => p.Value.Contains("CONFIRMED", StringComparison.OrdinalIgnoreCase) || p.Value.StartsWith("ACTIVE", StringComparison.OrdinalIgnoreCase) || p.Value.StartsWith("ENTRY POINT", StringComparison.OrdinalIgnoreCase));
        var failed = snapshot.Count(p => p.Value == "LOAD FAILED");
        var unconfirmed = snapshot.Count - confirmed - failed;
        Log($"[MOD LOAD] Totals: {snapshot.Count} enabled; {confirmed} confirmed; {unconfirmed} unconfirmed; {failed} explicit failure(s).");
        Log("[MOD LOAD] Note: ENABLED/UNCONFIRMED means MystTiq saw configuration evidence but no explicit successful-load message; it is not automatically a failure.");

        // Synchronize the MOD Library after the startup evidence window. The scanner
        // now refreshes UE4SS runtime evidence, so this updates LOADED automatically
        // instead of leaving the pre-start snapshot visible until a later manual scan.
        modInventory.Invalidate();
        RefreshMods();
    }

    private void Start_Click(object s, RoutedEventArgs e)
    {
        if (!File.Exists(settings.ServerExe))
        {
            NavigateToPage(1);
            NewServerSettingsExpander.IsExpanded = true;
            InstallStatusText.Text = "The server is not installed. Review the new-server defaults, then install the missing server components.";
            return;
        }
        _ = RunExclusive(async ct =>
        {
            SuspendRestPolling("server startup");
            await ResetRconForServerSessionAsync("new server session");
            await PrepareForNewServerSessionAsync(ct);
            if (!ReconcileModStateBeforeStart()) return;
            BeginModLoadTracking();
            server.Start();
            StartSessionLogTail();
            ScheduleRestPollingResume();
            Log("Server start requested silently. Live process output and Pal.log will appear here.");
        });
    }
    private async Task PrepareForNewServerSessionAsync(CancellationToken ct)
    {
        await StopSessionLogTailAsync(ApplicationConstants.Timing.ShutdownLogTailTimeout);
        await server.CancelActiveSessionIoAsync(TimeSpan.FromSeconds(1));
        ct.ThrowIfCancellationRequested();
        var io = server.GetSessionIoDiagnostics();
        if (io.StdOutReaders > 0 || io.StdErrReaders > 0 || io.PalLogReaders > 0)
        {
            throw new InvalidOperationException(
                $"Previous server session I/O is still active (stdout={io.StdOutReaders}, stderr={io.StdErrReaders}, Pal.log={io.PalLogReaders}). " +
                "Run Force Cleanup before starting another server session.");
        }
        var runtimeSession = runtimeState.BeginSession(ue4ssRuntimeResolver.Refresh());
        Log($"[SESSION] Previous session I/O verified clean. Runtime session {runtimeSession.SessionId} started at revision {runtimeSession.Revision}.");
    }

    private void StartNoMods_Click(object s, RoutedEventArgs e) => _ = RunExclusive(async ct =>
    {
        SuspendRestPolling("server startup");
        await ResetRconForServerSessionAsync("new no-mod server session");
        await PrepareForNewServerSessionAsync(ct);
        server.Start(true);
        StartSessionLogTail();
        ScheduleRestPollingResume();
        Log("Server started silently with -NoMods. Live process output and Pal.log will appear here.");
    });
    private void Save_Click(object s,RoutedEventArgs e)=>_ = RunExclusive(async ct=>{using var api=Api();await api.SaveAsync(ct);Log("World saved.");});
    private void Stop_Click(object s, RoutedEventArgs e)
    {
        // Stop is special while Restart owns the operation lock: it means "cancel the
        // pending restart and leave the server stopped", not "ignore my click".
        if (restartInProgress)
        {
            cancelPendingRestart = true;
            server.MarkStopping();
            UpdateStatusText.Text = "Current operation: Restart cancelled — waiting for server shutdown";
            Log("Stop requested during restart. The pending restart has been cancelled; MystTiq will leave the server stopped after shutdown completes.");
            return;
        }

        _ = RunExclusive(async ct =>
        {
            SuspendRestPolling("server stop");
            if (!server.HasActiveSession && await Task.Run(server.IsRunning, ct))
            {
                Log("[SESSION] Stop requested without a managed session. Attempting to adopt the discovered PalServer process before using the force-stop fallback.");
                var adopted = await Task.Run(server.TryAdoptRunningServer, ct);
                if (!adopted)
                    Log("[SESSION] Adoption was unavailable. Force Stop will target PalServer processes inside the configured server root directly.");
            }
            server.MarkStopping();
            UpdateStatusText.Text = "Current operation: Stopping server — saving world";
            if (!await Task.Run(server.IsRunning, ct))
            {
                Log("Server is already stopped.");
                UpdateStatusText.Text = "Current operation: Server stopped";
                return;
            }

            // v2.0.1.9 diagnostic stop path: deliberately bypass Palworld's REST
            // graceful-shutdown endpoint. Save first, then terminate the exact managed
            // PalServer session/process tree so we can isolate the shutdown hang.
            try
            {
                using var api = Api();
                await api.SaveAsync(ct);
                Log("World save requested successfully. Graceful shutdown is bypassed for this diagnostic build.");
            }
            catch (Exception ex)
            {
                Log($"World save request failed before forced stop: {ex.Message}. Continuing with controlled process termination.");
            }

            UpdateStatusText.Text = "Current operation: Stopping server — releasing session I/O";
            await StopSessionLogTailAsync(ApplicationConstants.Timing.ShutdownLogTailTimeout);
            UpdateStatusText.Text = "Current operation: Stopping server — terminating PalServer";
            await server.ForceStopAsync();

            UpdateStatusText.Text = "Current operation: Verifying post-stop cleanup";
            var cleanup = await server.CleanupSessionAfterShutdownAsync(ct);
            UpdateStatusText.Text = cleanup.Clean
                ? "Current operation: Server stopped — cleanup verified"
                : "Current operation: Server stopped — cleanup needs attention";
            Log(cleanup.Clean
                ? "Server force-stopped. Session Guardian verified that server processes and guarded ports were released."
                : $"Server force-stopped, but Session Guardian found leftovers. Report: {cleanup.ReportPath}");
        });
    }

    private void Restart_Click(object s, RoutedEventArgs e) => _ = RunExclusive(async ct =>
    {
        restartInProgress = true;
        cancelPendingRestart = false;
        try
        {
            SuspendRestPolling("server restart");
            UpdateStatusText.Text = "Current operation: Restarting server";
            if (!server.HasActiveSession && await Task.Run(server.IsRunning, ct))
            {
                Log("[SESSION] Restart requested without a managed session. Attempting to adopt the discovered PalServer process.");
                await Task.Run(server.TryAdoptRunningServer, ct);
            }
            if (await Task.Run(server.IsRunning, ct))
            {
                server.MarkStopping();
                UpdateStatusText.Text = "Current operation: Restarting — saving world";
                try
                {
                    using var api = Api();
                    await api.SaveAsync(ct);
                    Log("World save requested successfully. Restart is using direct process termination in this diagnostic build.");
                }
                catch (Exception ex)
                {
                    Log($"World save request failed before restart stop: {ex.Message}. Continuing with controlled process termination.");
                }

                UpdateStatusText.Text = "Current operation: Restarting — releasing session I/O";
                await StopSessionLogTailAsync(ApplicationConstants.Timing.ShutdownLogTailTimeout);
                UpdateStatusText.Text = "Current operation: Restarting — terminating PalServer";
                await server.ForceStopAsync();
                UpdateStatusText.Text = "Current operation: Restarting — verifying stop cleanup";
                var cleanup = await server.CleanupSessionAfterShutdownAsync(ct);
                if (!cleanup.Clean)
                    Log($"Restart cleanup found leftovers. Report: {cleanup.ReportPath}");
            }

            if (cancelPendingRestart)
            {
                UpdateStatusText.Text = "Current operation: Restart cancelled — server stopped";
                Log("Restart cancelled. Server will remain stopped.");
                return;
            }

            UpdateStatusText.Text = "Current operation: Restarting — creating backup";
            await CreateBackupAsync(false, ct);

            if (cancelPendingRestart)
            {
                UpdateStatusText.Text = "Current operation: Restart cancelled — server stopped";
                Log("Restart cancelled before startup. Server will remain stopped.");
                return;
            }

            UpdateStatusText.Text = "Current operation: Restarting — starting server";
            await ResetRconForServerSessionAsync("restart startup");
            await PrepareForNewServerSessionAsync(ct);
            if (!ReconcileModStateBeforeStart()) return;
            server.Start();
            StartSessionLogTail();
            ScheduleRestPollingResume();
            UpdateStatusText.Text = "Current operation: Restart complete";
            Log("Server restarted.");
        }
        finally
        {
            restartInProgress = false;
            cancelPendingRestart = false;
        }
    });
    private void Backup_Click(object s, RoutedEventArgs e) =>
        _ = RunExclusive(async ct =>
        {
            try
            {
                BackupsStatusText.Foreground = Brushes.Gold;
                BackupsStatusText.Text = server.IsRunning()
                    ? "Backup status: Saving the active world and creating a verified archive..."
                    : "Backup status: Creating a verified archive...";

                var path = await CreateBackupAsync(server.IsRunning(), ct, allowUnsavedFallback: true);

                BackupsStatusText.Foreground = Brushes.LightGreen;
                BackupsStatusText.Text = $"Backup completed successfully: {Path.GetFileName(path)}";
                AppDialog.Show(
                    $"Backup completed successfully.\n\n{path}",
                    "Backup Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                BackupsStatusText.Foreground = Brushes.Gold;
                BackupsStatusText.Text = "Backup cancelled. No backup files were changed.";
                Log("Backup cancelled by the administrator after the REST save warning.");
            }
            catch (BackupSourceLockedException ex) when (server.IsRunning())
            {
                var choice = AppDialog.Show(
                    "Palworld is holding an active save file open, so Windows cannot create a consistent live snapshot.\n\n" +
                    $"Locked file: {ex.RelativePath}\n\n" +
                    "MystTiq can temporarily stop the server, create and verify the backup, then start the server again. " +
                    "Connected players will be disconnected during this maintenance backup.\n\n" +
                    "Continue with the coordinated stop-backup-start workflow?",
                    "Live Backup Requires Maintenance Stop",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);

                if (choice != MessageBoxResult.Yes)
                {
                    BackupsStatusText.Foreground = Brushes.Gold;
                    BackupsStatusText.Text = "Backup cancelled because the active save file is locked.";
                    Log($"Live backup cancelled. Palworld kept '{ex.RelativePath}' locked after {ex.Attempts} attempts.");
                    return;
                }

                try
                {
                    var path = await CreateCoordinatedMaintenanceBackupAsync(ct);
                    BackupsStatusText.Foreground = Brushes.LightGreen;
                    BackupsStatusText.Text = $"Maintenance backup completed successfully: {Path.GetFileName(path)}";
                    AppDialog.Show(
                        $"Backup completed and the Palworld server was started again.\n\n{path}",
                        "Maintenance Backup Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch (Exception maintenanceError)
                {
                    BackupsStatusText.Foreground = Brushes.IndianRed;
                    BackupsStatusText.Text = "Maintenance backup failed: " + maintenanceError.Message;
                    Log("Maintenance backup failed: " + maintenanceError.Message);
                    AppDialog.Show(
                        "MystTiq could not complete the coordinated maintenance backup.\n\n" +
                        maintenanceError.Message +
                        "\n\nCheck the Dashboard before manually starting the server.",
                        "Maintenance Backup Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                BackupsStatusText.Foreground = Brushes.IndianRed;
                BackupsStatusText.Text = "Backup failed: " + ex.Message;
                Log("Backup failed: " + ex.Message);
                AppDialog.Show(
                    "MystTiq could not create the backup.\n\n" + ex.Message,
                    "Backup Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        });

    private async Task<string> CreateCoordinatedMaintenanceBackupAsync(CancellationToken ct)
    {
        SuspendRestPolling("coordinated maintenance backup");
        Log("Live snapshot was blocked by an active Palworld save lock. Beginning coordinated stop-backup-start workflow.");

        if (!server.HasActiveSession && await Task.Run(server.IsRunning, ct))
        {
            Log("[SESSION] Maintenance backup is adopting the discovered PalServer process before shutdown.");
            await Task.Run(server.TryAdoptRunningServer, ct);
        }

        server.MarkStopping();
        UpdateStatusText.Text = "Current operation: Maintenance backup — saving world";
        try
        {
            using var api = Api();
            await api.SaveAsync(ct);
            Log("World save requested before maintenance backup shutdown.");
        }
        catch (Exception saveError)
        {
            Log($"World save request failed before maintenance backup shutdown: {saveError.Message}. Continuing with controlled stop.");
        }

        UpdateStatusText.Text = "Current operation: Maintenance backup — stopping server";
        await StopSessionLogTailAsync(ApplicationConstants.Timing.ShutdownLogTailTimeout);
        await server.ForceStopAsync();

        var cleanup = await server.CleanupSessionAfterShutdownAsync(ct);
        if (!cleanup.Clean)
        {
            UpdateStatusText.Text = "Current operation: Maintenance backup blocked — cleanup needs attention";
            throw new InvalidOperationException(
                "The server stopped, but Session Guardian found remaining processes or guarded ports. " +
                $"Review the cleanup report before restarting: {cleanup.ReportPath}");
        }

        string? backupPath = null;
        Exception? backupFailure = null;
        try
        {
            UpdateStatusText.Text = "Current operation: Maintenance backup — creating verified archive";
            backupPath = await CreateBackupAsync(saveFirst: false, ct);
            return backupPath;
        }
        catch (Exception ex)
        {
            backupFailure = ex;
            throw;
        }
        finally
        {
            try
            {
                UpdateStatusText.Text = "Current operation: Maintenance backup — starting server";
                await ResetRconForServerSessionAsync("maintenance backup restart");
                await PrepareForNewServerSessionAsync(ct);
                if (!ReconcileModStateBeforeStart())
                {
                    UpdateStatusText.Text = "Current operation: Maintenance restart blocked — MOD health gate";
                    Log("Maintenance restart was blocked by the MOD startup health gate. The server remains stopped.");
                }
                else
                {
                    BeginModLoadTracking();
                    server.Start();
                    StartSessionLogTail();
                    ScheduleRestPollingResume();
                    UpdateStatusText.Text = backupFailure is null
                        ? "Current operation: Maintenance backup complete — server started"
                        : "Current operation: Backup failed — server restarted";
                    Log(backupFailure is null
                        ? "Coordinated maintenance backup completed and the server was started again."
                        : "The maintenance backup failed, but MystTiq started the server again successfully.");
                }
            }
            catch (Exception restartError)
            {
                UpdateStatusText.Text = "Current operation: Maintenance backup — server restart failed";
                Log("Server restart failed after maintenance backup: " + restartError.Message);
                if (backupFailure is null)
                {
                    throw new InvalidOperationException(
                        $"The backup was created successfully at '{backupPath}', but the server could not be restarted automatically. " +
                        restartError.Message,
                        restartError);
                }
            }
        }
    }

    private static bool IsRestAuthenticationFailure(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("401", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("AdminPassword is empty", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> CreateBackupAsync(
        bool saveFirst,
        CancellationToken ct,
        bool allowUnsavedFallback = false)
    {
        Log(saveFirst
            ? "Saving the world and waiting for save files to become stable..."
            : "Creating a backup snapshot...");

        if (saveFirst)
            SyncApiPasswordFromServerConfiguration(logChanges: true);

        using var api = saveFirst ? Api() : null;
        try
        {
            var path = await backups.CreateAsync(api, saveFirst, ct);
            Log("Backup created and verified: " + path);
            RefreshBackups();
            return path;
        }
        catch (HttpRequestException ex) when (saveFirst && allowUnsavedFallback && IsRestAuthenticationFailure(ex))
        {
            var worldOverride = FindWorldOptionOverride();
            var iniPassword = config.TryReadAdminPassword();
            var warning = new StringBuilder()
                .AppendLine("Palworld rejected MystTiq's pre-backup save command.")
                .AppendLine()
                .AppendLine(ex.Message)
                .AppendLine()
                .AppendLine($"AdminPassword in PalWorldSettings.ini: {(string.IsNullOrWhiteSpace(iniPassword) ? "EMPTY" : "SET")}")
                .AppendLine($"WorldOption.sav override: {(worldOverride is null ? "not detected" : "DETECTED")}");

            if (worldOverride is not null)
                warning.AppendLine("Override path: " + worldOverride);

            warning
                .AppendLine()
                .AppendLine("MystTiq can still create a verified filesystem backup, but the running server could not be told to save its newest in-memory state first.")
                .AppendLine("Choose Yes to create that backup anyway, or No to cancel.");

            Log("[BACKUP WARNING] REST save authentication failed: " + ex.Message);
            if (worldOverride is not null)
                Log("[BACKUP WARNING] Imported-world override detected: " + worldOverride);

            var answer = AppDialog.Show(
                warning.ToString(),
                "REST Save Authentication Failed",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (answer != MessageBoxResult.Yes)
                throw new OperationCanceledException("Backup cancelled after REST authentication failure.");

            BackupsStatusText.Foreground = Brushes.Gold;
            BackupsStatusText.Text = "Backup status: REST save unavailable; creating a verified filesystem snapshot...";
            Log("Creating filesystem backup without a successful REST save at administrator request.");

            var path = await backups.CreateAsync(api: null, saveFirst: false, token: ct);
            Log("Filesystem backup created and verified without REST save: " + path);
            RefreshBackups();
            return path;
        }
    }
    private void Update_Click(object sender, RoutedEventArgs e) =>
        _ = RunExclusive(async ct =>
        {
            UpdateServerButton.IsEnabled = false;

            try
            {
                if (server.IsRunning())
                {
                    SetUpdateStatus(
                        ServerUpdateState.Error,
                        "Update status: Stop the Palworld server before checking for updates.");
                    AppDialog.Show(
                        "Stop the Palworld server before checking for or installing server updates.",
                        "Server Update",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (Directory.Exists(settings.SaveRoot) &&
                    Directory.EnumerateFiles(settings.SaveRoot, "*", SearchOption.AllDirectories).Any())
                {
                    SetUpdateStatus(
                        ServerUpdateState.Checking,
                        "Update status: Creating verified safety backup before SteamCMD...");
                    Log("Creating verified pre-update safety backup...");
                    var safetyBackup = await CreateBackupAsync(saveFirst: false, ct: ct);
                    Log("Pre-update safety backup verified: " + safetyBackup);
                }

                SetUpdateStatus(
                    ServerUpdateState.Checking,
                    "Update status: Checking for updates...");
                Log("Starting SteamCMD update check for Palworld Dedicated Server (App ID 2394010)...");

                var result = await server.UpdateServerAsync(
                    (state, message) =>
                    {
                        Dispatcher.BeginInvoke(new Action(() => SetUpdateStatus(
                            state,
                            "Update status: " + message)), DispatcherPriority.Background);
                    },
                    ct);

                SetUpdateStatus(
                    result.State,
                    "Update status: " + result.Message);

                switch (result.State)
                {
                    case ServerUpdateState.UpToDate:
                        Log("SteamCMD reports that the server is up to date.");
                        break;

                    case ServerUpdateState.Complete:
                        Log("SteamCMD update completed successfully.");
                        break;

                    case ServerUpdateState.Error:
                        Log("SteamCMD update failed: " + result.Message);
                        AppDialog.Show(
                            result.Message,
                            "Server Update Failed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        break;
                }
            }
            finally
            {
                UpdateServerButton.IsEnabled = true;
            }
        });

    private void SetUpdateStatus(ServerUpdateState state, string message)
    {
        UpdateStatusText.Text = message;
        UpdateStatusText.Foreground = new SolidColorBrush(state switch
        {
            ServerUpdateState.UpToDate => Color.FromRgb(107, 225, 120),
            ServerUpdateState.Complete => Color.FromRgb(107, 225, 120),
            ServerUpdateState.Updating => Color.FromRgb(255, 193, 92),
            ServerUpdateState.Error => Color.FromRgb(240, 91, 87),
            _ => Color.FromRgb(159, 196, 234)
        });
    }

    private async void RefreshPlayers_Click(object s, RoutedEventArgs e) =>
        await RefreshPlayersAsync(silent: false);

    private async Task RefreshPlayersAsync(bool silent)
    {
        try
        {
            var ran = await pageOperations.RunAsync(
                "players.refresh",
                "Refresh players",
                async context =>
                {
                    context.Report("Resolving active world", 10);
                    var discovery = worldDiscovery.Current();
                    var world = discovery.Context;
                    var importedCount = playerHistory.DiscoverWorldPlayerSaves(world.WorldPath);
                    context.Report($"Merged {importedCount} imported player save(s); reading REST players", 20);
                    List<LivePlayerSnapshot> livePlayers = [];

                    using var api = Api();
                    using var document = await api.GetAsync("players");
                    context.CancellationToken.ThrowIfCancellationRequested();

                    if (document.RootElement.TryGetProperty("players", out var array) &&
                        array.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var player in array.EnumerateArray())
                        {
                            var userId = GetAny(player, "userId", "userid");
                            var steamId = GetAny(player, "steamId", "steamid");
                            livePlayers.Add(new LivePlayerSnapshot(
                                GetAny(player, "name"),
                                userId,
                                steamId,
                                GetAny(player, "playerId", "playerid"),
                                GetAny(player, "ip"),
                                GetAny(player, "ping"),
                                DetectPlayerPlatform(userId, steamId),
                                GetAny(player, "level"),
                                GetAny(player, "buildingCount", "buildingcount", "building_count")));
                        }
                    }

                    context.Report("Updating player history", 70);
                    playerHistory.MergeOnline(livePlayers);
                    scanCache.Set("players.live", livePlayers, TimeSpan.FromSeconds(30));
                    RefreshPlayerHistoryGrid();

                    var names = livePlayers.Select(row => string.IsNullOrWhiteSpace(row.Name) ? row.UserId : row.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                        .ToList();
                    PlayersText.Text = $"Players: {livePlayers.Count}";
                    DashboardPlayerCountText.Text = livePlayers.Count.ToString();
                    SidebarPlayersText.Text = $"{livePlayers.Count} online";
                    DashboardHealthPlayers.Text = $"Players: {livePlayers.Count}";
                    DashboardPlayersList.ItemsSource = names.Count > 0 ? names : new[] { "No players online" };
                    context.Report("Player refresh complete", 100);
                });

            if (!ran && !silent)
                Log("Player refresh is already running.");
        }
        catch (Exception exception)
        {
            RefreshPlayerHistoryGrid();
            if (!silent)
            {
                Log("ERROR: " + exception.Message);
                infrastructureNotifications.Publish(
                    NotificationLevel.Error,
                    exception.Message,
                    "Players",
                    "Player refresh failed",
                    MainPageIndex.Players);
            }
        }
    }

    private void RefreshPlayerHistoryGrid()
    {
        var history = playerHistory.Snapshot();
        var selectedKey = PlayersGrid.SelectedItem is PlayerRow selected ? PlayerKey(selected) : "";
        var query = PlayerSearchBox?.Text?.Trim() ?? string.Empty;
        var adminFilter = (PlayerAdminFilterCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Players";
        var rows = history
            .Where(r => showAllKnownPlayers || r.IsOnline)
            .Select(ToPlayerRow)
            .Where(r => PlayerMatchesSearch(r, query))
            .Where(r => PlayerMatchesAdministrationFilter(r, adminFilter))
            .OrderByDescending(r => r.Status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(r => ParsePlayerDate(r.LastSeen))
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        PlayersGrid.ItemsSource = rows;
        var onlineCount = history.Count(r => r.IsOnline);
        var bannedCount = history.Count(r => r.IsBanned);
        var saveCount = history.Select(ToPlayerRow).Count(r => r.SaveStatus.Equals("Found", StringComparison.OrdinalIgnoreCase));
        var platformCount = history.Select(r => r.Platform).Where(p => !string.IsNullOrWhiteSpace(p) && !p.Equals("Unknown", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        PlayerKnownCountText.Text = history.Count.ToString();
        PlayerOnlineCountText.Text = onlineCount.ToString();
        PlayerBannedCountText.Text = bannedCount.ToString();
        PlayerSaveCountText.Text = saveCount.ToString();
        PlayerFilteredCountText.Text = rows.Count.ToString();
        PlayerPlatformCountText.Text = platformCount.ToString();
        PlayerHistorySummaryText.Text = $"{history.Count} known • {onlineCount} online • {rows.Count} shown • Database: {Path.GetFileName(playerHistory.FilePath)}";

        if (!string.IsNullOrWhiteSpace(selectedKey))
        {
            var match = rows.FirstOrDefault(r => PlayerKey(r).Equals(selectedKey, StringComparison.OrdinalIgnoreCase));
            if (match is not null) PlayersGrid.SelectedItem = match;
        }
        UpdatePlayerDetails();
    }

    private PlayerRow ToPlayerRow(PlayerHistoryRecord record)
    {
        var savePath = FindPlayerSavePath(record.PlayerId);
        return new PlayerRow(
            record.IsOnline ? "ONLINE" : "Offline",
            string.IsNullOrWhiteSpace(record.Name) ? "Unknown player" : record.Name,
            record.UserId,
            record.SteamId,
            record.PlayerId,
            record.Ip,
            record.IsOnline ? record.Ping : "",
            record.Platform,
            record.Level,
            record.BuildingCount,
            FormatPlayerDate(record.FirstSeenUtc),
            record.IsOnline ? "Now" : FormatPlayerDate(record.LastSeenUtc),
            record.ObservedSessions,
            record.IsBanned ? "YES" : "",
            record.Notes,
            string.IsNullOrWhiteSpace(record.Source) ? "Unknown" : record.Source,
            string.IsNullOrWhiteSpace(savePath) ? "Not found" : "Found",
            savePath);
    }

    private static DateTime ParsePlayerDate(string value) =>
        value.Equals("Now", StringComparison.OrdinalIgnoreCase) ? DateTime.MaxValue :
        DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsed) ? parsed : DateTime.MinValue;

    private static bool PlayerMatchesSearch(PlayerRow row, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        return new[] { row.Name, row.UserId, row.SteamId, row.PlayerId, row.Platform, row.Notes, row.Source, row.Status }
            .Any(value => !string.IsNullOrWhiteSpace(value) && value.Contains(query, StringComparison.CurrentCultureIgnoreCase));
    }


    private bool PlayerMatchesAdministrationFilter(PlayerRow row, string filter)
    {
        var key = PlayerKey(row);
        var summary = playerAdministration.GetSummary(key);
        return filter switch
        {
            "Online" => row.Status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase),
            "Offline" => !row.Status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase),
            "Admins" => summary.IsAdmin,
            "Banned" => summary.IsBanned || row.Banned.Equals("YES", StringComparison.OrdinalIgnoreCase),
            "Whitelisted" => summary.IsWhitelisted,
            "Has Notes" => summary.NoteCount > 0 || !string.IsNullOrWhiteSpace(row.Notes),
            "Has Warnings" => summary.ActiveWarningCount > 0,
            _ => true
        };
    }

    private void PlayerAdminFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) RefreshPlayerHistoryGrid();
    }

    private string FindPlayerSavePath(string playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId) || !Directory.Exists(settings.SaveRoot)) return string.Empty;
        try
        {
            static string NormalizeId(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
            var wanted = NormalizeId(playerId);
            if (string.IsNullOrWhiteSpace(wanted)) return string.Empty;

            var fileSystem = new SafeFileSystemService();
            var discovery = new PlayerSaveDiscoveryService(fileSystem);
            return fileSystem.EnumerateDirectories(settings.SaveRoot, "Players", SearchOption.AllDirectories)
                .SelectMany(directory => discovery.DiscoverFromPlayersDirectory(directory).Accepted)
                .FirstOrDefault(candidate => NormalizeId(candidate.PlayerId) == wanted)?.Path ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ResolvePlayerSavePath(PlayerRow player)
    {
        if (!string.IsNullOrWhiteSpace(player.SavePath) && File.Exists(player.SavePath) &&
            !Path.GetFileNameWithoutExtension(player.SavePath).EndsWith("_dps", StringComparison.OrdinalIgnoreCase))
            return player.SavePath;

        foreach (var identifier in new[] { player.PlayerId, player.UserId, player.SteamId })
        {
            var path = FindPlayerSavePath(identifier);
            if (!string.IsNullOrWhiteSpace(path)) return path;
        }
        return string.Empty;
    }

    private static string FormatPlayerDate(DateTime value) =>
        value <= DateTime.UnixEpoch ? "—" : value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    private static string PlayerKey(PlayerRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.UserId)) return "user:" + row.UserId.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(row.SteamId)) return "steam:" + row.SteamId.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(row.PlayerId)) return "player:" + row.PlayerId.Trim().ToLowerInvariant();
        return "";
    }

    private void PlayerView_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        showAllKnownPlayers = PlayerViewCombo.SelectedIndex == 1;
        RefreshPlayerHistoryGrid();
    }

    private void PlayerSearch_Changed(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) RefreshPlayerHistoryGrid();
    }

    private void PlayerSelection_Changed(object sender, SelectionChangedEventArgs e) => UpdatePlayerDetails();

    private void UpdatePlayerDetails()
    {
        if (PlayersGrid.SelectedItem is not PlayerRow p)
        {
            PlayerDetailText.Text = "Select a player to view IDs and history.";
            PlayerIdentityText.Text = "Select a player to view complete identifiers.";
            PlayerNotesBox.Text = "";
            PlayerNotesBox.IsEnabled = false;
            SavePlayerNoteButton.IsEnabled = false;
            if (PlayerHealthText is not null) PlayerHealthText.Text = "—";
            if (PlayerAdministrationSummaryText is not null) PlayerAdministrationSummaryText.Text = "Select a player to view administration status.";
            if (PlayerAdministrationReadinessText is not null) PlayerAdministrationReadinessText.Text = "STATUS: SELECT A PLAYER";
            RefreshSelectedPlayerTimeline();
            return;
        }

        PlayerDetailText.Text = $"{p.Status}  •  {p.Name}\nPlatform: {p.Platform}   Level: {BlankDash(p.Level)}\nSave: {p.SaveStatus}   Source: {p.Source}\nLast seen: {p.LastSeen}   Sessions: {p.Sessions}";
        PlayerIdentityText.Text = $"User ID: {BlankDash(p.UserId)}\nSteam ID: {BlankDash(p.SteamId)}\nPlayer ID: {BlankDash(p.PlayerId)}\nFirst seen: {p.FirstSeen}   Last IP: {BlankDash(p.Ip)}\nBuildings: {BlankDash(p.BuildingCount)}   Ping: {BlankDash(p.Ping)}   Banned: {BlankDash(p.Banned)}";
        PlayerNotesBox.IsEnabled = true;
        SavePlayerNoteButton.IsEnabled = true;
        PlayerNotesBox.Text = p.Notes;
        if (PlayerHealthText is not null) PlayerHealthText.Text = GetPlayerHealth(p);
        RefreshPlayerAdministrationSummary(p);
        RefreshPlayerToolkit(p);
        RefreshSelectedPlayerTimeline();
    }

    private static string BlankDash(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private void DiscoverPlayerSaves_Click(object sender, RoutedEventArgs e)
    {
        var added = playerHistory.DiscoverWorldPlayerSaves();
        showAllKnownPlayers = true;
        PlayerViewCombo.SelectedIndex = 1;
        RefreshPlayerHistoryGrid();
        AppDialog.Show(added == 0 ? "No new player save files were found." : $"Discovered {added} additional player save file(s). Imported records will be enriched automatically if those players connect again.", "Player History", MessageBoxButton.OK, MessageBoxImage.Information);
    }


    private void CopyPlayerIds_Click(object sender, RoutedEventArgs e)
    {
        if (PlayersGrid.SelectedItem is not PlayerRow p)
        {
            AppDialog.Show("Select a player first.", "Player Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Clipboard.SetText($"Player: {p.Name}{Environment.NewLine}User ID: {p.UserId}{Environment.NewLine}Steam ID: {p.SteamId}{Environment.NewLine}Player ID: {p.PlayerId}");
        Log($"[PLAYERS] Copied identifiers for {p.Name}.");
    }

    private void OpenPlayerSave_Click(object sender, RoutedEventArgs e)
    {
        if (PlayersGrid.SelectedItem is not PlayerRow p || string.IsNullOrWhiteSpace(p.SavePath) || !File.Exists(p.SavePath))
        {
            AppDialog.Show("No matching player save file was found for the selected player. Use Discover Saves to rescan the world.", "Player Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{p.SavePath}\"") { UseShellExecute = true });
    }


    private void DeletePlayerFiles_Click(object sender, RoutedEventArgs e)
    {
        if (PlayersGrid.SelectedItem is not PlayerRow player)
        {
            AppDialog.Show("Select a player first.", "Delete Player Files", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (player.Status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase))
        {
            AppDialog.Show("The selected player is online. Disconnect or kick the player, stop PalServer, and try again.", "Delete Player Files", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (server.IsRunning())
        {
            AppDialog.Show("PalServer must be stopped before player files can be deleted.", "Delete Player Files", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var primarySavePath = ResolvePlayerSavePath(player);
        if (string.IsNullOrWhiteSpace(primarySavePath) || !File.Exists(primarySavePath))
        {
            var removeOnly = AppDialog.Show(
                $"No primary save file can be located for {player.Name}.\n\nRemove this stale player record from MystTiq's player list?\n\nThis does not alter Level.sav, guilds, bases, or Palbox ownership.",
                "Remove Stale Player Record",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);
            if (removeOnly == MessageBoxResult.Yes)
            {
                var removed = playerHistory.RemoveMatching(player);
                RefreshPlayerHistoryGrid();
                Log($"[PLAYERS] Removed {removed} stale player-history record(s) for {player.Name}.");
            }
            return;
        }

        var dpsPath = Path.Combine(Path.GetDirectoryName(primarySavePath) ?? string.Empty, Path.GetFileNameWithoutExtension(primarySavePath) + "_dps.sav");
        var warning = $"Delete the selected player's save files?\n\nPlayer: {player.Name}\nPlayer ID: {BlankDash(player.PlayerId)}\nPrimary save: {primarySavePath}\n_dps companion: {(File.Exists(dpsPath) ? dpsPath : "not present")}\n\nMyst will create a recovery ZIP first. This removes player files and all matching manager history records, but it does NOT purge guild, base, Palbox, or Level.sav references. Use Player Recovery or Guild & Base Recovery if ownership references remain.";
        if (AppDialog.Show(warning, "Delete Player Files", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;

        try
        {
            var backupRoot = Path.Combine(settings.BackupRoot, "DeletedPlayers");
            Directory.CreateDirectory(backupRoot);
            var safeName = string.Concat((string.IsNullOrWhiteSpace(player.Name) ? "Player" : player.Name).Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
            var backupPath = Path.Combine(backupRoot, $"DeletedPlayer_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
            using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(primarySavePath, Path.GetFileName(primarySavePath), CompressionLevel.Optimal);
                if (File.Exists(dpsPath)) archive.CreateEntryFromFile(dpsPath, Path.GetFileName(dpsPath), CompressionLevel.Optimal);
                var manifest = archive.CreateEntry("MystDeletedPlayer.json", CompressionLevel.Optimal);
                using var writer = new StreamWriter(manifest.Open(), new UTF8Encoding(false));
                writer.Write(JsonSerializer.Serialize(new
                {
                    deletedAt = DateTimeOffset.Now,
                    player.Name,
                    player.UserId,
                    player.SteamId,
                    player.PlayerId,
                    primarySave = primarySavePath,
                    dpsSave = File.Exists(dpsPath) ? dpsPath : null,
                    warning = "Player files only. Level.sav, guild, base and ownership references were not changed."
                }, new JsonSerializerOptions { WriteIndented = true }));
            }

            File.Delete(primarySavePath);
            if (File.Exists(dpsPath)) File.Delete(dpsPath);
            var removedRecords = playerHistory.RemoveMatching(player);
            RefreshPlayerHistoryGrid();
            Log($"[PLAYERS] Deleted player files for {player.Name}; removed {removedRecords} matching history record(s). Recovery ZIP: {backupPath}");
            RecordAudit("Warning", "Players", "Player save files deleted", $"{player.Name} • recovery: {backupPath}", 4);
            AppDialog.Show($"Player files deleted successfully.\n\nRecovery ZIP:\n{backupPath}\n\nWorld ownership references were not changed.", "Delete Player Files", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log("[PLAYERS] Delete failed: " + ex.Message);
            AppDialog.Show("Player deletion failed. No further files will be removed.\n\n" + ex.Message, "Delete Player Files", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportPlayersCsv_Click(object sender, RoutedEventArgs e)
    {
        var rows = PlayersGrid.ItemsSource?.Cast<PlayerRow>().ToList() ?? [];
        if (rows.Count == 0)
        {
            AppDialog.Show("There are no players in the current filtered view to export.", "Player Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Player Database",
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"Myst_Players_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (dialog.ShowDialog(this) != true) return;
        static string Csv(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
        var output = new StringBuilder();
        output.AppendLine("Status,Player,Platform,Level,User ID,Steam ID,Player ID,First Seen,Last Seen,Sessions,Save Status,Save Path,Source,Banned,Notes");
        foreach (var p in rows)
            output.AppendLine(string.Join(",", new[] { p.Status, p.Name, p.Platform, p.Level, p.UserId, p.SteamId, p.PlayerId, p.FirstSeen, p.LastSeen, p.Sessions.ToString(), p.SaveStatus, p.SavePath, p.Source, p.Banned, p.Notes }.Select(Csv)));
        File.WriteAllText(dialog.FileName, output.ToString(), new UTF8Encoding(true));
        Log($"[PLAYERS] Exported {rows.Count} player record(s) to {dialog.FileName}.");
        RecordAudit("Success", "Players", "Player list exported", dialog.FileName, 4);
    }

    private void SavePlayerNote_Click(object sender, RoutedEventArgs e)
    {
        if (PlayersGrid.SelectedItem is not PlayerRow p) return;
        var key = playerHistory.ResolveKey(p);
        if (string.IsNullOrWhiteSpace(key)) return;
        playerHistory.SaveNotes(key, PlayerNotesBox.Text);
        RefreshPlayerHistoryGrid();
        Log($"[PLAYERS] Saved note for {p.Name}.");
        RecordAudit("Information", "Players", "Player note saved", p.Name, 4);
    }

    private static string GetAny(JsonElement e,params string[] names){foreach(var p in e.EnumerateObject())if(names.Any(n=>p.Name.Equals(n,StringComparison.OrdinalIgnoreCase)))return p.Value.ToString();return "";}
    private void Kick_Click(object s,RoutedEventArgs e)=>_ = PlayerAction(true);
    private void Ban_Click(object s,RoutedEventArgs e)=>_ = PlayerAction(false);
    private async Task PlayerAction(bool kick)
    {
        if (PlayersGrid.SelectedItem is not PlayerRow p) return;
        if (!p.Status.Equals("ONLINE", StringComparison.OrdinalIgnoreCase))
        {
            AppDialog.Show("Kick and Ban Selected require the player to be online. Offline players remain available in All Known Players for notes, IDs, and unban operations.", "Players", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var id = string.IsNullOrWhiteSpace(p.UserId) ? p.SteamId : p.UserId;
        if (string.IsNullOrWhiteSpace(id)) { AppDialog.Show("This player has no server UserID available yet.", "Players", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        try
        {
            using var api=Api();
            if(kick) await api.KickAsync(id,"Removed by administrator.");
            else
            {
                await api.BanAsync(id,"Banned by administrator.");
                var key = playerHistory.ResolveKey(p);
                if (!string.IsNullOrWhiteSpace(key)) playerHistory.MarkBanned(key, true);
                playerAdministration.SetBan(PlayerKey(p), p.Name, null, true);
            }
            await RefreshPlayersAsync(silent: false);
        }
        catch(Exception ex){Log("ERROR: "+ex.Message);}
    }

    private static string DetectPlayerPlatform(string userId, string steamId)
    {
        var id = (userId ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(steamId) || id.StartsWith("steam_", StringComparison.OrdinalIgnoreCase)) return "Steam";
        if (id.Contains("GDK", StringComparison.OrdinalIgnoreCase) || id.StartsWith("xbox_", StringComparison.OrdinalIgnoreCase)) return "Xbox/GDK";
        if (id.StartsWith("ps", StringComparison.OrdinalIgnoreCase)) return "PS5";
        return string.IsNullOrWhiteSpace(id) ? "Unknown" : "Crossplay";
    }

    private void AdminAccess_Click(object sender, RoutedEventArgs e)
    {
        if (PlayersGrid.SelectedItem is not PlayerRow p) { AppDialog.Show("Select a player first.", "Admin Access", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var uid = string.IsNullOrWhiteSpace(p.UserId) ? p.SteamId : p.UserId;
        var password = ReadRconSettings().Password;
        var text = $"Player: {p.Name}\nPlatform: {p.Platform}\nPlayer UID: {uid}\n\nVanilla Palworld does not provide a native command that remotely promotes one selected player. In-game admin is granted with /AdminPassword <password>. Admin Commands 1.0.1+ also supports PlayerUID-based permissions, but MystTiq will not guess or rewrite that mod's permission schema until it is detected from the installed mod.\n\nIn-game command for this session:\n/AdminPassword {(string.IsNullOrWhiteSpace(password) ? "<your admin password>" : password)}";
        AppDialog.Show(text, "Admin Access", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void Unban_Click(object sender, RoutedEventArgs e)
    {
        var id = PlayersGrid.SelectedItem is PlayerRow p ? (string.IsNullOrWhiteSpace(p.UserId) ? p.SteamId : p.UserId) : string.Empty;
        if (string.IsNullOrWhiteSpace(id)) { AppDialog.Show("Select a known player with a UserID/SteamID. You can also enter UnBanPlayer <UserID> in the RCON console.", "Unban", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        try
        {
            await EnsureRconConnectedAsync();
            var response = await rcon.ExecuteAsync("UnBanPlayer " + id);
            if (PlayersGrid.SelectedItem is PlayerRow selected)
            {
                var key = playerHistory.ResolveKey(selected);
                if (!string.IsNullOrWhiteSpace(key)) playerHistory.MarkBanned(key, false);
                playerAdministration.ClearBan(PlayerKey(selected));
            }
            RefreshPlayerHistoryGrid();
            Log("[RCON UNBAN] " + (string.IsNullOrWhiteSpace(response) ? id : response));
        }
        catch (Exception ex) { Log("Unban failed: " + ex.Message); }
    }

    private void ScheduledRestartCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (ScheduledRestartTimeBox is not null)
            ScheduledRestartTimeBox.IsEnabled = ScheduledRestartCheck?.IsChecked == true;
    }

    private void SaveAutomation_Click(object sender, RoutedEventArgs e)
    {
        var raw = ScheduledRestartTimeBox.Text.Trim();
        if (!TimeSpan.TryParse(raw, out var parsed) || parsed < TimeSpan.Zero || parsed >= TimeSpan.FromDays(1))
        {
            AppDialog.Show("Enter the scheduled restart time as HH:mm, for example 04:00.", "Automation", MessageBoxButton.OK, MessageBoxImage.Warning); return;
        }
        settings.ScheduledRestartEnabled = ScheduledRestartCheck.IsChecked == true;
        settings.ScheduledRestartTime = raw;
        settings.AutoCrashRecovery = AutoCrashRecoveryCheck.IsChecked == true;
        store.Save(settings);
        Log($"[AUTOMATION] Saved. Scheduled restart={(settings.ScheduledRestartEnabled ? raw : "off")}; crash recovery={(settings.AutoCrashRecovery ? "on" : "off")}.");
    }

    private void AutomationTimer_Tick(object? sender, EventArgs e)
    {
        if (!settings.ScheduledRestartEnabled || restartInProgress || !server.IsRunning()) return;
        if (!TimeSpan.TryParse(settings.ScheduledRestartTime, out var scheduled)) return;
        var now = DateTime.Now;
        if (lastScheduledRestartDate.Date == now.Date) return;
        if (now.TimeOfDay < scheduled || now.TimeOfDay > scheduled + TimeSpan.FromMinutes(1)) return;
        lastScheduledRestartDate = now.Date;
        Log("[AUTOMATION] Scheduled daily restart triggered. MystTiq will save, stop, back up, and restart using the existing controlled restart lifecycle.");
        Restart_Click(this, new RoutedEventArgs());
    }

    private void RefreshBackups_Click(object s,RoutedEventArgs e)=>RefreshBackups();
    private void RefreshBackups()
    {
        var rows = backups.List();
        BackupsGrid.ItemsSource = rows;
        RefreshBackupCenterSummary();

        if (rows.Count == 0)
        {
            BackupsStatusText.Foreground = new SolidColorBrush(Color.FromRgb(159, 196, 234));
            BackupsStatusText.Text = "Backup status: No backups have been created yet.";
            return;
        }

        var verified = rows.Count(row => row.Status.Equals("Verified", StringComparison.OrdinalIgnoreCase));
        var attention = rows.Count - verified;
        BackupsStatusText.Foreground = attention == 0 ? Brushes.LightGreen : Brushes.Gold;
        BackupsStatusText.Text = attention == 0
            ? $"Backup health: {verified} verified backup(s)."
            : $"Backup health: {verified} verified, {attention} need verification.";
    }

    private void VerifyBackup_Click(object sender, RoutedEventArgs e)
    {
        if (BackupsGrid.SelectedItem is not BackupRow backup)
        {
            AppDialog.Show("Select a backup first.", "Verify Backup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _ = RunExclusive(async ct =>
        {
            try
            {
                BackupsStatusText.Foreground = Brushes.Gold;
                BackupsStatusText.Text = $"Backup status: Verifying {Path.GetFileName(backup.FilePath)}...";
                var result = await backups.VerifyAsync(backup.FilePath, ct);
                RefreshBackups();
                BackupsStatusText.Foreground = Brushes.LightGreen;
                BackupsStatusText.Text = "Backup verified successfully: " + result.Summary;
                Log($"Backup verification passed: {backup.FilePath}; SHA-256 {result.Sha256}");
                AppDialog.Show(
                    $"Backup verified successfully.\n\n{Path.GetFileName(backup.FilePath)}\n{result.Summary}\n\nSHA-256: {result.Sha256}",
                    "Backup Verified",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                BackupsStatusText.Foreground = Brushes.IndianRed;
                BackupsStatusText.Text = "Backup verification failed: " + ex.Message;
                throw;
            }
        });
    }

    private void Restore_Click(object s, RoutedEventArgs e)
    {
        if (BackupsGrid.SelectedItem is not BackupRow backup)
        {
            BackupsStatusText.Foreground = Brushes.IndianRed;
            BackupsStatusText.Text = "Restore failed: Select a backup first.";
            AppDialog.Show(
                "Select a backup first.",
                "Restore Backup",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (server.IsRunning())
        {
            BackupsStatusText.Foreground = Brushes.IndianRed;
            BackupsStatusText.Text = "Restore blocked: Stop the server before restoring a backup.";
            AppDialog.Show(
                "The Palworld server must be stopped before a backup can be restored.",
                "Restore Backup",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var choice = AppDialog.Show(
            $"Restore this backup?\n\n{Path.GetFileName(backup.FilePath)}\n{backup.SizeMb:N2} MB\nCreated {backup.Created:g}\n\n" +
            "The current SaveGames folder will be preserved as a safety copy before it is replaced.",
            "Confirm Backup Restore",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (choice != MessageBoxResult.Yes)
            return;

        _ = RunExclusive(async ct =>
        {
            try
            {
                BackupsStatusText.Foreground = Brushes.Gold;
                BackupsStatusText.Text = "Restore status: Validating and restoring the selected backup...";

                var safetyPath = await backups.RestoreAsync(backup.FilePath, server, ct);
                Log("Backup restored successfully: " + backup.FilePath);
                Log("Previous save preserved at: " + safetyPath);

                BackupsStatusText.Foreground = Brushes.LightGreen;
                BackupsStatusText.Text = $"Restore completed successfully. Previous save preserved at: {safetyPath}";
                AppDialog.Show(
                    "Backup restored successfully.\n\n" +
                    $"Restored: {Path.GetFileName(backup.FilePath)}\n" +
                    $"Previous save safety copy: {safetyPath}\n\n" +
                    "You can now start the server and verify the restored world.",
                    "Restore Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                BackupsStatusText.Foreground = Brushes.IndianRed;
                BackupsStatusText.Text = "Restore failed: " + ex.Message;
                throw;
            }
        });
    }

    private void DeleteBackup_Click(object sender, RoutedEventArgs e)
    {
        if (BackupsGrid.SelectedItem is not BackupRow backup)
        {
            AppDialog.Show("Select a backup first.", "Delete Backup", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var choice = AppDialog.Show(
            $"Permanently delete this backup?\n\n{Path.GetFileName(backup.FilePath)}\n{backup.SizeMb:N2} MB\nCreated {backup.Created:g}",
            "Delete Backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (choice != MessageBoxResult.Yes)
            return;

        _ = RunExclusive(_ =>
        {
            backups.Delete(backup.FilePath);
            Log("Deleted backup: " + backup.FilePath);
            RefreshBackups();
            return Task.CompletedTask;
        });
    }

    private void RefreshMods_Click(object s,RoutedEventArgs e)=>RefreshMods();
    private void RefreshMods()
    {
        var snapshot = modInventory.Current("Scan Library", force: true);
        var installed = snapshot.Mods.ToList();
        runtimeState.ApplyTo(installed);
        ModsGrid.ItemsSource = installed;
        localModRows = new ObservableCollection<LocalModRow>(snapshot.LocalMods);
        LocalModsGrid.ItemsSource = localModRows;
        ModDashLastVerified.Text = $"Last Scan: {snapshot.ScannedAt:MMM d yyyy}  {snapshot.ScannedAt:h:mm tt}  •  Duration: {snapshot.Duration.TotalSeconds:0.0} sec  •  One Scan";
        ModLibrarySummaryText.Text = $"Installed: {installed.Count}  •  Enabled: {installed.Count(x => x.Enabled)}  •  Disabled: {installed.Count(x => !x.Enabled)}";
        RefreshModDashboard(installed);
        RefreshModRuntime(installed);

        // Local workshop packages are often identified only by their numeric Steam ID.
        // Resolve friendly Workshop titles in the background and keep the ID visible
        // in brackets so the name is readable without losing the exact package identity.
        _ = RefreshWorkshopDisplayNamesAsync(installed, localModRows, forceRefresh: false);
    }

    private async Task RefreshWorkshopDisplayNamesAsync(IList<ModRow> installed, IList<LocalModRow> localRows, bool forceRefresh)
    {
        try
        {
            var ids = localRows.Select(row => row.WorkshopId)
                .Concat(installed.Select(GetWorkshopId))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in ids)
            {
                try
                {
                    var metadata = await GetWorkshopMetadataAsync(id, forceRefresh);
                    var title = CleanWorkshopTitle(metadata.Title);
                    if (!string.IsNullOrWhiteSpace(title))
                        resolved[id] = $"{title} ({id})";
                }
                catch (Exception ex)
                {
                    Log($"Workshop name lookup failed for {id}: {ex.Message}");
                }
            }

            if (resolved.Count == 0)
                return;

            foreach (var row in localRows)
                if (resolved.TryGetValue(row.WorkshopId, out var displayName))
                    row.Name = displayName;

            foreach (var row in installed)
            {
                var id = GetWorkshopId(row);
                if (!string.IsNullOrWhiteSpace(id) && resolved.TryGetValue(id, out var displayName))
                    row.Name = displayName;
            }

            // These row models do not implement INotifyPropertyChanged, so explicitly
            // refresh the views after title enrichment. Preserve current selections.
            var selectedInstalledPackage = (ModsGrid.SelectedItem as ModRow)?.Package;
            var selectedLocalId = (LocalModsGrid.SelectedItem as LocalModRow)?.WorkshopId;
            ModsGrid.Items.Refresh();
            LocalModsGrid.Items.Refresh();
            RefreshModDashboard(installed);

            if (!string.IsNullOrWhiteSpace(selectedInstalledPackage))
                ModsGrid.SelectedItem = installed.FirstOrDefault(row => row.Package.Equals(selectedInstalledPackage, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(selectedLocalId))
                LocalModsGrid.SelectedItem = localRows.FirstOrDefault(row => row.WorkshopId.Equals(selectedLocalId, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            Log("Workshop display-name refresh failed: " + ex.Message);
        }
    }

    private static string GetWorkshopId(ModRow row)
    {
        const string prefix = "Steam Workshop ";
        return row.Source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? row.Source[prefix.Length..].Trim()
            : string.Empty;
    }

    private static string CleanWorkshopTitle(string title)
    {
        var value = WebUtility.HtmlDecode(title ?? string.Empty).Trim();
        foreach (var prefix in new[] { "Steam Workshop::", "Steam Community :: Workshop :: " })
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                value = value[prefix.Length..].Trim();
        return value;
    }

    private void RefreshModDashboard(IEnumerable<ModRow> installed)
    {
        var rows = installed.ToList();
        var previousRows = modDashboardRows.Count > 0
            ? modDashboardRows.ToList()
            : LoadPersistedModScanResults();
        var previous = previousRows.ToDictionary(row => row.Package, StringComparer.OrdinalIgnoreCase);
        var updated = new List<ModDashboardRow>();

        foreach (var mod in rows)
        {
            if (previous.TryGetValue(mod.Package, out var existing))
            {
                var filesChanged = !string.Equals(existing.FilesStatus, mod.Deployed ? "Present" : "Missing", StringComparison.OrdinalIgnoreCase);
                var enabledChanged = !string.Equals(existing.EnabledStatus, mod.Enabled ? "Enabled" : "Disabled", StringComparison.OrdinalIgnoreCase);

                existing.Name = mod.Name;
                existing.Type = mod.Type;
                existing.FilesStatus = mod.Deployed ? "Present" : "Missing";
                existing.EnabledStatus = mod.Enabled ? "Enabled" : "Disabled";

                // Installation/enabled-state changes invalidate the old verification.
                if (filesChanged || enabledChanged)
                {
                    existing.RuntimeStatus = "Not checked";
                    existing.ErrorStatus = "Not checked";
                    existing.LastVerified = "Never";
                    var evaluation = modHealthEvaluation.Evaluate(mod, server.IsRunning(), runtimeChecked: false);
                    existing.Health = evaluation.DisplayStatus;
                    existing.HealthScore = evaluation.Score;
                    existing.Details = evaluation.Detail;
                }

                // RuntimeStateService is the authoritative current-session source. A
                // positive runtime observation must immediately heal a previously
                // Runtime-Unverified Dashboard row during an ordinary Library refresh;
                // otherwise Dashboard and Library can disagree even though they share
                // the same backend state. Never infer an unload from missing text here.
                if (server.IsRunning() && mod.LoadedByUe4ss &&
                    (mod.Type.Contains("UE4SS", StringComparison.OrdinalIgnoreCase) ||
                     mod.Source.Contains("UE4SS", StringComparison.OrdinalIgnoreCase)))
                {
                    existing.RuntimeStatus = "Loaded";
                    existing.ErrorStatus = existing.ErrorStatus == "Not checked" ? "None" : existing.ErrorStatus;
                    existing.LastVerified = DateTime.Now.ToString("g");
                    var evaluation = modHealthEvaluation.Evaluate(mod, serverRunning: true, runtimeChecked: true);
                    existing.Health = evaluation.DisplayStatus;
                    existing.HealthScore = evaluation.Score;
                    existing.Details = evaluation.Detail;
                }

                updated.Add(existing);
                continue;
            }

            updated.Add(new ModDashboardRow
            {
                Package = mod.Package,
                Name = mod.Name,
                Type = mod.Type,
                FilesStatus = mod.Deployed ? "Present" : "Missing",
                EnabledStatus = mod.Enabled ? "Enabled" : "Disabled",
                RuntimeStatus = "Not checked",
                ErrorStatus = "Not checked",
                DependencyStatus = "Not scanned",
                ConflictStatus = "Not scanned",
                VersionStatus = "Not checked",
                Compatibility = "Not scanned",
                Health = modHealthEvaluation.Evaluate(mod, server.IsRunning(), runtimeChecked: false).DisplayStatus,
                HealthScore = modHealthEvaluation.Evaluate(mod, server.IsRunning(), runtimeChecked: false).Score,
                Details = modHealthEvaluation.Evaluate(mod, server.IsRunning(), runtimeChecked: false).Detail
            });
        }

        modDashboardRows = new ObservableCollection<ModDashboardRow>(updated);
        ModDashboardGrid.ItemsSource = modDashboardRows;
        SelectFirstModDashboardRow();
        var runtimeChecked = modDashboardRows.Any(row => row.LastVerified != "Never");
        UpdateModDashboardSummary(rows.Count, modDashboardRows, runtimeChecked);
    }

    private List<ModDashboardRow> LoadPersistedModScanResults()
    {
        try
        {
            if (!File.Exists(ModScanResultsPath)) return [];
            return System.Text.Json.JsonSerializer.Deserialize<List<ModDashboardRow>>(File.ReadAllText(ModScanResultsPath)) ?? [];
        }
        catch (Exception ex)
        {
            Log("[MODS] Persisted scan results could not be loaded: " + ex.Message);
            return [];
        }
    }

    private void SavePersistedModScanResults(IEnumerable<ModDashboardRow> rows)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ModScanResultsPath)!);
            File.WriteAllText(ModScanResultsPath,
                System.Text.Json.JsonSerializer.Serialize(rows.ToList(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Log("[MODS] Scan results could not be persisted: " + ex.Message);
        }
    }

    private void ModDashboardGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectedModDetails(ModDashboardGrid.SelectedItem as ModDashboardRow);
    }

    private void UpdateSelectedModDetails(ModDashboardRow? row)
    {
        if (row is null)
        {
            ModDetailName.Text = "Select a mod";
            ModDetailType.Text = string.Empty;
            ModDetailHealth.Text = "—";
            ModDetailScore.Text = "—";
            ModDetailInstallation.Text = string.Empty;
            ModDetailRuntime.Text = string.Empty;
            ModDetailCompatibility.Text = string.Empty;
            ModDetailEvidence.Text = "Select a mod to view verification evidence.";
            return;
        }

        ModDetailName.Text = row.Name;
        ModDetailType.Text = row.Type;
        ModDetailHealth.Text = row.Health;
        ModDetailScore.Text = row.Health.ToUpperInvariant();
        ModDetailInstallation.Text = $"Files: {row.FilesStatus}\nEnabled: {row.EnabledStatus}";
        ModDetailRuntime.Text = $"Runtime: {row.RuntimeStatus}\nErrors: {row.ErrorStatus}\nLast verified: {row.LastVerified}";
        ModDetailCompatibility.Text = $"Dependencies: {row.DependencyStatus}\nConflicts: {row.ConflictStatus}\nVersion: {row.VersionStatus}\nStatic compatibility: {row.Compatibility}";
        ModDetailEvidence.Text = string.IsNullOrWhiteSpace(row.Details) ? "No additional evidence has been recorded." : row.Details;
    }

    private void SelectFirstModDashboardRow()
    {
        if (modDashboardRows.Count > 0)
        {
            ModDashboardGrid.SelectedIndex = 0;
            UpdateSelectedModDetails(modDashboardRows[0]);
        }
        else
        {
            UpdateSelectedModDetails(null);
        }
    }

    private void VerifySelectedMod_Click(object sender, RoutedEventArgs e)
    {
        if (ModDashboardGrid.SelectedItem is not ModDashboardRow selected)
        {
            AppDialog.Show("Select a mod first.", "MOD Verification", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            var installed = mods.Scan();
            var mod = installed.FirstOrDefault(item => item.Package.Equals(selected.Package, StringComparison.OrdinalIgnoreCase));
            if (mod is null)
                throw new InvalidOperationException("The selected mod is no longer installed.");

            var result = modVerification.VerifyAll(new[] { mod }, server.IsRunning()).Single();
            selected.FilesStatus = result.FilesStatus;
            selected.EnabledStatus = result.Enabled ? "Enabled" : "Disabled";
            selected.RuntimeStatus = result.RuntimeStatus;
            selected.ErrorStatus = result.ErrorSummary;
            selected.Health = ModHealthEvaluationService.ToDisplayText(result.HealthStatus);
            selected.HealthScore = result.HealthScore;
            selected.Details = result.Details;
            selected.LastVerified = result.VerifiedAt.ToString("g");

            // Rebind because ModDashboardRow is intentionally a simple view model.
            ModDashboardGrid.ItemsSource = null;
            ModDashboardGrid.ItemsSource = modDashboardRows;
            ModDashboardGrid.SelectedItem = selected;
            UpdateSelectedModDetails(selected);
            UpdateModDashboardSummary(installed.Count, modDashboardRows, true);
        }
        catch (Exception ex)
        {
            AppDialog.Show(ex.Message, "MOD Verification", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateModDashboardSummary(int installedCount, IEnumerable<ModDashboardRow> results, bool runtimeChecked)
    {
        var rows = results.ToList();
        var healthyCount = rows.Count(x => x.Health == "Healthy");
        var runtimeUnverifiedCount = rows.Count(x => x.Health == "Runtime Unverified");
        var failedCount = rows.Count(x => x.Health is "Failed" or "Missing");
        var attentionCount = rows.Count(x => x.Health is "Runtime Unverified" or "Misconfigured" or "Attention" or "Failed" or "Missing" or "Unknown");
        var updates = rows.Count(x => x.VersionStatus.StartsWith("Update ", StringComparison.OrdinalIgnoreCase));
        var conflicts = rows.Count(x => x.Compatibility == "Conflict");
        var missingDependencies = rows.Count(x => x.DependencyStatus.StartsWith("Missing ", StringComparison.OrdinalIgnoreCase));

        ModDashInstalled.Text = installedCount.ToString();
        ModDashHealthy.Text = healthyCount.ToString();
        ModDashUpdates.Text = updates.ToString();
        ModDashConflicts.Text = conflicts.ToString();
        ModDashDependencies.Text = missingDependencies.ToString();
        ModDashFailed.Text = failedCount.ToString();
        if (runtimeChecked) ModDashLastVerified.Text = $"Last Scan: {DateTime.Now:MMM d yyyy}  {DateTime.Now:h:mm tt}  •  Verification complete";

        if (installedCount == 0)
        {
            ModDashHealthText.Text = "No mods detected";
            ModDashHealthDetails.Text = "Install mods from the MOD Library to begin verification.";
            ModDashScore.Text = "—";
            ModDashHealthBanner.Background = new SolidColorBrush(Color.FromRgb(70, 85, 104));
            return;
        }

        ModDashScore.Text = failedCount > 0 ? "FAILED"
            : attentionCount > 0 ? "ATTENTION"
            : runtimeChecked ? "WORKING"
            : "UNKNOWN";
        if (runtimeChecked && failedCount == 0 && attentionCount == 0)
        {
            ModDashHealthText.Text = "All detected mods are healthy";
            ModDashHealthDetails.Text = "All detected mods passed their centralized health rules. UE4SS/Lua mods have matching runtime load evidence; non-UE4SS mods passed installation and enabled-state verification.";
            ModDashHealthBanner.Background = new SolidColorBrush(Color.FromRgb(23, 58, 42));
        }
        else if (attentionCount == 0)
        {
            ModDashHealthText.Text = runtimeChecked ? "Runtime verification complete" : "Mods detected — verification required";
            ModDashHealthDetails.Text = runtimeChecked
                ? "No failures were detected. Run Scan Compatibility to check dependencies, local version differences, and mod overlap."
                : "Inventory refresh completed. Health remains Unknown until Verify All Mods establishes runtime evidence.";
            ModDashHealthBanner.Background = runtimeChecked
                ? new SolidColorBrush(Color.FromRgb(23, 58, 42))
                : new SolidColorBrush(Color.FromRgb(32, 64, 96));
        }
        else
        {
            ModDashHealthText.Text = $"{attentionCount} mod{(attentionCount == 1 ? "" : "s")} need attention";
            ModDashHealthDetails.Text = failedCount > 0
                ? $"{failedCount} failed or missing. Review runtime, error, and evidence details."
                : runtimeUnverifiedCount > 0
                    ? $"{runtimeUnverifiedCount} UE4SS mod{(runtimeUnverifiedCount == 1 ? "" : "s")} are active but runtime-unverified. Start/refresh the server and verify again for UE4SS load evidence."
                    : "Review misconfigured mods, state mismatches, duplicates, or other verification evidence.";
            ModDashHealthBanner.Background = new SolidColorBrush(Color.FromRgb(74, 54, 24));
        }
    }

    private void VerifyAllMods_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ModDashHealthText.Text = "Verifying installed mods…";
            ModDashHealthDetails.Text = "Checking files, enabled state, recent UE4SS/server logs, runtime evidence, and errors.";
            ModDashScore.Text = "…";

            var inventory = modInventory.Current("Verify & Scan All MODs", force: true);
            var installed = inventory.Mods.ToList();
            ModsGrid.ItemsSource = installed;
            localModRows = new ObservableCollection<LocalModRow>(inventory.LocalMods);
            LocalModsGrid.ItemsSource = localModRows;
            ModLibrarySummaryText.Text = $"Installed: {installed.Count}  •  Enabled: {installed.Count(x => x.Enabled)}  •  Disabled: {installed.Count(x => !x.Enabled)}";
            var verification = modVerification.VerifyAll(installed, server.IsRunning());
            var compatibility = modCompatibility.Scan(installed);
            modDashboardRows = new ObservableCollection<ModDashboardRow>(verification.Select(result => new ModDashboardRow
            {
                Package = result.Package,
                Name = result.Name,
                Type = result.Type,
                FilesStatus = result.FilesStatus,
                EnabledStatus = result.Enabled ? "Enabled" : "Disabled",
                RuntimeStatus = result.RuntimeStatus,
                ErrorStatus = result.ErrorSummary,
                DependencyStatus = compatibility.Results.First(x => x.Package.Equals(result.Package, StringComparison.OrdinalIgnoreCase)).DependencyStatus,
                ConflictStatus = compatibility.Results.First(x => x.Package.Equals(result.Package, StringComparison.OrdinalIgnoreCase)).ConflictStatus,
                VersionStatus = compatibility.Results.First(x => x.Package.Equals(result.Package, StringComparison.OrdinalIgnoreCase)).VersionStatus,
                Compatibility = compatibility.Results.First(x => x.Package.Equals(result.Package, StringComparison.OrdinalIgnoreCase)).OverallStatus,
                Health = ModHealthEvaluationService.ToDisplayText(result.HealthStatus),
                HealthScore = result.HealthScore,
                Details = result.Details,
                LastVerified = result.VerifiedAt.ToString("g")
            }));
            ModDashboardGrid.ItemsSource = modDashboardRows;
            SavePersistedModScanResults(modDashboardRows);
            SelectFirstModDashboardRow();
            UpdateModDashboardSummary(installed.Count, modDashboardRows, true);
            ModDashLastVerified.Text = $"Last Scan: {inventory.ScannedAt:MMM d yyyy}  {inventory.ScannedAt:h:mm tt}  •  Duration: {inventory.Duration.TotalSeconds:0.0} sec  •  One Scan";

            var healthy = modDashboardRows.Count(x => x.Health == "Healthy");
            var runtimeUnverified = modDashboardRows.Count(x => x.Health == "Runtime Unverified");
            var attention = modDashboardRows.Count(x => x.Health is "Attention" or "Misconfigured");
            var disabled = modDashboardRows.Count(x => x.Health == "Disabled");
            var failed = modDashboardRows.Count(x => x.Health is "Failed" or "Missing");
            var unknown = modDashboardRows.Count(x => x.Health == "Unknown");
            AppDialog.Show(
                failed == 0 && attention == 0 && runtimeUnverified == 0 && unknown == 0
                    ? $"Verification completed. {healthy} healthy; {disabled} disabled. All enabled detected mods satisfy their centralized health rules."
                    : $"Verification completed. Healthy: {healthy}; Runtime Unverified: {runtimeUnverified}; Attention/Misconfigured: {attention}; Disabled: {disabled}; Failed/Missing: {failed}; Unknown: {unknown}. Review the MOD Dashboard for details.",
                "MOD Runtime Verification",
                MessageBoxButton.OK,
                failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppDialog.Show($"MOD verification could not be completed.\n\n{ex.Message}", "MOD Verification", MessageBoxButton.OK, MessageBoxImage.Error);
            RefreshMods();
        }
    }

    private void ExportModVerificationReport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var inventory = modInventory.Current("Export MOD verification report", force: true);
            var verification = modVerification.VerifyAll(inventory.Mods, server.IsRunning());
            var recommendations = modRepairRecommendations.Build(inventory.Mods, verification);
            var exported = modVerificationReportExporter.Export(verification, recommendations);
            Log($"MOD verification report exported: {exported.TextPath}");
            AppDialog.Show(
                $"Verification report exported for {exported.ModCount} MOD(s).\n\nText: {exported.TextPath}\nJSON: {exported.JsonPath}",
                "MOD Verification Report", MessageBoxButton.OK, MessageBoxImage.Information);
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{exported.TextPath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppDialog.Show($"The MOD verification report could not be exported.\n\n{ex.Message}",
                "MOD Verification Report", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ScanCompatibility_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ModDashHealthText.Text = "Scanning mod compatibility…";
            ModDashHealthDetails.Text = "Checking declared dependencies, managed file overlap, known conflict rules, feature overlap, and local Workshop versions.";

            var installed = mods.Scan();
            ModsGrid.ItemsSource = installed;
            localModRows = environment.ScanLocalMods();
            LocalModsGrid.ItemsSource = localModRows;
            var summary = modCompatibility.Scan(installed);
            var existing = modDashboardRows.ToDictionary(row => row.Package, StringComparer.OrdinalIgnoreCase);

            var updated = new List<ModDashboardRow>();
            foreach (var mod in installed)
            {
                existing.TryGetValue(mod.Package, out var row);
                row ??= new ModDashboardRow
                {
                    Package = mod.Package,
                    Name = mod.Name,
                    Type = mod.Type,
                    FilesStatus = mod.Deployed ? "Present" : "Missing",
                    EnabledStatus = mod.Enabled ? "Enabled" : "Disabled",
                    RuntimeStatus = "Not checked",
                    ErrorStatus = "Not checked",
                    Health = !mod.Deployed ? "Failed" : !mod.Enabled ? "Disabled" : server.IsRunning() ? "Active" : "Installed",
                    HealthScore = 0
                };

                var result = summary.Results.First(item => item.Package.Equals(mod.Package, StringComparison.OrdinalIgnoreCase));
                row.DependencyStatus = result.DependencyStatus;
                row.ConflictStatus = result.ConflictStatus;
                row.VersionStatus = result.VersionStatus;
                row.Compatibility = result.OverallStatus;

                const string workshopPrefix = "Steam Workshop ";
                if (mod.Source.StartsWith(workshopPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var workshopId = mod.Source[workshopPrefix.Length..].Trim();
                    var local = localModRows.FirstOrDefault(item => item.WorkshopId.Equals(workshopId, StringComparison.OrdinalIgnoreCase));
                    if (local?.UpdateStatus == "UPDATE AVAILABLE")
                    {
                        row.VersionStatus = "Update available";
                        if (row.Compatibility == "Compatible") row.Compatibility = "Attention";
                    }
                    else if (local?.UpdateStatus == "CURRENT")
                    {
                        row.VersionStatus = "Current";
                    }
                }

                row.Details = string.IsNullOrWhiteSpace(row.Details) || row.Details.StartsWith("Run Verify", StringComparison.OrdinalIgnoreCase)
                    ? result.Details
                    : row.Details + " Compatibility: " + result.Details;
                updated.Add(row);
            }

            modDashboardRows = new ObservableCollection<ModDashboardRow>(updated);
            ModDashboardGrid.ItemsSource = modDashboardRows;
            SelectFirstModDashboardRow();
            UpdateModDashboardSummary(installed.Count, modDashboardRows, modDashboardRows.Any(row => row.RuntimeStatus != "Not checked"));

            var compatibleCount = modDashboardRows.Count(row => row.Compatibility == "Compatible");
            var updateCount = modDashboardRows.Count(row => row.VersionStatus.StartsWith("Update", StringComparison.OrdinalIgnoreCase));
            var conflictCount = modDashboardRows.Count(row => row.Compatibility == "Conflict");
            var missingCount = modDashboardRows.Count(row => row.DependencyStatus.StartsWith("Missing ", StringComparison.OrdinalIgnoreCase));
            var attentionCount = modDashboardRows.Count(row => row.Compatibility == "Attention");
            var message = $"Compatibility scan complete. Compatible: {compatibleCount}; Updates: {updateCount}; Conflicts: {conflictCount}; Missing dependencies: {missingCount}; Attention: {attentionCount}.";
            ModDashHealthDetails.Text = message + " Runtime verification remains separate; use Verify All Mods to confirm loading.";
            AppDialog.Show(message, "MOD Compatibility Scan", MessageBoxButton.OK,
                conflictCount > 0 || missingCount > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppDialog.Show($"Compatibility scan could not be completed.\n\n{ex.Message}", "MOD Compatibility", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenModLibrary_Click(object sender, RoutedEventArgs e) => NavigateToPage(8);

    private void OpenModsRoot_Click(object sender, RoutedEventArgs e)
    {
        var path = ue4ssRuntimeResolver.GetActiveModsRoot();
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private void MigrateLegacyMods_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var preview = mods.InspectLegacyMigration();
            if (!preview.IsMigrationRequired || preview.CandidateCount == 0)
            {
                AppDialog.Show(
                    $"No user MOD folders require migration.\n\nLegacy: {preview.LegacyRoot}\nActive: {preview.ActiveRoot}",
                    "UE4SS Legacy MOD Migration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (server.IsRunning())
            {
                AppDialog.Show(
                    "Stop PalServer before migrating legacy UE4SS MOD files. The migration is copy-first and non-destructive, but MOD files should not be changing while they are copied.",
                    "Stop Server Before Migration",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var legacyOnly = preview.LegacyOnlyMods.Count;
            var alreadyPresent = preview.AlreadyPresentMods.Count;
            var skipped = preview.SkippedRuntimeComponents.Count;
            var choice = AppDialog.Show(
                $"UE4SS mod path mismatch detected.\n\nManaged / legacy:\n{preview.LegacyRoot}\n\nActive UE4SS:\n{preview.ActiveRoot}\n\n" +
                $"Legacy-only user MODs: {legacyOnly}\nAlready present in active root: {alreadyPresent}\nUE4SS runtime component folders skipped: {skipped}\n\n" +
                "MystTiq will COPY missing user MOD files into the active root. Existing active files will never be overwritten, and the legacy copies will not be deleted. Continue?",
                "Migrate Legacy UE4SS MODs",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (choice != MessageBoxResult.Yes) return;

            var result = mods.MigrateLegacyMods();
            RefreshMods();
            Log($"UE4SS legacy migration completed: {result.CopiedModCount} mod folder(s), {result.CopiedFileCount} file(s) copied, {result.ConflictCount} conflict(s) preserved.");

            var message = $"Migration completed.\n\nMOD folders copied: {result.CopiedModCount}\nFiles copied: {result.CopiedFileCount}\nConflicts preserved: {result.ConflictCount}\n\nLegacy copies were retained for rollback safety.";
            if (result.Warnings.Count > 0)
                message += "\n\nNotes:\n" + string.Join("\n", result.Warnings.Select(item => "• " + item));

            AppDialog.Show(message, "UE4SS Legacy MOD Migration", MessageBoxButton.OK,
                result.ConflictCount == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            Log($"UE4SS legacy migration failed: {ex.Message}");
            AppDialog.Show(ex.Message, "UE4SS Legacy MOD Migration Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    private void ModsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ModsGrid.SelectedItem is not ModRow mod) return;
        LocalModsGrid.SelectedItem = null;
        ModInfoName.Text = mod.Name;
        ModInfoSource.Text = $"Installed server mod • {mod.Source} • {mod.Type}";
        ModInfoStatus.Text = mod.Status;
        ModInfoVersion.Text = string.IsNullOrWhiteSpace(mod.Version) ? "Version not provided" : mod.Version;
        ModInfoAuthor.Text = "Unknown";
        ModInfoDescription.Text = mod.Description;
        ModInfoDetails.Text = $"Package: {mod.Package}\r\nDeployed: {(mod.Deployed ? "Yes" : "No")}\r\nEnabled: {(mod.Enabled ? "Yes" : "No")}";
        ModInfoCompatibility.Text = BuildInstalledCompatibility(mod);
        ModInfoOnlineStatus.Text = "No Workshop ID is associated with this installed package. Use Search Online to look for public information.";
        SetModHealth(mod.Deployed && mod.Enabled ? "READY TO USE" : mod.Deployed ? "DISABLED" : "ATTENTION REQUIRED", mod.Deployed && mod.Enabled ? "ready" : mod.Deployed ? "warning" : "error");
    }

    private void LocalModsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LocalModsGrid.SelectedItem is not LocalModRow mod) return;
        ModsGrid.SelectedItem = null;
        ModInfoName.Text = mod.Name;
        ModInfoSource.Text = $"Steam Workshop • ID {mod.WorkshopId}";
        ModInfoStatus.Text = mod.UpdateStatus;
        ModInfoVersion.Text = $"Steam copy: {mod.AvailableVersion}\r\nInstalled: {mod.InstalledVersion}";
        ModInfoAuthor.Text = mod.Author;
        ModInfoDescription.Text = mod.Description;
        ModInfoDetails.Text = $"Type: {mod.Type}\r\nOptions: {mod.Variants}\r\nSize: {mod.Size}\r\nLocal update time: {mod.LastUpdated:g}";
        ModInfoCompatibility.Text = BuildWorkshopCompatibility(mod);
        ModInfoOnlineStatus.Text = "Checking the Steam Workshop for current information...";
        SetModHealth(mod.UpdateStatus == "UPDATE AVAILABLE" ? "UPDATE RECOMMENDED" : mod.UpdateStatus == "CURRENT" ? "READY TO USE" : "AVAILABLE TO INSTALL", mod.UpdateStatus == "UPDATE AVAILABLE" ? "warning" : "ready");
        _ = LoadOnlineWorkshopInfoAsync(mod, forceRefresh: false);
    }


    private string BuildInstalledCompatibility(ModRow mod)
    {
        var ue4ssReady = environment.VerifyComponent("UE4SS Runtime").Success;
        try
        {
            var result = modCompatibility.Scan(mods.Scan()).Results
                .FirstOrDefault(item => item.Package.Equals(mod.Package, StringComparison.OrdinalIgnoreCase));
            if (result is not null)
            {
                return $"Palworld Dedicated Server: {(mod.Deployed ? "Detected" : "Files missing")}\r\n" +
                       $"UE4SS Runtime: {(ue4ssReady ? "Installed" : "Needs attention")}\r\n" +
                       $"Dependencies: {result.DependencyStatus}\r\n" +
                       $"Conflicts: {result.ConflictStatus}\r\n" +
                       $"Version: {result.VersionStatus}\r\n" +
                       $"Overall: {result.OverallStatus}\r\n" +
                       "Crossplay: Unknown unless declared by the mod author";
            }
        }
        catch { }

        return $"Palworld Dedicated Server: {(mod.Deployed ? "Detected" : "Files missing")}\r\n" +
               $"UE4SS Runtime: {(ue4ssReady ? "Installed" : "Needs attention")}\r\n" +
               "Compatibility scan unavailable\r\nCrossplay: Unknown";
    }

    private string BuildWorkshopCompatibility(LocalModRow mod)
    {
        var ue4ssReady = environment.VerifyComponent("UE4SS Runtime").Success;
        return $"Palworld version: {mod.Compatibility}\r\n" +
               $"UE4SS Runtime: {(ue4ssReady ? "Installed" : "Needs attention")}\r\n" +
               "Dedicated server: Verify with the author\r\nClient installation: Check description\r\nCrossplay: Check description\r\nDependencies: Check Workshop requirements";
    }

    private void SetModHealth(string text, string state)
    {
        ModHealthText.Text = text;
        ModHealthBanner.Background = state switch
        {
            "ready" => new SolidColorBrush(Color.FromRgb(31, 139, 76)),
            "warning" => new SolidColorBrush(Color.FromRgb(179, 122, 24)),
            "error" => new SolidColorBrush(Color.FromRgb(176, 57, 57)),
            _ => new SolidColorBrush(Color.FromRgb(70, 85, 104))
        };
    }

    private async void RefreshSelectedModInfo_Click(object sender, RoutedEventArgs e)
    {
        // REFRESH INFO is a local/runtime refresh action. Searching the web is kept
        // exclusively behind SEARCH ONLINE so the two buttons never perform the same
        // action or unexpectedly launch a browser.
        if (ModsGrid.SelectedItem is ModRow installed)
        {
            var package = installed.Package;
            RefreshMods();
            var refreshed = (ModsGrid.ItemsSource as IEnumerable<ModRow>)?
                .FirstOrDefault(row => row.Package.Equals(package, StringComparison.OrdinalIgnoreCase));
            if (refreshed is not null)
            {
                ModsGrid.SelectedItem = refreshed;
                ModsGrid.ScrollIntoView(refreshed);
                ModInfoOnlineStatus.Text = "Local metadata, deployment state, and current-session runtime status refreshed.";
            }
            return;
        }

        if (LocalModsGrid.SelectedItem is LocalModRow local)
        {
            var workshopId = local.WorkshopId;
            RefreshMods();
            var refreshed = localModRows.FirstOrDefault(row =>
                row.WorkshopId.Equals(workshopId, StringComparison.OrdinalIgnoreCase));
            if (refreshed is not null)
            {
                LocalModsGrid.SelectedItem = refreshed;
                LocalModsGrid.ScrollIntoView(refreshed);
                if (!string.IsNullOrWhiteSpace(refreshed.WorkshopId))
                    await LoadOnlineWorkshopInfoAsync(refreshed, forceRefresh: true);
                else
                    ModInfoOnlineStatus.Text = "Local metadata refreshed. No Workshop ID is available for an online metadata refresh.";
            }
            return;
        }

        AppDialog.Show("Select an installed or local MOD first.", "Refresh MOD Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SearchSelectedModOnline_Click(object sender, RoutedEventArgs e)
    {
        var name = LocalModsGrid.SelectedItem is LocalModRow local ? local.Name : ModsGrid.SelectedItem is ModRow installed ? installed.Name : string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            AppDialog.Show("Select a mod first.", "Search Online", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var query = Uri.EscapeDataString($"Palworld mod {name}");
        Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={query}") { UseShellExecute = true });
    }

    private async Task LoadOnlineWorkshopInfoAsync(LocalModRow mod, bool forceRefresh)
    {
        if (string.IsNullOrWhiteSpace(mod.WorkshopId)) return;
        try
        {
            ModInfoOnlineStatus.Text = forceRefresh ? "Refreshing Workshop information..." : "Loading Workshop information...";
            var metadata = await GetWorkshopMetadataAsync(mod.WorkshopId, forceRefresh);
            if (LocalModsGrid.SelectedItem is not LocalModRow selected || selected.WorkshopId != mod.WorkshopId) return;

            if (!string.IsNullOrWhiteSpace(metadata.Title))
            {
                var title = CleanWorkshopTitle(metadata.Title);
                ModInfoName.Text = string.IsNullOrWhiteSpace(title) ? $"Workshop Mod ({mod.WorkshopId})" : $"{title} ({mod.WorkshopId})";
            }
            if (!string.IsNullOrWhiteSpace(metadata.Author)) ModInfoAuthor.Text = metadata.Author;
            if (!string.IsNullOrWhiteSpace(metadata.Description)) ModInfoDescription.Text = metadata.Description;
            ModInfoOnlineStatus.Text = $"Steam Workshop information refreshed {metadata.FetchedUtc.ToLocalTime():g}.";
            if (!string.IsNullOrWhiteSpace(metadata.LastUpdated))
                ModInfoVersion.Text = $"Steam copy: {mod.AvailableVersion}\r\nInstalled: {mod.InstalledVersion}\r\nWorkshop updated: {metadata.LastUpdated}";
        }
        catch (Exception ex)
        {
            if (LocalModsGrid.SelectedItem is LocalModRow selected && selected.WorkshopId == mod.WorkshopId)
                ModInfoOnlineStatus.Text = "Online information could not be retrieved. Local metadata is still available. " + ex.Message;
            Log("Workshop metadata lookup failed: " + ex.Message);
        }
    }

    private async Task<WorkshopMetadata> GetWorkshopMetadataAsync(string workshopId, bool forceRefresh)
    {
        var cacheDirectory = Path.Combine(ApplicationPathService.Current.CacheRoot, "Mods");
        Directory.CreateDirectory(cacheDirectory);
        var cachePath = Path.Combine(cacheDirectory, workshopId + ".json");
        if (!forceRefresh && File.Exists(cachePath))
        {
            var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath);
            if (age < TimeSpan.FromDays(7))
            {
                var cached = JsonSerializer.Deserialize<WorkshopMetadata>(await File.ReadAllTextAsync(cachePath));
                if (cached is not null) return cached;
            }
        }

        var url = $"https://steamcommunity.com/sharedfiles/filedetails/?id={Uri.EscapeDataString(workshopId)}";
        var html = await modMetadataClient.GetStringAsync(url);
        var metadata = new WorkshopMetadata
        {
            WorkshopId = workshopId,
            Title = ExtractMeta(html, "og:title"),
            Description = ExtractMeta(html, "og:description"),
            Author = ExtractAuthor(html),
            LastUpdated = ExtractWorkshopUpdated(html),
            FetchedUtc = DateTime.UtcNow
        };
        await File.WriteAllTextAsync(cachePath, JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }));
        return metadata;
    }

    private static string ExtractMeta(string html, string property)
    {
        var pattern = $"<meta[^>]+property=[\\\"']{Regex.Escape(property)}[\\\"'][^>]+content=[\\\"'](?<value>.*?)[\\\"'][^>]*>";
        var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            pattern = $"<meta[^>]+content=[\\\"'](?<value>.*?)[\\\"'][^>]+property=[\\\"']{Regex.Escape(property)}[\\\"'][^>]*>";
            match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        }
        return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value).Trim() : string.Empty;
    }

    private static string ExtractAuthor(string html)
    {
        var match = Regex.Match(html, "<div[^>]+class=[\\\"'][^\\\"']*friendBlockContent[^\\\"']*[\\\"'][^>]*>(?<value>.*?)<", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var raw = match.Success ? Regex.Replace(match.Groups["value"].Value, "<.*?>", string.Empty) : string.Empty;
        return WebUtility.HtmlDecode(raw).Trim();
    }

    private static string ExtractWorkshopUpdated(string html)
    {
        var match = Regex.Match(html, "Updated</div>\\s*<div[^>]*>(?<value>.*?)</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return match.Success ? WebUtility.HtmlDecode(Regex.Replace(match.Groups["value"].Value, "<.*?>", string.Empty)).Trim() : string.Empty;
    }

    private sealed class WorkshopMetadata
    {
        public string WorkshopId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string LastUpdated { get; set; } = string.Empty;
        public DateTime FetchedUtc { get; set; }
    }

    private void OpenSelectedModFolder_Click(object sender, RoutedEventArgs e)
    {
        string? path = null;
        if (ModsGrid.SelectedItem is ModRow installed)
        {
            var managed = Path.Combine(settings.ManagedModsRoot, installed.Package);
            var ue4ss = Path.Combine(ue4ssRuntimeResolver.GetActiveModsRoot(), installed.Package);
            path = Directory.Exists(managed) ? managed : Directory.Exists(ue4ss) ? ue4ss : null;
        }
        else if (LocalModsGrid.SelectedItem is LocalModRow local)
            path = local.SourcePath;

        if (path is null || !Directory.Exists(path))
        {
            AppDialog.Show("The selected mod folder could not be found.", "Open Mod Folder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private void OpenSelectedWorkshop_Click(object sender, RoutedEventArgs e)
    {
        if (LocalModsGrid.SelectedItem is not LocalModRow mod || string.IsNullOrWhiteSpace(mod.WorkshopId))
        {
            AppDialog.Show("Select a local Steam Workshop mod first.", "Open Workshop", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Process.Start(new ProcessStartInfo($"https://steamcommunity.com/sharedfiles/filedetails/?id={mod.WorkshopId}") { UseShellExecute = true });
    }

    private void EnableSelectedMod_Click(object sender, RoutedEventArgs e) => SetSelectedModEnabled(true);

    private void DisableSelectedMod_Click(object sender, RoutedEventArgs e) => SetSelectedModEnabled(false);

    private void SetSelectedModEnabled(bool enabled)
    {
        if (ModsGrid.SelectedItem is not ModRow selected)
        {
            AppDialog.Show("Select an installed mod first.", enabled ? "Enable Mod" : "Disable Mod", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (server.IsRunning())
        {
            var proceed = AppDialog.Show(
                $"'{selected.Name}' will be {(enabled ? "enabled" : "disabled")} on disk, but PalServer is currently running.\n\nThe server must be restarted before the runtime state changes. Continue?",
                enabled ? "Enable Mod" : "Disable Mod",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (proceed != MessageBoxResult.Yes) return;
        }

        try
        {
            // Re-scan from disk so this explicit action is authoritative and is not
            // affected by unsaved checkbox edits elsewhere in the grid.
            var currentRows = mods.Scan().ToList();
            var target = currentRows.FirstOrDefault(row => row.Package.Equals(selected.Package, StringComparison.OrdinalIgnoreCase));
            if (target is null)
                throw new InvalidOperationException("The selected mod could not be found in the current server inventory.");

            target.Enabled = enabled;
            var result = mods.Apply(currentRows);
            // RefreshMods() also refreshes the MOD Dashboard using the current scan.
            RefreshMods();

            var action = enabled ? "enabled" : "disabled";
            Log($"Mod '{selected.Name}' {action}. {result.ChangedItemCount} runtime file/folder change(s).");
            var message = $"{selected.Name} has been {action}." +
                          (server.IsRunning() ? "\n\nRestart PalServer for the change to take effect." : "\n\nThe next server start will use this state.");
            var selectedWarnings = result.Warnings
                .Where(warning => warning.StartsWith(selected.Name + ":", StringComparison.OrdinalIgnoreCase) ||
                                  warning.StartsWith(selected.Package + ":", StringComparison.OrdinalIgnoreCase))
                .Select(warning =>
                {
                    var colon = warning.IndexOf(':');
                    return colon >= 0 ? warning[(colon + 1)..].Trim() : warning;
                })
                .ToList();
            if (selectedWarnings.Count > 0)
                message += "\n\nWarnings:\n" + string.Join("\n", selectedWarnings.Take(8));
            AppDialog.Show(message, enabled ? "Mod Enabled" : "Mod Disabled", MessageBoxButton.OK,
                selectedWarnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            Log($"Failed to {(enabled ? "enable" : "disable")} mod '{selected.Name}': {ex.Message}");
            AppDialog.Show(ex.Message, enabled ? "Enable Mod Failed" : "Disable Mod Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    private void EnableAllMods_Click(object sender, RoutedEventArgs e)
    {
        var currentRows = mods.Scan().ToList();
        if (currentRows.Count == 0)
        {
            AppDialog.Show("No installed mods were found.", "Enable All Mods", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (server.IsRunning())
        {
            var proceed = AppDialog.Show("Enable all discovered mods on disk? PalServer must be restarted before runtime state changes.", "Enable All Mods", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (proceed != MessageBoxResult.Yes) return;
        }
        foreach (var row in currentRows) row.Enabled = true;
        var result = mods.Apply(currentRows);
        modInventory.Invalidate();
        RefreshMods();
        Log($"Enabled all mods: {result.EnabledCount} enabled, {result.ChangedItemCount} runtime file/folder change(s).");
        AppDialog.Show($"All discovered mods have been enabled. Files/folders changed: {result.ChangedItemCount}." + (server.IsRunning() ? "\n\nRestart PalServer for the runtime state to change." : ""), "All Mods Enabled", MessageBoxButton.OK, result.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void DisableAllMods_Click(object sender, RoutedEventArgs e)
    {
        var currentRows = mods.Scan().ToList();
        if (currentRows.Count == 0)
        {
            AppDialog.Show("No installed mods were found.", "Disable All Mods", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = AppDialog.Show(
            $"Disable all {currentRows.Count} installed server mod(s)?\n\nWorkshop downloads and ZIP-installed files will be retained so they can be enabled again later." +
            (server.IsRunning() ? "\n\nPalServer is running. Restart the server after applying this change." : string.Empty),
            "Disable All Mods", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
            return;

        foreach (var row in currentRows)
            row.Enabled = false;

        var result = mods.Apply(currentRows);
        RefreshMods();
        Log($"Disabled all mods: {result.DisabledCount} disabled, {result.ChangedItemCount} runtime file/folder change(s).");

        var message = $"All installed mods have been disabled.\n\nFiles/folders changed: {result.ChangedItemCount}.";
        if (result.Warnings.Count > 0)
            message += "\n\nWarnings:\n" + string.Join("\n", result.Warnings.Take(8));
        if (server.IsRunning())
            message += "\n\nRestart PalServer for the runtime state to change.";
        AppDialog.Show(message, "All Mods Disabled", MessageBoxButton.OK,
            result.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void RepairModStates_Click(object sender, RoutedEventArgs e)
    {
        if (server.IsRunning())
        {
            var proceed = AppDialog.Show(
                "Repairing UE4SS activation state changes files on disk. The currently running PalServer will not change until it is restarted. Continue?",
                "Repair MOD States", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (proceed != MessageBoxResult.Yes) return;
        }

        try
        {
            var result = mods.RepairUe4ssStates();
            RefreshMods();
            Log($"Repaired UE4SS mod activation state: {result.RepairedMarkers} enabled.txt override(s) neutralized, {result.EntriesAdded} mods.txt entr{(result.EntriesAdded == 1 ? "y" : "ies")} added.");

            var message = $"UE4SS state reconciliation complete.\n\n" +
                          $"enabled.txt overrides neutralized: {result.RepairedMarkers}\n" +
                          $"mods.txt entries added: {result.EntriesAdded}\n\n" +
                          "mods.txt is now the authoritative activation source for managed UE4SS user mods.";
            if (result.Warnings.Count > 0)
                message += "\n\nWarnings:\n" + string.Join("\n", result.Warnings.Take(8));
            if (server.IsRunning())
                message += "\n\nRestart PalServer for runtime state to match the repaired configuration.";

            AppDialog.Show(message, "MOD States Repaired", MessageBoxButton.OK,
                result.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            Log($"Failed to repair UE4SS mod states: {ex.Message}");
            AppDialog.Show(ex.Message, "Repair MOD States Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyMods_Click(object sender, RoutedEventArgs e)
    {
        if (ModsGrid.ItemsSource is not IEnumerable<ModRow> rows)
            return;

        var result = mods.Apply(rows);
        RefreshMods();

        var message = $"Applied mod states.\n\nEnabled: {result.EnabledCount}\nDisabled: {result.DisabledCount}\nFiles/folders changed: {result.ChangedItemCount}";
        if (result.Warnings.Count > 0)
            message += "\n\nWarnings:\n" + string.Join("\n", result.Warnings.Take(8));
        message += "\n\nRestart the Palworld server for all changes to take effect.";

        Log($"Applied mod states: {result.EnabledCount} enabled, {result.DisabledCount} disabled, {result.ChangedItemCount} file/folder changes.");
        AppDialog.Show(message, "Mod Changes Applied", MessageBoxButton.OK,
            result.Warnings.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }

    private void DeleteMod_Click(object sender, RoutedEventArgs e)
    {
        if (ModsGrid.SelectedItem is not ModRow mod)
        {
            AppDialog.Show("Select a mod first.", "Delete Mod", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var choice = AppDialog.Show(
            $"Permanently remove '{mod.Name}' and all files recorded for this mod?\n\n" +
            "Shared files that belong to another separately installed mod are not tracked automatically. " +
            "Only continue when this is the mod you intend to remove.",
            "Delete Mod",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (choice != MessageBoxResult.Yes)
            return;

        _ = RunExclusive(_ =>
        {
            var result = mods.Delete(mod.Package);
            RefreshMods();
            Log($"Deleted mod '{mod.Name}': {result.DeletedFileCount} associated file(s) removed" +
                (result.MissingFileCount > 0 ? $", {result.MissingFileCount} already missing." : "."));
            return Task.CompletedTask;
        });
    }

    private void BrowseModZip_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select a Palworld mod package",
            Filter = "Mod packages (*.zip;*.rar;*.7z)|*.zip;*.rar;*.7z|ZIP archives (*.zip)|*.zip|RAR archives (*.rar)|*.rar|7Z archives (*.7z)|*.7z",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
            _ = InstallModZipAsync(dialog.FileName);
    }

    private Task InstallModZipAsync(string zipPath) => RunExclusive(async ct =>
    {
        Log("Checking mod package: " + zipPath);
        var preview = await Task.Run(() => mods.InspectZip(zipPath), ct);
        var overwrite = false;

        var dependencyText = preview.Dependencies.Count == 0
            ? "None detected"
            : string.Join(", ", preview.Dependencies);
        var analysisChoice = AppDialog.Show(
            $"Package Analysis\n\n" +
            $"Name: {preview.Name}\n" +
            $"Type: {preview.PackageType}\n" +
            $"Install location: {preview.InstallLocation}\n" +
            $"Dependencies: {dependencyText}\n\n" +
            "Continue with installation?",
            "Smart MOD Installer",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information,
            MessageBoxResult.Yes);

        if (analysisChoice != MessageBoxResult.Yes)
        {
            Log($"Installation cancelled after package analysis for '{preview.Name}'.");
            return;
        }

        var requiresStoppedServer = preview.PackageType.Contains("Win64 Loader", StringComparison.OrdinalIgnoreCase);
        if (requiresStoppedServer && server.IsRunning())
        {
            var stopChoice = AppDialog.Show(
                $"{preview.Name} installs native DLL files directly into the Palworld Win64 folder. PalServer must be stopped before these files can be installed safely.\n\nStop the server and continue?",
                "Stop Server for Native MOD Install",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.Yes);
            if (stopChoice != MessageBoxResult.Yes)
            {
                Log($"Installation cancelled because PalServer remained running for '{preview.Name}'.");
                return;
            }

            Log($"Stopping PalServer before installing native MOD '{preview.Name}'.");
            await server.ForceStopAsync();
            if (server.IsRunning())
                throw new InvalidOperationException("PalServer could not be stopped. The native mod installation was cancelled before any files were changed.");
        }

        if (preview.AlreadyExists)
        {
            var conflictSummary = preview.ExistingFiles.Count == 1
                ? "1 installed file will be replaced."
                : $"{preview.ExistingFiles.Count} installed files will be replaced.";

            var choice = AppDialog.Show(
                $"{preview.Name} is already installed.\n\n" +
                conflictSummary + "\n\n" +
                "MystTiq will stage and validate the new package, back up the current installation, replace it as an upgrade, preserve its enabled state, remove obsolete files, and roll back automatically if any step fails.\n\nUpgrade this mod?",
                "Upgrade Installed Mod",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.Yes);

            if (choice != MessageBoxResult.Yes)
            {
                Log($"Upgrade cancelled. Existing mod '{preview.Name}' was not changed.");
                return;
            }

            if (server.IsRunning())
            {
                var stopChoice = AppDialog.Show(
                    "PalServer is running and may have mod files locked. MystTiq must stop it before performing this upgrade.\n\nStop the server and continue?",
                    "Stop Server for MOD Upgrade",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Yes);
                if (stopChoice != MessageBoxResult.Yes)
                {
                    Log($"Upgrade cancelled because PalServer remained running for '{preview.Name}'.");
                    return;
                }

                Log($"Stopping PalServer before upgrading '{preview.Name}'.");
                await server.ForceStopAsync();
                if (server.IsRunning())
                    throw new InvalidOperationException("PalServer could not be stopped. The mod upgrade was cancelled before any files were changed.");
            }

            overwrite = true;
            Log($"Transactional upgrade approved for existing mod '{preview.Name}'.");
        }

        Log((overwrite ? "Staging mod upgrade" : "Installing mod package") + ": " + zipPath);
        var result = await Task.Run(() => mods.InstallZip(zipPath, overwrite), ct);
        RefreshMods();

        Log($"{(overwrite ? "Upgraded transactionally" : "Installed")} {result.PackageType} '{result.Name}' ({result.InstalledFileCount} files). It now appears in the Mods list.");
        if (result.SkippedFiles.Count > 0)
            Log($"Skipped {result.SkippedFiles.Count} unrecognized documentation or unsupported files.");

        AppDialog.Show(
            $"{(overwrite ? "Upgraded" : "Installed")} {result.Name}.\n\nFiles installed: {result.InstalledFileCount}\n" +
            "The mod has been added to the Mods list. Enable it if required, apply the enabled list, and restart the server.",
            overwrite ? "Mod Updated" : "Mod Installed",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    });

    private void ShowSimpleConfig_Click(object sender, RoutedEventArgs e)
    {
        ConfigGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ConfigGrid.CommitEdit(DataGridEditingUnit.Row, true);
        SimpleConfigPanel.Visibility = Visibility.Visible;
        ConfigGrid.Visibility = Visibility.Collapsed;
        SimpleConfigViewButton.Style = (Style)FindResource("ConfigViewSelectedButton");
        AdvancedConfigViewButton.Style = (Style)FindResource("ConfigViewButton");
        SyncSimpleSettingsFromRows();
        SetConfigStatus("Simple Settings view. Options here edit the same active values shown in Advanced Settings.", false);
    }

    private void ShowAdvancedConfig_Click(object sender, RoutedEventArgs e)
    {
        SimpleConfigPanel.Visibility = Visibility.Collapsed;
        ConfigGrid.Visibility = Visibility.Visible;
        SimpleConfigViewButton.Style = (Style)FindResource("ConfigViewButton");
        AdvancedConfigViewButton.Style = (Style)FindResource("ConfigViewSelectedButton");
        ConfigGrid.Items.Refresh();
        SetConfigStatus("Advanced Settings view. Amber rows differ from defaults; blue rows contain unsaved edits.", false);
    }

    private static string NormalizeConfigValue(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length >= 2 && text.StartsWith('"') && text.EndsWith('"'))
            text = text[1..^1];
        return text;
    }

    private SettingRow? FindConfigRow(string name) => configRows.FirstOrDefault(row =>
        row.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private void InitializeQolOptions()
    {
        if (qolOptions.Count > 0) return;
        void Add(string category, string label, string description, bool higherIsBetter, bool whole,
                 string displayKind, string suffix, double minimum, double maximum, double tick, params string[] names) =>
            qolOptions.Add(new QolOption
            {
                Category = category, Label = label, Description = description,
                HigherRawIsBenefit = higherIsBetter, WholeNumber = whole,
                DisplayKind = displayKind, ValueSuffix = suffix, SettingNames = names,
                Minimum = minimum, Maximum = maximum, TickFrequency = tick
            });

        Add("World", "Longer Days", "Slows daytime progression so daylight lasts longer.", false, false, "InverseBenefit", "%", 100, 190, 5, "DayTimeSpeedRate");
        Add("World", "Shorter Nights", "Speeds up nighttime progression.", true, false, "Rate", "%", 100, 400, 5, "NightTimeSpeedRate");
        Add("World", "More Item Drops", "Increases resources dropped from collection objects.", true, false, "Rate", "%", 100, 500, 10, "CollectionDropRate");
        Add("World", "Faster Resource Respawn", "Increases how quickly collection objects return.", true, false, "Rate", "%", 100, 500, 10, "CollectionObjectRespawnSpeedRate");
        Add("World", "Supply Drop Interval", "Sets the time between supply drops.", false, true, "Interval", " min", 10, 1000, 10, "SupplyDropSpan");

        Add("PlayerPal", "Player Health Regeneration", "Increases automatic player health recovery.", true, false, "Rate", "%", 100, 800, 25, "PlayerAutoHPRegeneRate");
        Add("PlayerPal", "Pal Health Regeneration", "Increases automatic Pal health recovery.", true, false, "Rate", "%", 100, 800, 25, "PalAutoHPRegeneRate");
        Add("PlayerPal", "Player Sleep Regeneration", "Increases player health recovery while sleeping.", true, false, "Rate", "%", 100, 1000, 50, "PlayerAutoHpRegeneRateInSleep");
        Add("PlayerPal", "Pal Sleep Regeneration", "Increases Pal health recovery while sleeping.", true, false, "Rate", "%", 100, 1000, 50, "PalAutoHpRegeneRateInSleep");
        Add("PlayerPal", "Reduced Player Hunger", "Reduces the player's hunger drain.", false, false, "Reduction", "%", 0, 100, 5, "PlayerStomachDecreaceRate");
        Add("PlayerPal", "Reduced Pal Hunger", "Reduces Pal hunger drain.", false, false, "Reduction", "%", 0, 100, 5, "PalStomachDecreaceRate");
        Add("PlayerPal", "Reduced Player Stamina Drain", "Reduces stamina consumption for players.", false, false, "Reduction", "%", 0, 100, 5, "PlayerStaminaDecreaceRate");
        Add("PlayerPal", "Reduced Pal Stamina Drain", "Reduces stamina consumption for Pals.", false, false, "Reduction", "%", 0, 100, 5, "PalStaminaDecreaceRate");

        Add("Gameplay", "Reduced Item Weight", "Reduces item weight so players can carry more.", false, false, "Reduction", "%", 0, 100, 5, "ItemWeightRate");
        Add("Gameplay", "Slower Food Spoilage", "Reduces item corruption/spoilage speed while keeping food management meaningful.", false, false, "Reduction", "%", 0, 100, 5, "ItemCorruptionMultiplier");
        Add("Gameplay", "Improved Equipment Durability", "Reduces durability damage taken by equipment.", false, false, "Reduction", "%", 0, 100, 5, "EquipmentDurabilityDamageRate");
        Add("Gameplay", "Faster Work", "Increases general work speed.", true, false, "Rate", "%", 100, 500, 10, "WorkSpeedRate");
        Add("Gameplay", "Faster Ranches", "Increases ranch and farm action speed.", true, false, "Rate", "%", 100, 500, 10, "MonsterFarmActionSpeedRate");

        Add("Bases", "Base Workers", "Sets the maximum number of working Pals per base.", true, true, "Count", "", 1, 100, 1, "BaseCampWorkerMaxNum");
        Add("Bases", "Guild Base Limit", "Sets the per-guild base limit.", true, true, "Count", "", 1, 20, 1, "BaseCampMaxNumInGuild");
        Add("Bases", "Structure Decay Damage", "Sets deterioration damage outside bases. Set to 0% to disable decay.", false, false, "Decay", "%", 0, 100, 5, "BuildObjectDeteriorationDamageRate");

        WorldQolItems.ItemsSource = qolOptions.Where(x => x.Category == "World");
        PlayerPalQolItems.ItemsSource = qolOptions.Where(x => x.Category == "PlayerPal");
        GameplayQolItems.ItemsSource = qolOptions.Where(x => x.Category == "Gameplay");
        BaseQolItems.ItemsSource = qolOptions.Where(x => x.Category == "Bases");
        QolSummaryItems.ItemsSource = qolSummary;
    }

    private static bool TryNumber(string? value, out double number) =>
        double.TryParse(NormalizeConfigValue(value), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out number);

    private void SyncSimpleSettingsFromRows(bool preserveEnabledSelection = false)
    {
        if (qolOptions.Count == 0) return;
        syncingSimpleSettings = true;
        try
        {
            foreach (var option in qolOptions)
            {
                var rows = option.SettingNames.Select(FindConfigRow).Where(x => x is not null).Cast<SettingRow>().ToList();
                if (rows.Count != option.SettingNames.Length || !TryNumber(rows[0].DefaultValue, out var defaultValue))
                {
                    option.IsEnabled = false;
                    option.ResultText = "Unavailable";
                    continue;
                }

                if (!TryNumber(rows[0].Value, out var activeValue)) activeValue = defaultValue;
                if (!preserveEnabledSelection)
                    option.IsEnabled = rows.Any(r => !string.Equals(NormalizeConfigValue(r.Value), NormalizeConfigValue(r.DefaultValue), StringComparison.OrdinalIgnoreCase));

                option.Percentage = option.DisplayKind switch
                {
                    "Count" or "Interval" => Math.Round(activeValue, option.WholeNumber ? 0 : 2),
                    "Decay" => defaultValue == 0 ? activeValue : Math.Round(activeValue / defaultValue * 100.0, 2),
                    "Reduction" => defaultValue == 0 ? 0 : Math.Round((1.0 - activeValue / defaultValue) * 100.0, 2),
                    "InverseBenefit" => defaultValue == 0 ? 100 : Math.Round(100.0 + (1.0 - activeValue / defaultValue) * 100.0, 2),
                    _ => defaultValue == 0 ? 100 : Math.Round(activeValue / defaultValue * 100.0, 2)
                };

                option.ResultText = option.DisplayKind switch
                {
                    "Count" when option.Label.Contains("Worker", StringComparison.OrdinalIgnoreCase) => $"{FormatQolValue(activeValue, true)} workers",
                    "Count" => $"{FormatQolValue(activeValue, true)} bases",
                    "Interval" => $"Every {FormatQolValue(activeValue, true)} minutes",
                    "Decay" when activeValue == 0 => "Disabled",
                    "Decay" => $"{FormatQolValue(option.Percentage, false)}% normal decay",
                    "Reduction" => $"{FormatQolValue(option.Percentage, false)}% less",
                    "InverseBenefit" => $"{FormatQolValue(option.Percentage, false)}% duration",
                    "Rate" => $"{FormatQolValue(option.Percentage, false)}% of default",
                    _ => $"INI value: {FormatQolValue(activeValue, option.WholeNumber)}"
                };
            }
        }
        finally { syncingSimpleSettings = false; }
        SyncSimpleDeathPenaltyFromRows();
        UpdateQolSummary();
    }

    private void SyncSimpleDeathPenaltyFromRows()
    {
        if (SimpleDeathPenaltyCombo is null) return;
        var row = FindConfigRow("DeathPenalty");
        var value = CleanConfigValue(row?.Value ?? row?.DefaultValue);
        syncingSimpleSettings = true;
        try
        {
            foreach (ComboBoxItem item in SimpleDeathPenaltyCombo.Items)
            {
                if (string.Equals(item.Tag?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    SimpleDeathPenaltyCombo.SelectedItem = item;
                    return;
                }
            }
            SimpleDeathPenaltyCombo.SelectedIndex = 1; // Item: Palworld's common default
        }
        finally { syncingSimpleSettings = false; }
    }

    private void SimpleDeathPenalty_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (syncingSimpleSettings || !IsLoaded || SimpleDeathPenaltyCombo.SelectedItem is not ComboBoxItem item) return;
        var row = FindConfigRow("DeathPenalty");
        if (row is null) return;
        row.Value = item.Tag?.ToString() ?? "Item";
        ConfigGrid.Items.Refresh();
        QolCustomProfileText.Visibility = Visibility.Visible;
        QolPresetDescriptionText.Text = "Fine-tuned settings based on your own server preferences.";
        UpdateConfigurationDirtyState();
        SetConfigStatus("Death penalty updated in the editor. Click Save Changes to write PalWorldSettings.ini.", false);
    }

    private static string FormatQolValue(double value, bool whole) => whole
        ? Math.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture)
        : value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    private void QolOption_Changed(object sender, RoutedEventArgs e)
    {
        if (syncingSimpleSettings || (sender as FrameworkElement)?.DataContext is not QolOption option) return;
        ApplyQolOption(option, markCustom: true);
    }

    private void ApplyQolOption(QolOption option, bool markCustom)
    {
        var changed = 0;
        foreach (var name in option.SettingNames)
        {
            var row = FindConfigRow(name);
            if (row is null || !TryNumber(row.DefaultValue, out var defaultValue)) continue;
            var nextValue = row.DefaultValue;
            if (option.IsEnabled)
            {
                double raw;
                if (option.DisplayKind is "Count" or "Interval")
                {
                    raw = Math.Max(0, option.Percentage);
                }
                else if (option.DisplayKind == "Decay")
                {
                    raw = Math.Max(0, defaultValue * option.Percentage / 100.0);
                }
                else if (option.DisplayKind == "Reduction")
                {
                    raw = Math.Max(0, defaultValue * (1.0 - option.Percentage / 100.0));
                }
                else if (option.DisplayKind == "InverseBenefit")
                {
                    raw = Math.Max(0.01, defaultValue * (2.0 - option.Percentage / 100.0));
                }
                else
                {
                    raw = Math.Max(0, defaultValue * option.Percentage / 100.0);
                }
                if (option.WholeNumber) raw = Math.Round(raw);
                nextValue = FormatQolValue(raw, option.WholeNumber);
            }
            if (!string.Equals(NormalizeConfigValue(row.Value), NormalizeConfigValue(nextValue), StringComparison.OrdinalIgnoreCase))
            {
                row.Value = nextValue;
                changed++;
            }
        }
        if (markCustom) MarkQolCustom();
        ConfigGrid.Items.Refresh();
        // Keep a newly checked QoL option enabled even when its current value still
        // equals the server default. This allows the user to check the box first,
        // then move the slider or edit the numeric field.
        SyncSimpleSettingsFromRows(preserveEnabledSelection: true);
        UpdateConfigurationDirtyState();
        SetConfigStatus(changed > 0
            ? $"Updated {option.Label}. Click Save Changes to write PalWorldSettings.ini."
            : option.IsEnabled
                ? $"{option.Label} is enabled. Adjust the slider or value, then click Save Changes."
                : $"{option.Label} was returned to its default value.", false);
    }

    private void ApplyQolPreset_Click(object sender, RoutedEventArgs e)
    {
        var key = (QolPresetCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            SetConfigStatus("Choose a preset before applying it.", true);
            return;
        }

        var targets = GetQolPreset(key);
        syncingSimpleSettings = true;
        try
        {
            foreach (var option in qolOptions)
            {
                var targetPairs = option.SettingNames.Where(targets.ContainsKey).Select(name => (Name: name, Value: targets[name])).ToList();
                option.IsEnabled = targetPairs.Count > 0;
                foreach (var name in option.SettingNames)
                {
                    var row = FindConfigRow(name);
                    if (row is null) continue;
                    row.Value = targets.TryGetValue(name, out var target) ? target : row.DefaultValue;
                }
            }
        }
        finally { syncingSimpleSettings = false; }
        ConfigGrid.Items.Refresh();
        SyncSimpleSettingsFromRows();
        UpdateConfigurationDirtyState();
        QolCustomProfileText.Visibility = Visibility.Collapsed;
        UpdateQolPresetDescription(key);
        var message = $"{key} QoL preset loaded. Review the values, then click Save Changes.";
        SetConfigStatus(message, false);
        Log(message);
    }

    private Dictionary<string, string> GetQolPreset(string key)
    {
        var p = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (key == "Official") return p;
        if (key == "Balanced")
        {
            p["DayTimeSpeedRate"]="0.85"; p["NightTimeSpeedRate"]="1.10";
            p["PlayerAutoHPRegeneRate"]="1.5"; p["PalAutoHPRegeneRate"]="1.5";
            p["PlayerStomachDecreaceRate"]="0.75"; p["PalStomachDecreaceRate"]="0.75";
            p["PlayerStaminaDecreaceRate"]="0.80"; p["PalStaminaDecreaceRate"]="0.80";
            p["ItemWeightRate"]="0.65"; p["ItemCorruptionMultiplier"]="0.50"; p["EquipmentDurabilityDamageRate"]="0.75";
            p["WorkSpeedRate"]="1.25"; p["MonsterFarmActionSpeedRate"]="1.25";
            p["CollectionDropRate"]="1.5"; p["CollectionObjectRespawnSpeedRate"]="1.25";
            p["BaseCampWorkerMaxNum"]="30"; p["BaseCampMaxNumInGuild"]="6";
            p["BuildObjectDeteriorationDamageRate"]="0";
            return p;
        }
        if (key == "Relaxed")
        {
            p["DayTimeSpeedRate"]="0.75"; p["NightTimeSpeedRate"]="1.25";
            p["PlayerAutoHPRegeneRate"]="2.0"; p["PalAutoHPRegeneRate"]="2.0";
            p["PlayerStomachDecreaceRate"]="0.60"; p["PalStomachDecreaceRate"]="0.60";
            p["PlayerStaminaDecreaceRate"]="0.65"; p["PalStaminaDecreaceRate"]="0.65";
            p["ItemWeightRate"]="0.50"; p["ItemCorruptionMultiplier"]="0.25"; p["EquipmentDurabilityDamageRate"]="0.60";
            p["WorkSpeedRate"]="1.50"; p["MonsterFarmActionSpeedRate"]="1.50";
            p["CollectionDropRate"]="2.0"; p["CollectionObjectRespawnSpeedRate"]="1.50";
            p["SupplyDropSpan"]="90"; p["BaseCampWorkerMaxNum"]="35";
            p["BaseCampMaxNumInGuild"]="8";
            p["BuildObjectDeteriorationDamageRate"]="0";
            return p;
        }
        return p;
    }

    private void SelectQolPreset(string key)
    {
        foreach (var item in QolPresetCombo.Items.OfType<ComboBoxItem>())
            if (string.Equals(item.Tag?.ToString(), key, StringComparison.OrdinalIgnoreCase))
            { QolPresetCombo.SelectedItem = item; QolCustomProfileText.Visibility = Visibility.Collapsed; UpdateQolPresetDescription(key); return; }
    }

    private void MarkQolCustom()
    {
        syncingSimpleSettings = true;
        try { QolPresetCombo.SelectedIndex = -1; }
        finally { syncingSimpleSettings = false; }
        QolCustomProfileText.Visibility = Visibility.Visible;
        QolPresetDescriptionText.Text = "Fine-tuned settings based on your own server preferences.";
    }

    private void QolPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncingSimpleSettings || !IsLoaded) return;
        var key = (QolPresetCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(key)) return;
        QolCustomProfileText.Visibility = Visibility.Collapsed;
        UpdateQolPresetDescription(key);
        ApplyQolPreset_Click(sender, new RoutedEventArgs());
    }

    private void UpdateQolPresetDescription(string key)
    {
        QolPresetDescriptionText.Text = key switch
        {
            "Official" => "Pocketpair's default dedicated-server configuration.",
            "Balanced" => "Small quality-of-life improvements without dramatically changing progression.",
            "Relaxed" => "A friendlier persistent-world preset with more generous rates and very slow food spoilage.",
            _ => "Choose a starting profile, then fine-tune individual options."
        };
    }

    private void QolSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded || syncingSimpleSettings || (sender as FrameworkElement)?.DataContext is not QolOption option || !option.IsEnabled) return;
        ApplyQolOption(option, markCustom: true);
    }

    private void UpdateQolSummary()
    {
        qolSummary.Clear();
        foreach (var option in qolOptions.Where(x => x.IsEnabled))
        {
            var text = option.DisplayKind switch
            {
                "Count" when option.Label.Contains("Worker", StringComparison.OrdinalIgnoreCase) => $"✓ {option.Label}: {FormatQolValue(option.Percentage, true)} workers",
                "Count" => $"✓ {option.Label}: {FormatQolValue(option.Percentage, true)} bases",
                "Interval" => $"✓ Supply drops every {FormatQolValue(option.Percentage, true)} minutes",
                "Decay" when option.Percentage == 0 => "✓ Structure decay disabled",
                "Reduction" => $"✓ {option.Label}: {FormatQolValue(option.Percentage, false)}% less",
                "InverseBenefit" => $"✓ Days last {FormatQolValue(option.Percentage, false)}% of default duration",
                "Rate" => $"✓ {option.Label}: {FormatQolValue(option.Percentage, false)}% of default",
                _ => $"✓ {option.Label}: {option.ResultText}"
            };
            qolSummary.Add(text);
        }
        if (qolSummary.Count == 0) qolSummary.Add("No QoL adjustments are currently enabled.");
    }

    private void SyncSimpleServerIdentityFromRows()
    {
        if (SimpleServerNameTextBox is null || SimpleServerDescriptionTextBox is null) return;
        syncingSimpleSettings = true;
        try
        {
            var nameRow = FindConfigRow("ServerName");
            var descriptionRow = FindConfigRow("ServerDescription");
            SimpleServerNameTextBox.Text = CleanConfigValue(nameRow?.Value ?? nameRow?.DefaultValue);
            SimpleServerDescriptionTextBox.Text = CleanConfigValue(descriptionRow?.Value ?? descriptionRow?.DefaultValue);
        }
        finally { syncingSimpleSettings = false; }
    }

    private static string GenerateRandomServerName()
    {
        string[] adjectives = { "Misty", "Golden", "Azure", "Moonlit", "Frostbound", "Wild", "Crystal", "Ember" };
        string[] nouns = { "Pal Haven", "Pal Realm", "Pal Isles", "Adventure", "Sanctuary", "Frontier", "Expedition", "World" };
        return $"{adjectives[Random.Shared.Next(adjectives.Length)]} {nouns[Random.Shared.Next(nouns.Length)]} {Random.Shared.Next(1000, 10000)}";
    }

    private void GenerateServerName_Click(object sender, RoutedEventArgs e)
    {
        SimpleServerNameTextBox.Text = GenerateRandomServerName();
        SimpleServerNameTextBox.Focus();
        SimpleServerNameTextBox.CaretIndex = SimpleServerNameTextBox.Text.Length;
    }

    private void SimpleServerIdentity_Changed(object sender, TextChangedEventArgs e)
    {
        if (syncingSimpleSettings || !IsLoaded) return;

        var nameRow = FindConfigRow("ServerName");
        var descriptionRow = FindConfigRow("ServerDescription");
        if (nameRow is not null) nameRow.Value = QuoteConfigText(SimpleServerNameTextBox.Text);
        if (descriptionRow is not null) descriptionRow.Value = QuoteConfigText(SimpleServerDescriptionTextBox.Text);

        ConfigGrid.Items.Refresh();
        RefreshDashboardServerIdentity();
        UpdateConfigurationDirtyState();
        SetConfigStatus("Server identity updated in the editor. Click Save Changes to write PalWorldSettings.ini.", false);
    }

    private static string CleanConfigValue(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length >= 2 && text.StartsWith('"') && text.EndsWith('"'))
            text = text[1..^1];
        return string.IsNullOrWhiteSpace(text) ? "—" : text;
    }

    private static string QuoteConfigText(string? value)
    {
        var clean = (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        return $"\"{clean}\"";
    }


    private bool FilterConfigRow(object item)
    {
        if (item is not SettingRow row) return false;
        var query = ConfigSearchBox?.Text?.Trim() ?? string.Empty;
        var category = (ConfigCategoryCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All Categories";
        var categoryMatch = category == "All Categories" || row.Category.Equals(category, StringComparison.OrdinalIgnoreCase);
        var textMatch = string.IsNullOrWhiteSpace(query) || row.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || row.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) || row.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
        return categoryMatch && textMatch;
    }

    private void ConfigFilter_Changed(object sender, RoutedEventArgs e) => configView?.Refresh();

    private void ExportConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export PalWorldSettings.ini", FileName = "PalWorldSettings.ini", Filter = "INI files (*.ini)|*.ini" };
            if (dialog.ShowDialog(this) != true) return;
            config.Export(dialog.FileName);
            SetConfigStatus("Configuration exported to " + dialog.FileName, false);
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void ImportConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Import PalWorldSettings.ini", Filter = "INI files (*.ini)|*.ini", CheckFileExists = true, Multiselect = false };
            if (dialog.ShowDialog(this) != true) return;
            if (server.IsRunning() && AppDialog.Show("The server is running. Importing configuration now requires a restart before it takes effect. Continue?", "Server Running", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            config.Import(dialog.FileName);
            ReloadConfig();
            SyncApiPasswordFromServerConfiguration(logChanges: true);
            SetConfigStatus("Configuration imported successfully. Restart the server to apply it.", false);
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "Import Failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void CompareConfig_Click(object sender, RoutedEventArgs e)
    {
        ConfigGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ConfigGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var changed = configRows.Where(x => x.IsDirty).ToList();
        if (changed.Count == 0) { AppDialog.Show("There are no unsaved configuration changes.", "Compare Changes", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        var details = string.Join("\n\n", changed.Take(20).Select(x => $"{x.DisplayName} ({x.Name})\nCurrent: {x.Value}\nSaved: {(x.IsDirty ? "previous active value" : x.Value)}"));
        if (changed.Count > 20) details += $"\n\n…and {changed.Count - 20} more changes.";
        AppDialog.Show(details, $"{changed.Count} Unsaved Configuration Change(s)", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void ReloadConfig_Click(object s,RoutedEventArgs e)=>ReloadConfig();

    private void ReloadConfig()
    {
        try
        {
            configRows = config.Load();
            configView = CollectionViewSource.GetDefaultView(configRows);
            configView.Filter = FilterConfigRow;
            ConfigGrid.ItemsSource = configView;
            foreach (var row in configRows)
                row.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName != nameof(SettingRow.Value)) return;

                    if (row.Name.Equals("ServerName", StringComparison.OrdinalIgnoreCase) ||
                        row.Name.Equals("ServerDescription", StringComparison.OrdinalIgnoreCase))
                    {
                        RefreshDashboardServerIdentity();
                    }

                    UpdateConfigurationDirtyState();
                    SyncSimpleSettingsFromRows();
                };
            RefreshDashboardServerIdentity();
            SyncSimpleServerIdentityFromRows();
            SyncSimpleSettingsFromRows();
            UpdateConfigurationDirtyState("Active settings loaded from PalWorldSettings.ini.");
        }
        catch(Exception ex)
        {
            Log("Configuration: " + ex.Message);
            SetConfigStatus("Load failed: " + ex.Message, true);
        }
    }


    private void LoadActive_Click(object sender, RoutedEventArgs e)
    {
        ConfigGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        ConfigGrid.CommitEdit(DataGridEditingUnit.Row, true);

        if (configRows.Any(row => row.IsDirty))
        {
            var answer = AppDialog.Show(
                "You have unsaved configuration changes.\n\nLoading active settings will discard those edits. Continue?",
                "Discard Unsaved Changes?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
            {
                SetConfigStatus("Load Active cancelled. Your unsaved edits were preserved.", false);
                return;
            }
        }

        ReloadConfig();
        Log("Active configuration reloaded from PalWorldSettings.ini.");
    }

    private void UpdateConfigurationDirtyState(string? cleanMessage = null)
    {
        if (ConfigTitleText is null || SaveConfigButton is null) return;

        var dirtyCount = configRows.Count(row => row.IsDirty);
        var isDirty = dirtyCount > 0;
        ConfigTitleText.Text = isDirty ? "Server Configuration *" : "Server Configuration";
        SaveConfigButton.Content = isDirty ? $"✓  SAVE CHANGES ({dirtyCount})" : "✓  SAVE CHANGES";

        if (isDirty)
            SetConfigStatus($"{dirtyCount} unsaved setting{(dirtyCount == 1 ? "" : "s")}. Blue rows contain edits made during this session.", false);
        else if (!string.IsNullOrWhiteSpace(cleanMessage))
            SetConfigStatus(cleanMessage, false);
    }

    private void RefreshDashboardServerIdentity()
    {
        if (ServerNameFooterText is null || ServerDescriptionFooterText is null)
            return;

        var serverName = configRows.FirstOrDefault(row =>
            row.Name.Equals("ServerName", StringComparison.OrdinalIgnoreCase));
        var serverDescription = configRows.FirstOrDefault(row =>
            row.Name.Equals("ServerDescription", StringComparison.OrdinalIgnoreCase));

        ServerNameFooterText.Text = CleanConfigValue(serverName?.Value ?? serverName?.DefaultValue);
        ServerDescriptionFooterText.Text = CleanConfigValue(serverDescription?.Value ?? serverDescription?.DefaultValue);
    }

    private void LoadDefaults_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ConfigGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ConfigGrid.CommitEdit(DataGridEditingUnit.Row, true);

            var restored = 0;
            var skipped = 0;
            foreach (var row in configRows)
            {
                if (row.DefaultValue.StartsWith("(not present", StringComparison.OrdinalIgnoreCase))
                {
                    skipped++;
                    continue;
                }

                row.Value = row.DefaultValue;
                restored++;
            }

            ConfigGrid.Items.Refresh();
            SyncSimpleSettingsFromRows();
            var message = $"Loaded default values into {restored} active settings" +
                          (skipped > 0 ? $"; {skipped} custom settings were left unchanged." : ".") +
                          " Click SAVE CHANGES to write them to PalWorldSettings.ini.";
            UpdateConfigurationDirtyState();
            SetConfigStatus(message, false);
            Log("Configuration defaults loaded into the editor. Changes are not saved yet.");
        }
        catch (Exception ex)
        {
            Log("ERROR loading configuration defaults: " + ex.Message);
            SetConfigStatus("Load defaults failed: " + ex.Message, true);
            AppDialog.Show(ex.Message, "Load Defaults Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveConfig_Click(object s, RoutedEventArgs e)
    {
        try
        {
            ConfigGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            ConfigGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var invalid = configRows.Where(row => !row.IsValid).ToList();
            if (invalid.Count > 0)
            {
                AppDialog.Show("Correct the highlighted invalid values before saving.\n\n" + string.Join("\n", invalid.Take(8).Select(x => $"• {x.DisplayName}: {x.ValidationMessage}")), "Invalid Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (server.IsRunning())
            {
                var proceed = AppDialog.Show("The Palworld server is currently running. The configuration can be saved, but most changes will not take effect until the server is restarted.\n\nSave anyway?", "Server Restart Required", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (proceed != MessageBoxResult.Yes) return;
            }

            config.Save(configRows);
            ReloadConfig();

            var message = "Save completed successfully: " + settings.ConfigFile +
                          ". Restart the Palworld server for all settings to take effect.";
            Log("Configuration saved to PalWorldSettings.ini.");
            SetConfigStatus(message, false);
            AppDialog.Show(
                "Configuration changes were saved to:\n" + settings.ConfigFile +
                "\n\nRestart the Palworld server for all settings to take effect.",
                "Configuration Saved",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            SetConfigStatus("Save failed: " + ex.Message, true);
            AppDialog.Show(ex.Message, "Configuration Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetConfigStatus(string message, bool isError)
    {
        if (ConfigStatusText is null) return;
        ConfigStatusText.Text = message;
        ConfigStatusText.Foreground = (Brush)new BrushConverter().ConvertFromString(
            isError ? "#FF6B6B" : "#7FE39A")!;
    }

    private void LoadLog_Click(object s, RoutedEventArgs e)
    {
        var path = File.Exists(sessionLog.CurrentLogPath) ? sessionLog.CurrentLogPath : ResolveLog();

        if (path is null)
        {
            AppDialog.Show(
                "No Palworld text log was found under:" + Environment.NewLine +
                settings.LogsRoot + Environment.NewLine + Environment.NewLine +
                "The manager automatically adds -log -logformat=text when starting the server.",
                "Server log not found",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        ServerLogBox.Text = string.Join(
            Environment.NewLine,
            File.ReadLines(path).TakeLast(2000));

        ServerLogBox.ScrollToEnd();
        Log("[LOG] Loaded server log: " + path);
    }
    private void RefreshConsoleView()
    {
        if (ServerLogBox is null) return;
        var severity = ConsoleSeverityFilter?.SelectedItem is ComboBoxItem severityItem
            ? severityItem.Content?.ToString() ?? "All"
            : "All";
        var category = ConsoleCategoryFilter?.SelectedItem is ComboBoxItem categoryItem
            ? categoryItem.Content?.ToString() ?? "All"
            : "All";
        var search = ConsoleSearchBox?.Text?.Trim() ?? string.Empty;
        var hideRest = HideRoutineRestCheckBox?.IsChecked == true;

        IEnumerable<string> filtered = consoleLines;
        if (!severity.Equals("All", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(line => line.Contains($"[{severity.ToUpperInvariant()}", StringComparison.OrdinalIgnoreCase));
        if (!category.Equals("All", StringComparison.OrdinalIgnoreCase))
            filtered = filtered.Where(line => line.Contains($"[{category.ToUpperInvariant()}]", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(search))
            filtered = filtered.Where(line => line.Contains(search, StringComparison.OrdinalIgnoreCase));
        if (hideRest)
            filtered = filtered.Where(line =>
                !line.Contains("[REST API]", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("REST accessed endpoint /v1/api/info OK", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("REST accessed endpoint /v1/api/players OK", StringComparison.OrdinalIgnoreCase));

        ServerLogBox.Text = string.Join(Environment.NewLine, filtered.TakeLast(4000));
        if (ConsoleAutoScrollCheckBox?.IsChecked != false)
            ServerLogBox.ScrollToEnd();
    }

    private void ConsoleFilter_Changed(object sender, RoutedEventArgs e) => RefreshConsoleView();
    private void ConsoleSearch_Changed(object sender, TextChangedEventArgs e) => RefreshConsoleView();

    private void RefreshConsole_Click(object sender, RoutedEventArgs e)
    {
        RefreshConsoleView();
        RefreshAdminCommandsRuntimeFromHistory(logResult: false);
        Log("Console view and Admin Commands runtime status refreshed.");
    }

    private void RefreshAdminCommandsStatus_Click(object sender, RoutedEventArgs e)
    {
        RefreshAdminCommandsRuntimeFromHistory(logResult: true);
    }

    private void RefreshAdminCommandsRuntimeFromHistory(bool logResult)
    {
        var priorState = adminCommandsRuntimeLoaded;
        var evidenceFound = false;
        var loaded = false;

        // Newest evidence wins. This repairs stale UI state when the Console tab is
        // revisited after the original runtime line was emitted.
        for (var index = consoleLines.Count - 1; index >= 0; index--)
        {
            var line = consoleLines[index];
            var compact = Regex.Replace(line, "[^a-zA-Z0-9]", string.Empty);
            if (!compact.Contains("admincommands", StringComparison.OrdinalIgnoreCase))
                continue;

            var success =
                line.Contains("loaded successfully", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("successfully loaded", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("initialized successfully", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("registered successfully", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("started successfully", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("entry point executed", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("hook registered", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("hooks registered", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("heartbeat", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("functionally verified", StringComparison.OrdinalIgnoreCase);
            var failure =
                line.Contains("failed to load", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("failed loading", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("load failed", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("unhandled exception", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("fatal", StringComparison.OrdinalIgnoreCase);

            if (!success && !failure)
                continue;

            evidenceFound = true;
            loaded = success && !failure;
            break;
        }

        adminCommandsRuntimeLoaded = server.IsRunning() && evidenceFound && loaded;
        UpdateAdminCommandsConsoleState();

        if (logResult)
        {
            var result = adminCommandsRuntimeLoaded
                ? "loaded runtime evidence found"
                : evidenceFound
                    ? "latest runtime evidence does not confirm a successful load"
                    : "no retained Admin Commands runtime evidence found";
            Log($"Admin Commands status refresh completed: {result}.");
        }
        else if (priorState != adminCommandsRuntimeLoaded)
        {
            Log($"Admin Commands status synchronized from retained session evidence: {(adminCommandsRuntimeLoaded ? "loaded" : "not loaded")}.");
        }
    }

    private void PauseConsole_Click(object sender, RoutedEventArgs e)
    {
        consolePaused = !consolePaused;
        PauseConsoleButton.Content = consolePaused ? "▶  RESUME" : "Ⅱ  PAUSE";
        if (!consolePaused) RefreshConsoleView();
    }

    private void ClearConsole_Click(object sender, RoutedEventArgs e)
    {
        consoleLines.Clear();
        ServerLogBox.Clear();
        Log("Console view cleared. The active session log file was not deleted.");
    }

    private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(sessionLog.CurrentLogPath)!);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{Path.GetDirectoryName(sessionLog.CurrentLogPath)}\"") { UseShellExecute = true });
    }

    private void SaveVisibleLog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export visible console output",
            Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt",
            FileName = $"MystConsole_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log"
        };
        if (dialog.ShowDialog(this) != true) return;
        File.WriteAllText(dialog.FileName, ServerLogBox.Text, new UTF8Encoding(false));
        Log("Visible console output exported to: " + dialog.FileName);
    }

    private void RestoreRconPreset()
    {
        if (RconPresetCombo is null) return;
        var desired = string.IsNullOrWhiteSpace(settings.LastRconPreset) ? "Command Library" : settings.LastRconPreset;
        foreach (var candidate in RconPresetCombo.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(candidate.Content?.ToString(), desired, StringComparison.OrdinalIgnoreCase))
            {
                RconPresetCombo.SelectedItem = candidate;
                return;
            }
        }
        RconPresetCombo.SelectedIndex = 0;
    }

    private async Task ResetRconForServerSessionAsync(string reason)
    {
        try
        {
            if (rcon.IsConnected)
                await rcon.DisconnectAsync();
        }
        catch (Exception ex)
        {
            Log($"[RCON] Session cleanup warning ({reason}): {ex.Message}");
        }

        await Dispatcher.InvokeAsync(() =>
        {
            RconStatusText.Text = "Disconnected";
            RconStatusText.Foreground = Brushes.Gold;
            DashboardHealthRcon.Text = "RCON: Reconnect on command";
        });
    }

    private async Task EnsureRconConnectedAsync(CancellationToken ct = default)
    {
        if (rcon.IsConnected) return;
        var details = ReadRconSettings();
        if (!details.Enabled)
            throw new InvalidOperationException("RCON is disabled in PalWorldSettings.ini. Set RCONEnabled=True and restart the server.");
        if (string.IsNullOrWhiteSpace(details.Password))
            throw new InvalidOperationException("AdminPassword is empty. RCON requires the server admin password.");

        RconStatusText.Text = "Connecting...";
        RconStatusText.Foreground = Brushes.Gold;
        await rcon.ConnectAsync(details.Host, details.Port, details.Password, ct);
        RconStatusText.Text = $"Connected to {details.Host}:{details.Port}";
        RconStatusText.Foreground = Brushes.LightGreen;
        Log($"RCON connected to {details.Host}:{details.Port}.");
    }

    private (string Host, int Port, string Password, bool Enabled) ReadRconSettings()
    {
        var host = "127.0.0.1";
        var port = 25575;
        var password = settings.GetPassword();
        var enabled = false;
        if (!File.Exists(settings.ConfigFile)) return (host, port, password, enabled);
        var text = File.ReadAllText(settings.ConfigFile);
        string? Match(string name)
        {
            var pattern = "(?:^|[,\\(])\\s*" + Regex.Escape(name) + "\\s*=\\s*(?:\"(?<q>[^\"]*)\"|(?<v>[^,\\)\\r\\n]+))";
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            return match.Success ? (match.Groups["q"].Success ? match.Groups["q"].Value : match.Groups["v"].Value.Trim()) : null;
        }
        enabled = bool.TryParse(Match("RCONEnabled"), out var enabledValue) && enabledValue;
        if (int.TryParse(Match("RCONPort"), out var configuredPort)) port = configuredPort;
        var adminPassword = Match("AdminPassword");
        if (!string.IsNullOrWhiteSpace(adminPassword)) password = adminPassword;
        return (host, port, password, enabled);
    }


    private string? FindWorldOptionOverride()
    {
        try
        {
            if (!Directory.Exists(settings.SaveRoot)) return null;
            return Directory.EnumerateFiles(settings.SaveRoot, "WorldOption.sav", SearchOption.AllDirectories).FirstOrDefault();
        }
        catch { return null; }
    }

    private string DisableWorldOptionOverride(string worldOverride)
    {
        if (string.IsNullOrWhiteSpace(worldOverride) || !File.Exists(worldOverride))
            throw new FileNotFoundException("WorldOption.sav was not found.", worldOverride);

        if (server.IsRunning())
            throw new InvalidOperationException("Stop PalServer before disabling WorldOption.sav. This prevents the running server from rewriting the imported override during shutdown.");

        var backup = worldOverride + ".myst-disabled-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        File.Move(worldOverride, backup);
        Log("[WORLD OVERRIDE] WorldOption.sav disabled safely: " + backup);
        return backup;
    }

    private void WorldOverrideCheck_Click(object sender, RoutedEventArgs e)
    {
        var worldOverride = FindWorldOptionOverride();
        if (worldOverride is null)
        {
            AppDialog.Show(
                "No WorldOption.sav override was found under the configured SaveGames folder. PalWorldSettings.ini should be authoritative unless another external tool is changing the server configuration.",
                "Imported World Check",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var iniPassword = config.TryReadAdminPassword();
        var message =
            "MystTiq detected WorldOption.sav in this world. Imported/downloaded Palworld worlds can use this file to override PalWorldSettings.ini.\n\n" +
            "Path: " + worldOverride + "\n" +
            "AdminPassword in PalWorldSettings.ini: " + (string.IsNullOrWhiteSpace(iniPassword) ? "EMPTY" : "SET") + "\n\n";

        if (server.IsRunning())
        {
            AppDialog.Show(
                message + "Stop PalServer, then run CHECK WORLD OVERRIDE again to safely back up and disable the override.",
                "World Override Detected",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var answer = AppDialog.Show(
            message + "Back up and disable WorldOption.sav now? The original file will be renamed, not deleted.",
            "World Override Detected",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (answer != MessageBoxResult.Yes) return;

        try
        {
            var backup = DisableWorldOptionOverride(worldOverride);
            AppDialog.Show(
                "WorldOption.sav was backed up and disabled.\n\n" + backup + "\n\nStart PalServer again so PalWorldSettings.ini becomes authoritative, then run Server Doctor / RCON Doctor to retest authentication.",
                "World Override Disabled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppDialog.Show(ex.Message, "World Override Fix Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RconDoctor_Click(object sender, RoutedEventArgs e)
    {
        var details = ReadRconSettings();
        var worldOverride = FindWorldOptionOverride();
        var report = new List<string>
        {
            $"RCON enabled: {(details.Enabled ? "YES" : "NO")}",
            $"Endpoint: {details.Host}:{details.Port}",
            $"AdminPassword in active INI: {(string.IsNullOrWhiteSpace(details.Password) ? "EMPTY" : "SET")}",
            $"WorldOption.sav override: {(worldOverride is null ? "not detected" : "DETECTED")}"
        };
        if (worldOverride is not null) report.Add("Override path: " + worldOverride);
        try
        {
            using var tcp = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await tcp.ConnectAsync(details.Host, details.Port, timeout.Token);
            report.Add("TCP reachability: PASS");
        }
        catch (Exception ex) { report.Add("TCP reachability: FAIL — " + ex.Message); }
        try
        {
            await rcon.DisconnectAsync();
            await EnsureRconConnectedAsync();
            report.Add("RCON authentication: PASS");
            await rcon.DisconnectAsync();
        }
        catch (Exception ex) { report.Add("RCON authentication: FAIL — " + ex.Message); }
        RconDoctorStatusText.Text = worldOverride is null ? "Doctor: scan complete" : "Doctor: WorldOption.sav override detected";
        RconDoctorStatusText.Foreground = worldOverride is null ? Brushes.LightGreen : Brushes.Orange;
        var message = string.Join(Environment.NewLine, report);
        if (worldOverride is not null)
        {
            if (server.IsRunning())
            {
                AppDialog.Show(
                    message + "\n\nStop PalServer before disabling WorldOption.sav. After the server is stopped, run RCON Doctor or Backups > CHECK WORLD OVERRIDE again.",
                    "RCON Doctor",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                var answer = AppDialog.Show(message + "\n\nBack up and disable WorldOption.sav now? The original file will be renamed, not deleted.", "RCON Doctor", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (answer == MessageBoxResult.Yes)
                {
                    try
                    {
                        var backup = DisableWorldOptionOverride(worldOverride);
                        Log("[RCON DOCTOR] WorldOption.sav disabled safely: " + backup);
                        AppDialog.Show("World override backed up and disabled. Start PalServer again so PalWorldSettings.ini becomes authoritative.", "World Override Disabled", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex) { AppDialog.Show(ex.Message, "World Override Fix Failed", MessageBoxButton.OK, MessageBoxImage.Error); }
                }
            }
        }
        else AppDialog.Show(message, "RCON Doctor", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private async void RconConnect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await EnsureRconConnectedAsync();
        }
        catch (Exception ex)
        {
            RconStatusText.Text = ex.Message;
            RconStatusText.Foreground = Brushes.OrangeRed;
            Log("RCON connection failed: " + ex.Message);
        }
    }

    private async void RconDisconnect_Click(object sender, RoutedEventArgs e)
    {
        await rcon.DisconnectAsync();
        RconStatusText.Text = "Disconnected";
        RconStatusText.Foreground = Brushes.Gold;
        Log("RCON disconnected.");
    }

    private async void RconSend_Click(object sender, RoutedEventArgs e)
    {
        var command = RconCommandBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(command)) return;
        try
        {
            await EnsureRconConnectedAsync();

            if (IsDangerousRconCommand(command))
            {
                var answer = AppDialog.Show($"Send this administrative command?\n\n{command}", "Confirm RCON Command", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (answer != MessageBoxResult.Yes) return;
            }

            Log("[RCON COMMAND] " + command);
            string response;
            try
            {
                response = await rcon.ExecuteAsync(command);
            }
            catch (Exception firstError) when (firstError is IOException or EndOfStreamException or SocketException or InvalidOperationException)
            {
                // Palworld can close idle RCON sockets. Reconnect once and retry transparently.
                await rcon.DisconnectAsync();
                await EnsureRconConnectedAsync();
                response = await rcon.ExecuteAsync(command);
            }

            Log("[RCON RESPONSE] " + (string.IsNullOrWhiteSpace(response) ? "Command completed with no text response." : response));
            rconHistory.Add(command);
            rconHistoryIndex = rconHistory.Count;

            if (RconPresetCombo.SelectedItem is ComboBoxItem selected && selected.Tag is not null)
            {
                var preset = selected.Tag.ToString() ?? string.Empty;
                RconCommandBox.Text = preset;
                RconCommandBox.CaretIndex = preset.Length;
            }
            else
            {
                RconCommandBox.Clear();
            }
        }
        catch (Exception ex)
        {
            RconStatusText.Text = "Disconnected / retry on send";
            RconStatusText.Foreground = Brushes.OrangeRed;
            Log("RCON command failed: " + ex.Message);
            try { await rcon.DisconnectAsync(); } catch { }
        }
    }

    private static bool IsDangerousRconCommand(string command)
    {
        var first = command.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return first.Equals("Shutdown", StringComparison.OrdinalIgnoreCase)
            || first.Equals("DoExit", StringComparison.OrdinalIgnoreCase)
            || first.Equals("KickPlayer", StringComparison.OrdinalIgnoreCase)
            || first.Equals("BanPlayer", StringComparison.OrdinalIgnoreCase);
    }

    private void RconCommandBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            RconSend_Click(sender, e);
            return;
        }
        if (e.Key == Key.Up && rconHistory.Count > 0)
        {
            rconHistoryIndex = Math.Max(0, rconHistoryIndex - 1);
            RconCommandBox.Text = rconHistory[rconHistoryIndex];
            RconCommandBox.CaretIndex = RconCommandBox.Text.Length;
            e.Handled = true;
        }
        else if (e.Key == Key.Down && rconHistory.Count > 0)
        {
            rconHistoryIndex = Math.Min(rconHistory.Count, rconHistoryIndex + 1);
            RconCommandBox.Text = rconHistoryIndex < rconHistory.Count ? rconHistory[rconHistoryIndex] : string.Empty;
            RconCommandBox.CaretIndex = RconCommandBox.Text.Length;
            e.Handled = true;
        }
    }

    private void RconPreset_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (RconPresetCombo.SelectedItem is not ComboBoxItem item) return;
        if (settings is not null)
        {
            settings.LastRconPreset = item.Content?.ToString() ?? "Command Library";
            store.Save(settings);
        }
        var command = item.Tag?.ToString();
        if (!string.IsNullOrWhiteSpace(command))
        {
            RconCommandBox.Text = command;
            RconCommandBox.CaretIndex = command.Length;
        }
    }

    private string? ResolveLog()
    {
        if (!Directory.Exists(settings.LogsRoot))
            return null;

        var preferred = Path.Combine(settings.LogsRoot, "Pal.log");

        if (File.Exists(preferred))
            return preferred;

        var palLog = Directory
            .EnumerateFiles(settings.LogsRoot, "Pal*.log", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();

        if (palLog is not null)
            return palLog;

        return Directory
            .EnumerateFiles(settings.LogsRoot, "*.log", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    private void StartSessionLogTail()
    {
        var generation = Interlocked.Increment(ref logTailGeneration);
        var previous = logTailCts;
        try { previous?.Cancel(); } catch { }
        try { previous?.Dispose(); } catch { }

        var cts = new CancellationTokenSource();
        logTailCts = cts;
        var task = TailLogAsync(generation, cts.Token);
        logTailTask = task;
        ObserveTask(task, $"Pal.log tail session {generation}");
    }

    private void StopSessionLogTail()
    {
        ObserveTask(StopSessionLogTailAsync(TimeSpan.FromSeconds(1)), "Pal.log reader cleanup");
    }

    private async Task StopSessionLogTailAsync(TimeSpan timeout)
    {
        var cts = logTailCts;
        var task = logTailTask;
        logTailCts = null;
        logTailTask = null;
        try { cts?.Cancel(); } catch { }
        if (task is not null)
        {
            try
            {
                var completed = await Task.WhenAny(task, Task.Delay(timeout));
                if (!ReferenceEquals(completed, task))
                    Log($"[LOG] Pal.log reader did not stop within {timeout.TotalSeconds:0.#}s; cleanup will continue without blocking the UI.");
                else
                    await task;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Log("[LOG] Pal.log reader cleanup warning: " + ex.Message); }
        }
        try { cts?.Dispose(); } catch { }
        UpdateServerIoCounters();
    }

    private async Task TailLogAsync(long generation, CancellationToken ct)
    {
        Interlocked.Increment(ref activePalLogReaders);
        UpdateServerIoCounters();
        Log($"[LOG] Pal.log reader #{generation} started.");
        long position = 0;
        string? activePath = null;

        try
        {
        while (!ct.IsCancellationRequested && generation == Volatile.Read(ref logTailGeneration))
        {
            try
            {
                var path = ResolveLog();

                if (path is null)
                {
                    // Log the waiting state once. Repeating the same message every
                    // 30 seconds added console noise without providing new state.
                    if (!logTailWaitingMessageShown)
                    {
                        logTailWaitingMessageShown = true;
                        lastLogTailWarningUtc = DateTime.UtcNow;
                        Log(
                            "[LOG] Waiting for a Palworld text log under: " +
                            settings.LogsRoot);
                    }

                    await Task.Delay(1000, ct);
                    continue;
                }

                if (!string.Equals(
                        path,
                        activePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    logTailWaitingMessageShown = false;
                    activePath = path;
                    currentServerLogPath = path;

                    var file = new FileInfo(path);

                    // Read a small recent history when attaching, then follow new
                    // content from that exact byte position.
                    position = Math.Max(0, file.Length - 120_000);

                    Log("[LOG] Following server log: " + path);
                }

                await using var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                if (position > stream.Length)
                    position = 0;

                stream.Seek(position, SeekOrigin.Begin);

                using var reader = new StreamReader(stream);
                string? line;

                while ((line = await reader.ReadLineAsync(ct)) is not null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var normalized = NormalizeServerOutput(line);
                    if (string.IsNullOrWhiteSpace(normalized)) continue;
                    ObserveExplicitModLoad(normalized);
                    ObserveAdminCommandsRuntime(normalized);
                    Log("[PAL.LOG] " + normalized);
                }

                position = stream.Position;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                if (DateTime.UtcNow - lastLogTailWarningUtc > TimeSpan.FromSeconds(15))
                {
                    lastLogTailWarningUtc = DateTime.UtcNow;
                    Log("[LOG WARNING] " + exception.Message);
                }
            }

            await Task.Delay(700, ct);
        }
        }
        finally
        {
            Interlocked.Decrement(ref activePalLogReaders);
            UpdateServerIoCounters();
            Log($"[LOG] Pal.log reader #{generation} stopped.");
        }
    }


    private void InitializeSetupPasswordDefaults()
    {
        // New server creation is intentionally zero-prompt: use a generated identity
        // only to create the initial world. The user can rename/configure it later.
        if (string.IsNullOrWhiteSpace(SetupServerNameBox.Text) || SetupServerNameBox.Text.Equals("My Palworld Server", StringComparison.OrdinalIgnoreCase))
            SetupServerNameBox.Text = GenerateRandomServerName();
        if (string.IsNullOrWhiteSpace(SetupDescriptionBox.Text))
            SetupDescriptionBox.Text = "A QoL Palworld server managed by MystTiq";
        var existingPassword = settings.GetPassword();
        SetupAdminPasswordBox.Password = string.IsNullOrWhiteSpace(existingPassword)
            ? InstallerService.GenerateSecureAdminPassword()
            : existingPassword;
    }

    private bool SyncApiPasswordFromServerConfiguration(bool logChanges = false)
    {
        try
        {
            var password = config.TryReadAdminPassword();
            if (string.IsNullOrWhiteSpace(password))
                return false;

            var changed = !string.Equals(settings.GetPassword(), password, StringComparison.Ordinal);
            settings.SetPassword(password);
            store.Save(settings);

            if (ApiPasswordBox is not null && ApiPasswordVisibleBox is not null)
            {
                syncingPasswordFields = true;
                ApiPasswordBox.Password = password;
                ApiPasswordVisibleBox.Text = password;
                syncingPasswordFields = false;
            }

            if (changed && logChanges)
                Log("Synchronized the REST API password from PalWorldSettings.ini.");

            return true;
        }
        catch (Exception exception)
        {
            if (logChanges)
                Log("Could not synchronize AdminPassword: " + exception.Message);
            return false;
        }
    }

    private void LoadSettings()
    {
        ServerRootBox.Text = settings.ServerRoot;
        SteamCmdBox.Text = settings.SteamCmdPath;
        BackupRootBox.Text = settings.BackupRoot;
        ApiUrlBox.Text = settings.ApiBaseUrl;
        ApiUserBox.Text = settings.ApiUser;

        syncingPasswordFields = true;
        var password = settings.GetPassword();
        ApiPasswordBox.Password = password;
        ApiPasswordVisibleBox.Text = password;
        syncingPasswordFields = false;

        LaunchArgsBox.Text = settings.LaunchArguments;
    }

    private void SaveSettings_Click(object s, RoutedEventArgs e)
    {
        try
        {
            SettingsStatusText.Foreground = new SolidColorBrush(Color.FromRgb(159, 196, 234));
            SettingsStatusText.Text = "Saving manager settings...";

            settings.ServerRoot = ServerRootBox.Text.Trim();
            settings.SteamCmdPath = SteamCmdBox.Text.Trim();
            settings.BackupRoot = BackupRootBox.Text.Trim();
            settings.ApiBaseUrl = ApiUrlBox.Text.Trim();
            settings.ApiUser = ApiUserBox.Text.Trim();

            var password = PasswordVisibilityToggle.IsChecked == true
                ? ApiPasswordVisibleBox.Text
                : ApiPasswordBox.Password;

            settings.SetPassword(password);
            settings.LaunchArguments = LaunchArgsBox.Text.Trim();

            store.Save(settings);

            SettingsStatusText.Foreground = new SolidColorBrush(Color.FromRgb(86, 217, 135));
            SettingsStatusText.Text = "Settings saved successfully. Restart the manager after changing server paths.";
            Log("[SETTINGS] Settings saved successfully. Restart the manager after changing paths.");
        }
        catch (Exception exception)
        {
            SettingsStatusText.Foreground = new SolidColorBrush(Color.FromRgb(240, 91, 87));
            SettingsStatusText.Text = "Save failed: " + exception.Message;
            Log("[SETTINGS ERROR] Save failed: " + exception.Message);

            AppDialog.Show(
                "The manager settings could not be saved." + Environment.NewLine + Environment.NewLine + exception.Message,
                "Settings Save Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void BrowseServerRoot_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select the Palworld dedicated server folder",
            InitialDirectory = Directory.Exists(ServerRootBox.Text)
                ? ServerRootBox.Text
                : settings.ServerRoot,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
            ServerRootBox.Text = dialog.FolderName;
    }

    private void BrowseBackupRoot_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select the Palworld backup folder",
            InitialDirectory = Directory.Exists(BackupRootBox.Text)
                ? BackupRootBox.Text
                : settings.BackupRoot,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
            BackupRootBox.Text = dialog.FolderName;
    }

    private void BrowseSteamCmd_Click(object sender, RoutedEventArgs e)
    {
        var currentDirectory = Path.GetDirectoryName(SteamCmdBox.Text);

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select steamcmd.exe",
            Filter = "SteamCMD executable (steamcmd.exe)|steamcmd.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = !string.IsNullOrWhiteSpace(currentDirectory) &&
                               Directory.Exists(currentDirectory)
                ? currentDirectory
                : Path.GetDirectoryName(settings.SteamCmdPath)
        };

        if (dialog.ShowDialog(this) == true)
            SteamCmdBox.Text = dialog.FileName;
    }

    private void PasswordVisibilityToggle_Checked(object sender, RoutedEventArgs e)
    {
        syncingPasswordFields = true;
        ApiPasswordVisibleBox.Text = ApiPasswordBox.Password;
        ApiPasswordBox.Visibility = Visibility.Collapsed;
        ApiPasswordVisibleBox.Visibility = Visibility.Visible;
        ApiPasswordVisibleBox.Focus();
        ApiPasswordVisibleBox.CaretIndex = ApiPasswordVisibleBox.Text.Length;
        syncingPasswordFields = false;
    }

    private void PasswordVisibilityToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        syncingPasswordFields = true;
        ApiPasswordBox.Password = ApiPasswordVisibleBox.Text;
        ApiPasswordVisibleBox.Visibility = Visibility.Collapsed;
        ApiPasswordBox.Visibility = Visibility.Visible;
        ApiPasswordBox.Focus();
        syncingPasswordFields = false;
    }

    private void ApiPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (syncingPasswordFields)
            return;

        syncingPasswordFields = true;
        ApiPasswordVisibleBox.Text = ApiPasswordBox.Password;
        syncingPasswordFields = false;
    }

    private void ApiPasswordVisibleBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (syncingPasswordFields)
            return;

        syncingPasswordFields = true;
        ApiPasswordBox.Password = ApiPasswordVisibleBox.Text;
        syncingPasswordFields = false;
    }
    private void RefreshEnvironment()
    {
        environmentRows = environment.Scan();
        EnvironmentGrid.ItemsSource = environmentRows;

        var readyCount = environmentRows.Count(row => row.Status == "READY");
        var attentionCount = environmentRows.Count - readyCount;
        EnvironmentHealthText.Text = attentionCount == 0
            ? $"{readyCount} / {environmentRows.Count} READY"
            : $"{readyCount} / {environmentRows.Count} READY · {attentionCount} NEED ATTENTION";
        EnvironmentHealthText.Foreground = attentionCount == 0
            ? new SolidColorBrush(Color.FromRgb(78, 211, 132))
            : new SolidColorBrush(Color.FromRgb(255, 194, 71));

        // First-time setup should be visible automatically. Existing servers keep the section collapsed.
        NewServerSettingsExpander.IsExpanded = !File.Exists(settings.ServerExe);
    }

    private void SetSetupOperationState(string state, string title, string detail, double percent, bool completed = false, bool failed = false)
    {
        InstallStateText.Text = state.ToUpperInvariant();
        InstallOperationTitleText.Text = title;
        InstallStatusText.Text = detail;
        InstallProgressBar.Value = Math.Clamp(percent, 0, 100);
        InstallPercentText.Text = $"{Math.Clamp(percent, 0, 100):0}%";

        InstallStateBadge.Background = failed
            ? new SolidColorBrush(Color.FromRgb(181, 59, 59))
            : completed
                ? new SolidColorBrush(Color.FromRgb(31, 139, 76))
                : state.Equals("IDLE", StringComparison.OrdinalIgnoreCase)
                    ? new SolidColorBrush(Color.FromRgb(70, 85, 104))
                    : new SolidColorBrush(Color.FromRgb(41, 116, 185));

        if (completed || failed)
            InstallRecentActivityText.Text = $"Recent activity: {title} · {(failed ? "Failed" : "Completed")} at {DateTime.Now:t}.";
    }

    private void ScanEnvironment_Click(object sender, RoutedEventArgs e) => RefreshEnvironment();
    private void ScanLocalMods_Click(object sender, RoutedEventArgs e)
    {
        localModRows = environment.ScanLocalMods();
        LocalModsGrid.ItemsSource = localModRows;
        Log($"Local Workshop scan completed: {localModRows.Count} mod(s) found.");
    }

    private async void RefreshLocalSteamMods_Click(object sender, RoutedEventArgs e)
    {
        // Bind one stable set of rows, then force-refresh Workshop metadata for those
        // exact objects. The previous implementation launched title enrichment and
        // immediately replaced the grid with a second scan, so resolved names were
        // written to rows that were no longer visible.
        var installed = mods.Scan();
        ModsGrid.ItemsSource = installed;
        localModRows = environment.ScanLocalMods();
        LocalModsGrid.ItemsSource = localModRows;
        RefreshModDashboard(installed);

        await RefreshWorkshopDisplayNamesAsync(installed, localModRows, forceRefresh: true);
        var titled = localModRows.Count(row => !string.IsNullOrWhiteSpace(row.Name) &&
            !row.Name.Equals(row.WorkshopId, StringComparison.OrdinalIgnoreCase) &&
            !row.Name.StartsWith("Workshop Mod ", StringComparison.OrdinalIgnoreCase));
        Log($"Local Steam MOD refresh completed: {localModRows.Count} local mod(s) found; {titled} title(s) resolved; managed server MOD state refreshed.");
    }


    private void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        SetSetupOperationState("RUNNING", "Checking for updates", "Comparing installed components and local Workshop content.", 20);
        RefreshEnvironment();
        RefreshMods();
        var workshopUpdates = localModRows.Count(row => row.UpdateStatus == "UPDATE AVAILABLE");
        var workshopMissing = localModRows.Count(row => row.UpdateStatus == "NOT INSTALLED");
        var serverReady = File.Exists(settings.ServerExe);
        var steamReady = File.Exists(settings.SteamCmdPath);
        var ue4ss = environment.VerifyComponent("UE4SS Runtime");
        SetSetupOperationState("COMPLETE", "Update check complete", $"Steam Workshop updates: {workshopUpdates}; available but not installed: {workshopMissing}.", 100, completed: true);
        AppDialog.Show(
            $"SteamCMD: {(steamReady ? "Installed" : "Missing")}\n" +
            $"Palworld Dedicated Server: {(serverReady ? "Installed" : "Missing")}\n" +
            $"UE4SS: {(ue4ss.Success ? "Ready" : "Needs attention")}\n" +
            $"Workshop mod updates: {workshopUpdates}\n" +
            $"Workshop mods not installed: {workshopMissing}\n\n" +
            "Steam Workshop comparison uses the newest local Steam copy and the server import timestamp.",
            "Check for Updates", MessageBoxButton.OK, workshopUpdates > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
    }

    private async void RefreshUpdateCenter_Click(object sender, RoutedEventArgs e)
    {
        UpdateCheckAllButton.Content = "…  CHECKING";
        UpdateCheckAllButton.IsEnabled = false;
        UpdateCenterCheckedBadge.Text = "CHECKING";
        UpdateCenterCheckedBadge.Foreground = new SolidColorBrush(Color.FromRgb(100, 181, 246));
        UpdateCenterSummaryText.Text = "Checking installed components, Steam build information, GitHub UE4SS releases, and local Workshop updates...";
        var visibleRows = BuildLocalUpdateCenterRows();
        foreach (var row in visibleRows)
        {
            row.Status = "CHECKING";
            row.Action = "CHECKING";
            row.LastChecked = DateTime.Now.ToString("g");
        }
        BindUpdateCenterRows(visibleRows);

        try
        {
            var rows = await BuildUpdateCenterRowsAsync();
            ApplyUpdateCenterRows(rows);
            UpdateCheckAllButton.Content = "✓  CHECK COMPLETE";
        }
        catch (Exception ex)
        {
            UpdateCenterSummaryText.Text = "Update check failed: " + ex.Message;
            UpdateCenterCheckedBadge.Text = "CHECK FAILED";
            UpdateCenterCheckedBadge.Foreground = Brushes.IndianRed;
            Log("Update Center check failed: " + ex.Message);
        }
        finally
        {
            UpdateCheckAllButton.IsEnabled = true;
        }
    }

    private void RefreshUpdateCenter()
    {
        var rows = BuildLocalUpdateCenterRows();
        var restored = RestoreUpdateInventoryMetadata(rows);
        BindUpdateCenterRows(rows);
        if (!restored)
        {
            UpdateCenterSummaryText.Text = "Select Check All to compare installed components against Steam/GitHub and local Workshop sources.";
            UpdateCenterCheckedBadge.Text = "NOT CHECKED";
            UpdateCenterCheckedBadge.Foreground = new SolidColorBrush(Color.FromRgb(144, 165, 188));
            UpdateAllAvailableButton.IsEnabled = false;
        }
        else
        {
            UpdateCenterSummaryText.Text = "Loaded the most recent saved update check. Run Check All to refresh online information.";
            UpdateCenterCheckedBadge.Text = rows.Any(r => r.Status == "UPDATE AVAILABLE") ? "UPDATES AVAILABLE" : rows.Any(r => r.Status == "CHECK FAILED") ? "CHECK FAILED" : rows.Any(r => r.Status is "MANUAL CHECK REQUIRED" or "UNABLE TO CHECK") ? "CHECK COMPLETE — REVIEW" : "SAVED RESULTS";
            UpdateAllAvailableButton.IsEnabled = rows.Any(r => r.Status == "UPDATE AVAILABLE");
        }
    }

    private List<UpdateCenterRow> BuildLocalUpdateCenterRows()
    {
        var rows = new List<UpdateCenterRow>();
        var pythonPath = settings.PythonExecutable;
        var pythonVersion = GetProcessVersion(pythonPath, "--version");
        rows.Add(new UpdateCenterRow { Group = "Save & Runtime Dependencies", Component = "Python Runtime", Installed = pythonVersion ?? "Not installed", Available = "Check Python.org", Status = pythonVersion is null ? "NOT INSTALLED" : "NOT CHECKED", Source = "Python.org", Recommendation = pythonVersion is null ? "Install Python from Server Setup." : "Check the official Python release when internet checking is requested." });
        var pipVersion = GetProcessVersion(string.IsNullOrWhiteSpace(pythonPath) ? "python" : pythonPath, "-m pip --version");
        rows.Add(new UpdateCenterRow { Group = "Save & Runtime Dependencies", Component = "pip", Installed = pipVersion ?? "Not installed", Available = "Check PyPI", Status = pipVersion is null ? "NOT INSTALLED" : "NOT CHECKED", Source = "PyPI", Recommendation = pipVersion is null ? "Repair Python to restore pip." : "Managed with the selected Python runtime." });
        AddFileBackedUpdateRow(rows, "Save & Runtime Dependencies", "Palworld Save Tools", Path.Combine(settings.ServerRoot, "Tools", "palworld-save-tools", "convert.py"), "cheahjs/palworld-save-tools");
        AddFileBackedUpdateRow(rows, "Save & Runtime Dependencies", "PlM/Oodle Decoder", Path.Combine(settings.ServerRoot, "Tools", "palworld-plm-tools", ".myst-install.json"), "deafdudecomputers/PalworldSaveTools + pyooz");
        rows.Add(new UpdateCenterRow { Group = "Core Server", Component = "MystTiq Server Manager", Installed = typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "Unknown", Available = "Check release source", Status = "NOT CHECKED", Source = "MystTiq", Recommendation = "Use the current approved MystTiq release package." });
        rows.Add(new UpdateCenterRow { Group = "Save & Runtime Dependencies", Component = ".NET Runtime", Installed = Environment.Version.ToString(), Available = "Check Microsoft", Status = "NOT CHECKED", Source = "Microsoft", Recommendation = "The manager is currently running on this .NET runtime." });
        rows.Add(new UpdateCenterRow { Group = "Save & Runtime Dependencies", Component = "Visual C++ Runtime", Installed = "Detected by Windows", Available = "Check Microsoft", Status = "NOT CHECKED", Source = "Microsoft", Recommendation = "Required by PalServer and native runtime components." });
        var cppTools = environment.VerifyComponent("Microsoft C++ Build Tools");
        rows.Add(new UpdateCenterRow { Group = "Save & Runtime Dependencies", Component = "Microsoft C++ Build Tools", Installed = cppTools.Success ? "Installed" : "Not installed", Available = "Visual Studio 2022 Build Tools", Status = cppTools.Success ? "CURRENT" : "NOT INSTALLED", Source = "Microsoft", Recommendation = cppTools.Success ? "MSVC compiler is available for pyooz and other native dependencies." : "Install from Server Setup before installing the PlM/Oodle decoder." });
        var steamInstalled = File.Exists(settings.SteamCmdPath);
        rows.Add(new UpdateCenterRow
        {
            Group = "Core Server", Component = "SteamCMD",
            Installed = steamInstalled ? "Installed" : "Not installed",
            Available = steamInstalled ? "Self-updating" : "Available",
            Status = steamInstalled ? "UP TO DATE" : "NOT INSTALLED",
            Source = "Valve / SteamCMD",
            Recommendation = steamInstalled ? "SteamCMD self-updates when launched." : "Install SteamCMD from Server Setup."
        });

        var serverInstalled = File.Exists(settings.ServerExe);
        var installedBuild = GetInstalledPalworldBuildId();
        rows.Add(new UpdateCenterRow
        {
            Group = "Core Server", Component = "Palworld Dedicated Server",
            Installed = serverInstalled ? (installedBuild is null ? GetFileVersionSafe(settings.ServerExe) : "Build " + installedBuild) : "Not installed",
            Available = serverInstalled ? "Check Steam" : "—",
            Status = serverInstalled ? "NOT CHECKED" : "NOT INSTALLED",
            Source = "Steam app 2394010",
            Recommendation = serverInstalled ? "Click Check All to compare the installed build with Steam." : "Install the dedicated server first."
        });

        var ue4ssState = environment.GetUe4ssRuntimeState();
        var identity = environment.GetUe4ssRuntimeIdentity();
        rows.Add(new UpdateCenterRow
        {
            Group = "Core Server", Component = "UE4SS Runtime",
            Installed = ue4ssState.Installed ? identity.Version : "Not installed",
            Available = "Check GitHub",
            Status = ue4ssState.Installed ? "NOT CHECKED" : "NOT INSTALLED",
            Source = "Okaetsu/RE-UE4SS",
            Recommendation = ue4ssState.Installed ? "Click Check All to compare the runtime timestamp with the latest Palworld release." : "Install UE4SS from Server Setup or MOD Runtime."
        });

        localModRows = environment.ScanLocalMods();
        foreach (var mod in localModRows.Where(m => m.ServerStatus != "Not installed" || m.UpdateStatus == "UPDATE AVAILABLE"))
        {
            var status = mod.UpdateStatus.Equals("UPDATE AVAILABLE", StringComparison.OrdinalIgnoreCase) ? "UPDATE AVAILABLE" :
                         mod.ServerStatus.Equals("Not installed", StringComparison.OrdinalIgnoreCase) ? "NOT INSTALLED" : "UP TO DATE";
            rows.Add(new UpdateCenterRow
            {
                Group = "Installed MODs",
                Component = mod.Name,
                Installed = mod.InstalledVersion,
                Available = mod.AvailableVersion,
                Status = status,
                Source = string.IsNullOrWhiteSpace(mod.WorkshopId) ? "Local MOD" : $"Workshop {mod.WorkshopId}",
                Recommendation = status == "UPDATE AVAILABLE" ? "A newer local Workshop copy is available; review it in MOD Library." : status == "UP TO DATE" ? "No newer local Workshop copy detected." : "The Workshop package is available locally but is not installed on the server."
            });
        }
        return rows;
    }

    private async Task<List<UpdateCenterRow>> BuildUpdateCenterRowsAsync()
    {
        var rows = BuildLocalUpdateCenterRows();

        var serverRow = rows.First(r => r.Component == "Palworld Dedicated Server");
        if (File.Exists(settings.ServerExe))
        {
            try
            {
                var installed = GetInstalledPalworldBuildId();
                var latest = await QueryLatestPalworldBuildIdAsync();
                serverRow.Installed = installed is null ? GetFileVersionSafe(settings.ServerExe) : "Build " + installed;
                serverRow.Available = latest is null ? "Unknown" : "Build " + latest;
                if (installed is null || latest is null)
                {
                    serverRow.Status = "CHECK FAILED";
                    serverRow.Recommendation = "Steam build information could not be compared. Use Update Server to validate the installation.";
                }
                else if (installed == latest)
                {
                    serverRow.Status = "UP TO DATE";
                    serverRow.Recommendation = "Installed Steam build matches the current public build.";
                }
                else
                {
                    serverRow.Status = "UPDATE AVAILABLE";
                    serverRow.Recommendation = "A newer public Palworld Dedicated Server build is available through SteamCMD.";
                }
            }
            catch (Exception ex)
            {
                serverRow.Status = "CHECK FAILED";
                serverRow.Available = "Check failed";
                serverRow.Recommendation = ex.Message;
            }
        }

        var ue4ssRow = rows.First(r => r.Component == "UE4SS Runtime");
        if (environment.GetUe4ssRuntimeState().Installed)
        {
            try
            {
                var source = (Ue4ssSourceCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Palworld Fork";
            var releases = await ue4ssReleases.GetReleasesAsync(source);
                var latest = releases.OrderByDescending(r => r.PublishedAt).FirstOrDefault();
                var installedIdentity = environment.GetUe4ssRuntimeIdentity();
                var runtimeWrite = environment.GetUe4ssRuntimeLastWriteUtc();
                ue4ssRow.Installed = installedIdentity.Version;
                ue4ssRow.Available = latest is null ? "No GitHub release" : latest.Display;
                if (latest is null || latest.PublishedAt == DateTime.MinValue || runtimeWrite is null)
                {
                    ue4ssRow.Status = "CHECK FAILED";
                    ue4ssRow.Recommendation = "Could not compare the installed runtime with GitHub release metadata. Open MOD Runtime for manual verification.";
                }
                else if (runtimeWrite.Value >= latest.PublishedAt.ToUniversalTime().AddMinutes(-5))
                {
                    ue4ssRow.Status = "UP TO DATE";
                    ue4ssRow.Recommendation = "Installed runtime files are at least as new as the latest published Palworld RE-UE4SS release.";
                }
                else
                {
                    ue4ssRow.Status = "UPDATE AVAILABLE";
                    ue4ssRow.Recommendation = "A newer Palworld RE-UE4SS release is available. Open MOD Runtime to review/install it.";
                }
            }
            catch (Exception ex)
            {
                ue4ssRow.Status = "CHECK FAILED";
                ue4ssRow.Available = "Check failed";
                ue4ssRow.Recommendation = ex.Message;
            }
        }

        await ApplyInstallerAwareUpdateChecksAsync(rows);
        FinalizeUpdateCheckResults(rows);
        return rows;
    }

    private async Task ApplyInstallerAwareUpdateChecksAsync(List<UpdateCenterRow> rows)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(ApplicationVersion.UserAgent);

        await CheckPythonRuntimeAsync(rows.First(r => r.Component == "Python Runtime"), client);
        await CheckPipAsync(rows.First(r => r.Component == "pip"), client);
        await CheckGitHubReleaseComponentAsync(rows.First(r => r.Component == "Palworld Save Tools"), client,
            "https://api.github.com/repos/cheahjs/palworld-save-tools/releases/latest",
            Path.Combine(settings.ServerRoot, "Tools", "palworld-save-tools", ".myst-install.json"));
        await CheckGitHubCommitComponentAsync(rows.First(r => r.Component == "PlM/Oodle Decoder"), client,
            "https://api.github.com/repos/deafdudecomputers/PalworldSaveTools/commits/master",
            Path.Combine(settings.ServerRoot, "Tools", "palworld-plm-tools", ".myst-install.json"));
        await CheckDotNetRuntimeAsync(rows.First(r => r.Component == ".NET Runtime"), client);

        var manager = rows.First(r => r.Component == "MystTiq Server Manager");
        manager.Status = "RELEASE SOURCE NOT CONFIGURED";
        manager.Available = "No release catalog";
        manager.Recommendation = "Configure a MystTiq release catalog before automatic manager updates can be checked.";
    }

    private static async Task CheckPythonRuntimeAsync(UpdateCenterRow row, HttpClient client)
    {
        if (row.Installed.StartsWith("Not installed", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            var match = Regex.Match(row.Installed, @"(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)");
            if (!match.Success) throw new InvalidOperationException("Installed Python version could not be parsed.");
            var major = match.Groups["major"].Value;
            var minor = match.Groups["minor"].Value;
            var installed = Version.Parse(match.Value);
            var html = await client.GetStringAsync("https://www.python.org/ftp/python/");
            var candidates = Regex.Matches(html, $"href=[\"'](?<v>{Regex.Escape(major)}\\.{Regex.Escape(minor)}\\.\\d+)/[\"']", RegexOptions.IgnoreCase)
                .Select(m => Version.TryParse(m.Groups["v"].Value, out var v) ? v : null)
                .Where(v => v is not null).Cast<Version>().OrderByDescending(v => v).ToList();
            var latest = candidates.FirstOrDefault() ?? throw new InvalidOperationException("No compatible Python release was returned.");
            row.Available = latest.ToString();
            row.Status = latest > installed ? "UPDATE AVAILABLE" : "UP TO DATE";
            row.Recommendation = latest > installed
                ? $"Python {latest} is available in the installed {major}.{minor} compatibility channel."
                : $"Python {installed} is current for the {major}.{minor} compatibility channel.";
        }
        catch (Exception ex)
        {
            row.Status = "UNABLE TO CHECK";
            row.Available = "Check unavailable";
            row.Recommendation = "Python.org version comparison failed: " + ex.Message;
        }
    }

    private static async Task CheckPipAsync(UpdateCenterRow row, HttpClient client)
    {
        if (row.Installed.StartsWith("Not installed", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            using var doc = JsonDocument.Parse(await client.GetStringAsync("https://pypi.org/pypi/pip/json"));
            var latestText = doc.RootElement.GetProperty("info").GetProperty("version").GetString() ?? throw new InvalidOperationException("PyPI returned no version.");
            var installedMatch = Regex.Match(row.Installed, @"(?<v>\d+(?:\.\d+){1,3})");
            if (!Version.TryParse(installedMatch.Groups["v"].Value, out var installed) || !Version.TryParse(latestText, out var latest))
                throw new InvalidOperationException("pip version could not be parsed.");
            row.Available = latestText;
            row.Status = latest > installed ? "UPDATE AVAILABLE" : "UP TO DATE";
            row.Recommendation = latest > installed ? $"pip {latestText} is available." : $"pip {installed} is current.";
        }
        catch (Exception ex)
        {
            row.Status = "UNABLE TO CHECK";
            row.Available = "Check unavailable";
            row.Recommendation = "PyPI version comparison failed: " + ex.Message;
        }
    }

    private static async Task CheckGitHubReleaseComponentAsync(UpdateCenterRow row, HttpClient client, string apiUrl, string manifestPath)
    {
        if (!File.Exists(manifestPath)) return;
        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var installed = manifest.RootElement.TryGetProperty("version", out var value) ? value.GetString() : null;
            using var release = JsonDocument.Parse(await client.GetStringAsync(apiUrl));
            var latest = release.RootElement.GetProperty("tag_name").GetString() ?? throw new InvalidOperationException("GitHub returned no release tag.");
            row.Installed = string.IsNullOrWhiteSpace(installed) ? row.Installed : installed;
            row.Available = latest;
            row.Status = string.Equals(NormalizeVersionLabel(installed), NormalizeVersionLabel(latest), StringComparison.OrdinalIgnoreCase) ? "UP TO DATE" : "UPDATE AVAILABLE";
            row.Recommendation = row.Status == "UP TO DATE" ? "The installed release matches the latest maintained GitHub release." : $"A newer maintained release ({latest}) is available.";
        }
        catch (Exception ex)
        {
            row.Status = "UNABLE TO CHECK";
            row.Available = "Check unavailable";
            row.Recommendation = "GitHub release comparison failed: " + ex.Message;
        }
    }

    private static async Task CheckGitHubCommitComponentAsync(UpdateCenterRow row, HttpClient client, string apiUrl, string manifestPath)
    {
        if (!File.Exists(manifestPath)) return;
        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var installedAt = manifest.RootElement.TryGetProperty("installedAt", out var installedValue) && installedValue.TryGetDateTimeOffset(out var parsed) ? parsed : File.GetLastWriteTimeUtc(manifestPath);
            using var commit = JsonDocument.Parse(await client.GetStringAsync(apiUrl));
            var sha = commit.RootElement.GetProperty("sha").GetString() ?? throw new InvalidOperationException("GitHub returned no commit identity.");
            var commitDate = commit.RootElement.GetProperty("commit").GetProperty("committer").GetProperty("date").GetDateTimeOffset();
            row.Installed = $"Installed {installedAt:g}";
            row.Available = sha[..Math.Min(8, sha.Length)];
            row.Status = installedAt >= commitDate.AddMinutes(-5) ? "UP TO DATE" : "UPDATE AVAILABLE";
            row.Recommendation = row.Status == "UP TO DATE" ? "The decoder installation is at least as new as the maintained source revision." : "The maintained PlM/Oodle source has changed since this decoder was installed; rebuild is recommended.";
        }
        catch (Exception ex)
        {
            row.Status = "UNABLE TO CHECK";
            row.Available = "Check unavailable";
            row.Recommendation = "GitHub source comparison failed: " + ex.Message;
        }
    }

    private static async Task CheckDotNetRuntimeAsync(UpdateCenterRow row, HttpClient client)
    {
        try
        {
            var installed = Environment.Version;
            using var index = JsonDocument.Parse(await client.GetStringAsync("https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json"));
            var channel = index.RootElement.GetProperty("releases-index").EnumerateArray()
                .FirstOrDefault(x => string.Equals(x.GetProperty("channel-version").GetString(), $"{installed.Major}.{installed.Minor}", StringComparison.OrdinalIgnoreCase));
            if (channel.ValueKind == JsonValueKind.Undefined) throw new InvalidOperationException("The installed .NET channel was not found in Microsoft metadata.");
            var latestText = channel.GetProperty("latest-runtime").GetString() ?? throw new InvalidOperationException("Microsoft returned no latest runtime version.");
            if (!Version.TryParse(latestText, out var latest)) throw new InvalidOperationException("Latest .NET runtime version could not be parsed.");
            row.Available = latestText;
            row.Status = latest > installed ? "UPDATE AVAILABLE" : "UP TO DATE";
            row.Recommendation = latest > installed ? $"A newer .NET {installed.Major}.{installed.Minor} runtime patch ({latestText}) is available." : $"The current .NET {installed.Major}.{installed.Minor} runtime channel is up to date.";
        }
        catch (Exception ex)
        {
            row.Status = "UNABLE TO CHECK";
            row.Available = "Check unavailable";
            row.Recommendation = "Microsoft .NET version comparison failed: " + ex.Message;
        }
    }

    private static string NormalizeVersionLabel(string? value) => (value ?? string.Empty).Trim().TrimStart('v', 'V');

    private static void FinalizeUpdateCheckResults(List<UpdateCenterRow> rows)
    {
        foreach (var row in rows)
        {
            if (row.Status is not ("NOT CHECKED" or "UNKNOWN" or "CHECKING")) continue;

            switch (row.Component)
            {
                case "Visual C++ Runtime":
                    row.Status = "INSTALLED / VERIFIED";
                    row.Available = "Installed runtime";
                    row.Recommendation = "The Visual C++ runtime was detected by Windows. Microsoft does not expose a reliable per-machine latest-version comparison here.";
                    break;
                case "MystTiq Server Manager":
                    row.Status = "RELEASE SOURCE NOT CONFIGURED";
                    row.Available = "No release catalog";
                    row.Recommendation = "Configure a MystTiq release catalog before automatic manager updates can be checked.";
                    break;
                default:
                    row.Status = "UNABLE TO CHECK";
                    row.Recommendation = string.IsNullOrWhiteSpace(row.Recommendation)
                        ? "MystTiq could not obtain reliable update metadata for this component."
                        : row.Recommendation;
                    break;
            }
        }
    }

    private static string GetUpdateAction(string status) => status switch
    {
        "UPDATE AVAILABLE" => "UPDATE",
        "NOT INSTALLED" or "MISSING" => "INSTALL",
        "CHECK FAILED" or "UNABLE TO CHECK" => "RETRY",
        "MANUAL CHECK REQUIRED" or "RELEASE SOURCE NOT CONFIGURED" => "SOURCE",
        "UP TO DATE" or "CURRENT" or "READY" or "INSTALLED / VERIFIED" => "VERIFY",
        _ => "CHECK"
    };

    private void BindUpdateCenterRows(List<UpdateCenterRow> rows)
    {
        foreach (var row in rows)
            if (string.IsNullOrWhiteSpace(row.Group) || row.Group == "Other") row.Group = ClassifyUpdateGroup(row.Component);
        var view = CollectionViewSource.GetDefaultView(rows);
        view.GroupDescriptions.Clear();
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(UpdateCenterRow.Group)));
        UpdateCenterGrid.ItemsSource = view;
    }

    private void ApplyUpdateCenterRows(List<UpdateCenterRow> rows)
    {
        var checkedAt = DateTime.Now;
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Group) || row.Group == "Other") row.Group = ClassifyUpdateGroup(row.Component);
            row.LastChecked = checkedAt.ToString("g");
            row.LastUpdated = ResolveLastUpdated(row);
            row.Action = GetUpdateAction(row.Status);
        }
        SaveUpdateInventoryMetadata(rows, checkedAt);
        UpdateAllAvailableButton.IsEnabled = rows.Any(r => r.Status.Equals("UPDATE AVAILABLE", StringComparison.OrdinalIgnoreCase));
        BindUpdateCenterRows(rows);
        var updates = rows.Count(r => r.Status.Equals("UPDATE AVAILABLE", StringComparison.OrdinalIgnoreCase));
        var failures = rows.Count(r => r.Status.Equals("CHECK FAILED", StringComparison.OrdinalIgnoreCase));
        var unavailable = rows.Count(r => r.Status.Equals("UNABLE TO CHECK", StringComparison.OrdinalIgnoreCase));
        var manual = rows.Count(r => r.Status.Equals("MANUAL CHECK REQUIRED", StringComparison.OrdinalIgnoreCase));
        var attention = rows.Count(r => r.Status is "NOT INSTALLED" or "ATTENTION" or "MISSING");
        var current = rows.Count(r => r.Status is "UP TO DATE" or "CURRENT" or "READY" or "INSTALLED / VERIFIED");
        UpdateCenterSummaryText.Text = $"{rows.Count} checked · {current} up to date/verified · {updates} update(s) available · {attention} not installed/attention · {failures} failed · {manual + unavailable} manual/unavailable. Last checked {checkedAt:g}.";
        UpdateCenterCheckedBadge.Text = updates > 0 ? $"{updates} UPDATE{(updates == 1 ? "" : "S")}" : failures > 0 ? "CHECK FAILED" : attention > 0 ? "NEEDS ATTENTION" : manual + unavailable > 0 ? "CHECK COMPLETE — REVIEW" : "UP TO DATE ✓";
        UpdateCenterCheckedBadge.Foreground = new SolidColorBrush(updates > 0
            ? Color.FromRgb(240, 180, 77)
            : failures > 0 ? Color.FromRgb(224, 106, 106) : attention > 0 || manual + unavailable > 0 ? Color.FromRgb(230, 197, 107) : Color.FromRgb(84, 217, 140));
        UpdateCenterSummaryBorder.BorderBrush = new SolidColorBrush(updates > 0
            ? Color.FromRgb(179, 122, 24)
            : failures > 0 ? Color.FromRgb(181, 59, 59) : attention > 0 || manual + unavailable > 0 ? Color.FromRgb(106, 84, 36) : Color.FromRgb(31, 139, 76));
    }


    private static string UpdateInventoryMetadataPath => Path.Combine(ApplicationPathService.Current.ActivityRoot, "update-inventory.json");

    private static bool RestoreUpdateInventoryMetadata(List<UpdateCenterRow> rows)
    {
        try
        {
            if (!File.Exists(UpdateInventoryMetadataPath)) return false;
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(UpdateInventoryMetadataPath));
            if (!doc.RootElement.TryGetProperty("components", out var items) || items.ValueKind != System.Text.Json.JsonValueKind.Array) return false;
            var saved = items.EnumerateArray().Where(x => x.TryGetProperty("Component", out _)).ToDictionary(x => x.GetProperty("Component").GetString() ?? "", StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (!saved.TryGetValue(row.Component, out var item)) continue;
                if (item.TryGetProperty("latestVersion", out var latest)) row.Available = latest.GetString() ?? row.Available;
                if (item.TryGetProperty("LastChecked", out var checkedAt)) row.LastChecked = checkedAt.GetString() ?? row.LastChecked;
                if (item.TryGetProperty("LastUpdated", out var updatedAt)) row.LastUpdated = updatedAt.GetString() ?? row.LastUpdated;
                if (item.TryGetProperty("Status", out var status)) row.Status = status.GetString() ?? row.Status;
                row.Action = GetUpdateAction(row.Status);
            }
            return true;
        }
        catch { return false; }
    }

    private async void UpdateCenterRowAction_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not UpdateCenterRow row) return;
        if (row.Action is "CHECK" or "RETRY") { RefreshUpdateCenter_Click(sender, e); return; }
        if (row.Component == "Palworld Dedicated Server" && row.Action == "UPDATE") { UpdateServerFromCenter_Click(sender, e); return; }
        if (row.Component == "UE4SS Runtime" && row.Action == "UPDATE") { NavigateToPage(MainPageIndex.ModRuntime); RefreshModRuntime(); return; }
        if (row.Group == "Installed MODs" && row.Action == "UPDATE") { NavigateToPage(MainPageIndex.ModLibrary); RefreshMods(); return; }
        if (row.Component == "Python Runtime" && row.Action == "UPDATE") { await InstallPythonFromUiAsync(); RefreshUpdateCenter_Click(sender, e); return; }
        if (row.Component == "pip" && row.Action == "UPDATE") { await UpgradePipFromUpdateCenterAsync(); RefreshUpdateCenter_Click(sender, e); return; }
        if (row.Component == "Palworld Save Tools" && row.Action == "UPDATE") { await InstallSaveToolsFromUiAsync(); RefreshUpdateCenter_Click(sender, e); return; }
        if (row.Component == "PlM/Oodle Decoder" && row.Action == "UPDATE") { await InstallPlmDecoderFromUiAsync(); RefreshUpdateCenter_Click(sender, e); return; }
        if (row.Component == ".NET Runtime" && row.Action == "UPDATE") { OpenUpdateSource(row); return; }
        if (row.Action == "SOURCE")
        {
            OpenUpdateSource(row);
            return;
        }
        if (row.Action == "INSTALL")
        {
            if (row.Component == "Python Runtime") await InstallPythonFromUiAsync();
            else if (row.Component == "Palworld Save Tools") await InstallSaveToolsFromUiAsync();
            else if (row.Component == "PlM/Oodle Decoder") await InstallPlmDecoderFromUiAsync();
            else if (row.Component == "Microsoft C++ Build Tools") await RunExclusive(async ct => await installer.InstallComponentAsync("Microsoft C++ Build Tools", CreateInstallProgress(), ct));
            else if (row.Component == "SteamCMD") await InstallSteamCmdFromUiAsync();
            else if (row.Component == "UE4SS Runtime") await InstallUe4ssFromUiAsync();
            else if (row.Component == "Palworld Dedicated Server") await InstallServerFromUiAsync();
            else AppDialog.Show("This component is managed externally. Use the source shown in Update Center.", "Update Center", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshUpdateCenter();
            return;
        }
        if (row.Action == "VERIFY")
            AppDialog.Show(row.Recommendation, row.Component, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task UpgradePipFromUpdateCenterAsync()
    {
        var python = ResolvePythonExecutable();
        if (string.IsNullOrWhiteSpace(python))
        {
            AppDialog.Show("Python could not be resolved. Repair Python from Server Setup first.", "Update pip", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        await RunExclusive(async ct =>
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = python,
                Arguments = "-m pip install --upgrade pip",
                WorkingDirectory = settings.ServerRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }) ?? throw new InvalidOperationException("Unable to start Python.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr);
        });
    }

    private static void OpenUpdateSource(UpdateCenterRow row)
    {
        var url = row.Component switch
        {
            "Python Runtime" => "https://www.python.org/downloads/",
            "pip" => "https://pypi.org/project/pip/",
            "Palworld Save Tools" => "https://github.com/cheahjs/palworld-save-tools",
            "PlM/Oodle Decoder" => "https://github.com/deafdudecomputers/PalworldSaveTools",
            ".NET Runtime" => "https://dotnet.microsoft.com/download/dotnet",
            "Visual C++ Runtime" => "https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist",
            "MystTiq Server Manager" => null,
            _ => null
        };
        if (string.IsNullOrWhiteSpace(url))
        {
            AppDialog.Show(row.Recommendation, row.Component, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { AppDialog.Show($"Unable to open the source page.\n\n{ex.Message}", row.Component, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private async void UpdateAllAvailable_Click(object sender, RoutedEventArgs e)
    {
        var rows = (UpdateCenterGrid.ItemsSource as ICollectionView)?.SourceCollection?.Cast<UpdateCenterRow>().ToList() ?? [];
        var available = rows.Where(r => r.Status.Equals("UPDATE AVAILABLE", StringComparison.OrdinalIgnoreCase)).ToList();
        if (available.Count == 0) { AppDialog.Show("No confirmed updates are currently available.", "Update Center", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (available.Any(r => r.Component == "Palworld Dedicated Server")) UpdateServerFromCenter_Click(sender, e);
        if (available.Any(r => r.Component == "UE4SS Runtime" || r.Group == "Installed MODs"))
            AppDialog.Show("Server updates have been started. UE4SS and MOD updates require selecting the desired release/package, so MystTiq will open their management pages rather than guessing.", "Update All Available", MessageBoxButton.OK, MessageBoxImage.Information);
        await Task.CompletedTask;
    }

    private static string ClassifyUpdateGroup(string component) => component switch
    {
        "SteamCMD" or "Palworld Dedicated Server" or "UE4SS Runtime" or "MystTiq Server Manager" => "Core Server",
        "Python Runtime" or "pip" or "Palworld Save Tools" or "Microsoft C++ Build Tools" or "PlM/Oodle Decoder" or ".NET Runtime" or "Visual C++ Runtime" => "Save & Runtime Dependencies",
        _ => "Installed MODs"
    };

    private static string? GetProcessVersion(string? executable, string arguments)
    {
        if (string.IsNullOrWhiteSpace(executable)) executable = "python";
        try
        {
            using var process = Process.Start(new ProcessStartInfo { FileName = executable, Arguments = arguments, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
            if (process is null || !process.WaitForExit(5000) || process.ExitCode != 0) return null;
            var value = (process.StandardOutput.ReadToEnd() + " " + process.StandardError.ReadToEnd()).Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
        catch { return null; }
    }

    private static void AddFileBackedUpdateRow(List<UpdateCenterRow> rows, string group, string component, string path, string source)
    {
        var exists = File.Exists(path);
        rows.Add(new UpdateCenterRow
        {
            Group = group,
            Component = component,
            Installed = exists ? GetFileVersionSafe(path) : "Not installed",
            Available = exists ? "Check source" : "Available",
            Status = exists ? "NOT CHECKED" : "NOT INSTALLED",
            Source = source,
            Recommendation = exists ? "Check the maintained source for a newer release." : "Install this dependency from Server Setup."
        });
    }

    private string ResolveLastUpdated(UpdateCenterRow row)
    {
        try
        {
            string? path = row.Component switch
            {
                "SteamCMD" => settings.SteamCmdPath,
                "Palworld Dedicated Server" => settings.ServerExe,
                "UE4SS Runtime" => environment.GetUe4ssRuntimeFolder(),
                "Palworld Save Tools" => Path.Combine(settings.ServerRoot, "Tools", "palworld-save-tools", ".myst-install.json"),
                "PlM/Oodle Decoder" => Path.Combine(settings.ServerRoot, "Tools", "palworld-plm-tools", ".myst-install.json"),
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (File.Exists(path)) return File.GetLastWriteTime(path).ToString("g");
                if (Directory.Exists(path)) return Directory.GetLastWriteTime(path).ToString("g");
            }
        }
        catch { }
        return "Unknown";
    }

    private static void SaveUpdateInventoryMetadata(IEnumerable<UpdateCenterRow> rows, DateTime checkedAt)
    {
        try
        {
            var folder = ApplicationPathService.Current.ActivityRoot;
            Directory.CreateDirectory(folder);
            var payload = new { lastChecked = checkedAt, components = rows.Select(row => new { row.Group, row.Component, row.Installed, latestVersion = row.Available, row.LastChecked, row.LastUpdated, row.Status, row.Source }).ToArray() };
            AtomicFile.Write(Path.Combine(folder, "update-inventory.json"), JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private string? GetInstalledPalworldBuildId()
    {
        var candidates = new[]
        {
            Path.Combine(settings.ServerRoot, "steamapps", "appmanifest_2394010.acf"),
            Path.Combine(settings.ServerRoot, "appmanifest_2394010.acf"),
            Path.Combine(Directory.GetParent(settings.ServerRoot)?.FullName ?? settings.ServerRoot, "steamapps", "appmanifest_2394010.acf")
        };
        foreach (var file in candidates.Where(File.Exists))
        {
            try
            {
                var match = Regex.Match(File.ReadAllText(file), "\\\"buildid\\\"\\s+\\\"(?<id>\\d+)\\\"", RegexOptions.IgnoreCase);
                if (match.Success) return match.Groups["id"].Value;
            }
            catch { }
        }
        return null;
    }

    private async Task<string?> QueryLatestPalworldBuildIdAsync()
    {
        if (!File.Exists(settings.SteamCmdPath))
            throw new FileNotFoundException("SteamCMD is required to query the latest Palworld build.", settings.SteamCmdPath);

        var psi = new ProcessStartInfo
        {
            FileName = settings.SteamCmdPath,
            WorkingDirectory = Path.GetDirectoryName(settings.SteamCmdPath) ?? settings.ServerRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add("+login"); psi.ArgumentList.Add("anonymous");
        psi.ArgumentList.Add("+app_info_update"); psi.ArgumentList.Add("1");
        psi.ArgumentList.Add("+app_info_request"); psi.ArgumentList.Add("2394010");
        psi.ArgumentList.Add("+app_info_print"); psi.ArgumentList.Add("2394010");
        psi.ArgumentList.Add("+quit");

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("SteamCMD could not be started.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("SteamCMD did not return Palworld app information within 90 seconds.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var output = stdout + Environment.NewLine + stderr;
        var diagnosticRoot = ApplicationPathService.Current.DiagnosticsRoot;
        Directory.CreateDirectory(diagnosticRoot);
        AtomicFile.Write(Path.Combine(diagnosticRoot, "SteamCMD_Palworld_AppInfo_Last.txt"), output);

        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
            throw new InvalidOperationException($"SteamCMD exited with code {process.ExitCode}. See SteamCMD_Palworld_AppInfo_Last.txt in MystTiq diagnostics.");

        var publicIndex = output.IndexOf("\"public\"", StringComparison.OrdinalIgnoreCase);
        if (publicIndex >= 0)
        {
            var publicWindow = output.Substring(publicIndex, Math.Min(2500, output.Length - publicIndex));
            var publicBuild = Regex.Match(publicWindow, @"""buildid""\s+""(?<id>\d+)""", RegexOptions.IgnoreCase);
            if (publicBuild.Success) return publicBuild.Groups["id"].Value;
        }

        var allBuilds = Regex.Matches(output, @"""buildid""\s+""(?<id>\d+)""", RegexOptions.IgnoreCase)
            .Select(m => m.Groups["id"].Value).Distinct().ToList();
        if (allBuilds.Count == 1) return allBuilds[0];

        var reason = output.Contains("No app info for AppID", StringComparison.OrdinalIgnoreCase)
            ? "SteamCMD returned no app information for App ID 2394010."
            : $"SteamCMD returned app information, but the public build ID could not be parsed ({allBuilds.Count} build ID candidate(s)).";
        throw new InvalidOperationException(reason + " Raw output was saved to MystTiq diagnostics.");
    }

    private static string GetFileVersionSafe(string path)
    {
        try
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
            return string.IsNullOrWhiteSpace(info.FileVersion) ? "Installed" : info.FileVersion!;
        }
        catch { return "Installed"; }
    }

    private async void UpdateServerFromCenter_Click(object sender, RoutedEventArgs e)
    {
        if (server.IsRunning())
        {
            AppDialog.Show("Stop the Palworld server before updating its files.", "Update Server", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = AppDialog.Show("SteamCMD will validate and update the Palworld dedicated server files. Continue?", "Update Server", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        await RunExclusive(async ct =>
        {
            var progress = new Progress<InstallProgressInfo>(p =>
            {
                SetSetupOperationState("RUNNING", "Updating Palworld Dedicated Server", p.Message, p.Percent);
            });
            try
            {
                await installer.InstallPalworldServerAsync(progress, ct);
                SetSetupOperationState("COMPLETE", "Palworld server update complete", "SteamCMD validation/update completed successfully.", 100, completed: true);
                RefreshEnvironment();
                RefreshUpdateCenter();
            }
            catch (Exception ex)
            {
                SetSetupOperationState("FAILED", "Palworld server update failed", ex.Message, 100, failed: true);
                AppDialog.Show(ex.Message, "Update Server", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }

    private void VerifyEnvironment_Click(object sender, RoutedEventArgs e)
    {
        SetSetupOperationState("RUNNING", "Verifying server environment", "Checking files, configuration, remote administration, and storage.", 15);
        var results = environment.Scan().Select(row => (row.Component, Result: environment.VerifyComponent(row.Component))).ToList();
        var failed = results.Where(item => !item.Result.Success && item.Component is not "RCON").ToList();
        var report = string.Join(Environment.NewLine, results.Select(item => $"{(item.Result.Success ? "PASS" : "CHECK")}  {item.Component}: {item.Result.Message}"));
        SetSetupOperationState(
            failed.Count == 0 ? "COMPLETE" : "ATTENTION",
            "Environment verification complete",
            failed.Count == 0 ? "All required components passed." : $"{failed.Count} required component(s) need attention.",
            100,
            completed: failed.Count == 0,
            failed: failed.Count > 0);
        AppDialog.Show(report, "Verify All Components", MessageBoxButton.OK, failed.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        RefreshEnvironment();
        _ = RefreshStatusAsync();
    }

    private async void EnvironmentAction_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not EnvironmentComponentRow row) return;
        if (row.Action == "VERIFY")
        {
            var result = environment.VerifyComponent(row.Component);
            InstallStatusText.Text = $"{row.Component}: {result.Message}";
            AppDialog.Show(result.Message, row.Component + " Verification", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            RefreshEnvironment();
            await RefreshStatusAsync();
            return;
        }
        if (row.Component == "SteamCMD" && row.Action == "INSTALL") await InstallSteamCmdFromUiAsync();
        else if (row.Component == "Palworld Dedicated Server" && row.Action == "INSTALL") await InstallServerFromUiAsync();
        else if (row.Component == "UE4SS Runtime" && row.Action == "INSTALL") await InstallUe4ssFromUiAsync();
        else if (row.Component == "Python Runtime" && row.Action == "INSTALL") await InstallPythonFromUiAsync();
        else if (row.Component == "Palworld Save Tools" && row.Action == "INSTALL") await InstallSaveToolsFromUiAsync();
        else if (row.Component == "Microsoft C++ Build Tools" && row.Action == "INSTALL") await RunExclusive(async ct => await installer.InstallComponentAsync("Microsoft C++ Build Tools", CreateInstallProgress(), ct));
        else if (row.Component == "PlM/Oodle Decoder" && row.Action == "INSTALL") await InstallPlmDecoderFromUiAsync();
        else if (row.Component == "UE4SS Runtime" && row.Action == "MANAGE")
        {
            NavigateToPage(11);
            RefreshModRuntime();
            return;
        }
        else if (row.Component == "UE4SS Runtime" && (row.Action == "DISABLE" || row.Action == "ENABLE"))
        {
            if (server.IsRunning())
            {
                AppDialog.Show("Stop PalServer before changing the UE4SS runtime state.", "UE4SS Runtime", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var message = row.Action == "DISABLE" ? environment.DisableUe4ssRuntime() : environment.EnableUe4ssRuntime();
                Log("[UE4SS] " + message);
                InstallStatusText.Text = message;
                AppDialog.Show(message + "\n\nThe next PalServer start will use this runtime state.", "UE4SS Runtime", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log("[UE4SS] Runtime state change failed: " + ex.Message);
                AppDialog.Show(ex.Message, "UE4SS Runtime", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else if (row.Component == "Backup Storage" && row.Action == "CREATE") Directory.CreateDirectory(settings.BackupRoot);
        else if (row.Component == "Default Server Settings" && row.Action == "CREATE")
        {
            installer.CreateDefaultConfiguration(SetupServerNameBox.Text, SetupDescriptionBox.Text, SetupAdminPasswordBox.Password, SetupServerPasswordBox.Password, 32, 8211, 8212);
            SyncApiPasswordFromServerConfiguration(logChanges: true);
            ReloadConfig();
        }
        else if ((row.Component == "RCON" || row.Component == "REST API") && row.Action == "ENABLE")
        {
            var restPort = int.TryParse(SetupRestPortBox.Text, out var configuredRestPort) ? configuredRestPort : 8212;
            installer.EnsureRemoteAdministrationEnabled(SetupAdminPasswordBox.Password, restPort, 25575);
            SyncApiPasswordFromServerConfiguration(logChanges: true);
            ReloadConfig();
            InstallStatusText.Text = "REST API and RCON were enabled in PalWorldSettings.ini. Restart the Palworld server before connecting.";
            AppDialog.Show(
                "REST API and RCON are now enabled in the active server configuration.\n\n" +
                "RCON Port: 25575\n" +
                "Password: AdminPassword\n\n" +
                "Restart the Palworld server for the change to take effect.",
                "Remote Administration Enabled", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else AppDialog.Show($"No automated action is registered for {row.Component}.", "Server Setup", MessageBoxButton.OK, MessageBoxImage.Information);
        RefreshEnvironment();
    }




    private async void InstallRequired_Click(object sender, RoutedEventArgs e)
    {
        if(AppDialog.Show("This will install any missing required components: Python with pip, SteamCMD, the Palworld dedicated server, latest experimental UE4SS, Palworld Save Tools, default settings, backup storage, and mod folders. Continue?","Install Required Components",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        SetSetupOperationState("RUNNING", "Installing missing components", "Preparing the guided installation workflow.", 1);
        var players=int.TryParse(SetupPlayersBox.Text,out var p)?p:32; var gamePort=int.TryParse(SetupPublicPortBox.Text,out var gp)?gp:8211; var restPort=int.TryParse(SetupRestPortBox.Text,out var rp)?rp:8212;
        await RunExclusive(async ct => await installer.InstallRequiredAsync(SetupServerNameBox.Text,SetupDescriptionBox.Text,SetupAdminPasswordBox.Password,SetupServerPasswordBox.Password,players,gamePort,restPort,CreateInstallProgress(),ct));
        SyncApiPasswordFromServerConfiguration(logChanges: true);
        ReloadConfig(); RefreshEnvironment();
        SetSetupOperationState("COMPLETE", "Missing components installed", "Required components are installed. REST API and RCON are enabled; restart the Palworld server before connecting.", 100, completed: true);
    }

    private void CreateSetupSettings_Click(object sender, RoutedEventArgs e)
    {
        if(!int.TryParse(SetupPlayersBox.Text,out var players) || players<1 || players>128 || !int.TryParse(SetupPublicPortBox.Text,out var gamePort) || !int.TryParse(SetupRestPortBox.Text,out var restPort))
        { AppDialog.Show("Enter valid player and port values.","Default Settings",MessageBoxButton.OK,MessageBoxImage.Warning); return; }
        installer.CreateDefaultConfiguration(SetupServerNameBox.Text,SetupDescriptionBox.Text,SetupAdminPasswordBox.Password,SetupServerPasswordBox.Password,players,gamePort,restPort);
        SyncApiPasswordFromServerConfiguration(logChanges: true);
        SetSetupOperationState("COMPLETE", "Default configuration created", "REST API credentials were synchronized with the manager.", 100, completed: true);
        ReloadConfig(); RefreshEnvironment();
    }

    private IProgress<InstallProgressInfo> CreateInstallProgress() => new Progress<InstallProgressInfo>(info =>
    {
        var percent = Math.Clamp(info.Percent, 0, 100);
        SetSetupOperationState("RUNNING", info.Component, info.Message, percent);
        Log($"{info.Component}: {info.Message}");
    });




    private async void InstallPython_Click(object sender, RoutedEventArgs e)
    {
        await InstallPythonFromUiAsync();
    }

    private async Task InstallPythonFromUiAsync()
    {
        if (AppDialog.Show("Install the latest official 64-bit Python release with pip?", "Install Python", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await RunExclusive(async ct => { await installer.InstallComponentAsync("Python Runtime", CreateInstallProgress(), ct); });
        RefreshEnvironment();
    }

    private async Task InstallSaveToolsFromUiAsync()
    {
        if (AppDialog.Show("Install or repair the official cheahjs palworld-save-tools converter under the configured server folder? Python 3.9+ is required.", "Install Palworld Save Tools", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await RunExclusive(async ct => { await installer.InstallComponentAsync("Palworld Save Tools", CreateInstallProgress(), ct); });
        RefreshEnvironment();
    }


    private async Task InstallPlmDecoderFromUiAsync()
    {
        if (AppDialog.Show("Install or repair PlM/Oodle decoding support? This installs pyooz and the PlM-capable PalworldSaveTools source under the server Tools folder.", "Install PlM/Oodle Decoder", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await RunExclusive(async ct => { await installer.InstallComponentAsync("PlM/Oodle Decoder", CreateInstallProgress(), ct); });
        RefreshEnvironment();
    }

    private async Task InstallUe4ssFromUiAsync()
    {
        if(AppDialog.Show("Install or repair the latest experimental UE4SS release in the Palworld server Win64 folder?","Install UE4SS",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        await RunExclusive(async ct => { await installer.InstallComponentAsync("UE4SS Runtime", CreateInstallProgress(), ct); });
        RefreshEnvironment();
    }

    private async Task InstallServerFromUiAsync()
    {
        await RunExclusive(async ct => { await installer.InstallComponentAsync("Palworld Dedicated Server", CreateInstallProgress(), ct); });
        RefreshEnvironment();
    }

    private async Task InstallSteamCmdFromUiAsync()
    {
        await RunExclusive(async ct => { await installer.InstallComponentAsync("SteamCMD", CreateInstallProgress(), ct); });
        RefreshEnvironment();
    }

    private static bool LooksLikeUe4ssRuntimePackage(string sourcePath)
    {
        if (!Directory.Exists(sourcePath)) return false;
        try
        {
            var fileNames = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return fileNames.Contains("UE4SS.dll") ||
                   fileNames.Contains("dwmapi.dll") ||
                   fileNames.Contains("xinput1_3.dll") ||
                   fileNames.Contains("UE4SS-settings.ini");
        }
        catch { return false; }
    }

    private async void ImportLocalMod_Click(object sender, RoutedEventArgs e)
    {
        if (LocalModsGrid.SelectedItem is not LocalModRow mod) { AppDialog.Show("Select a local Workshop mod first.", "Import Mod", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (mod.Compatibility == "Unknown" && AppDialog.Show("This mod's server compatibility is unknown. Import it anyway?", "Compatibility Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        if (server.IsRunning()) { AppDialog.Show("Stop the server before importing a mod.", "Server Running", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

        if (LooksLikeUe4ssRuntimePackage(mod.SourcePath) && environment.VerifyComponent("UE4SS Runtime").Success)
        {
            var answer = AppDialog.Show(
                "This Workshop package appears to contain a UE4SS runtime, but UE4SS is already installed in the configured Palworld server.\n\n" +
                "Installing a second runtime is usually redundant and can cause startup or shutdown instability.\n\n" +
                "Import the duplicate runtime anyway?",
                "UE4SS Already Satisfied", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
            if (answer != MessageBoxResult.Yes)
            {
                Log($"Skipped redundant UE4SS Workshop package '{mod.Name}'. Existing server runtime satisfies the dependency.");
                return;
            }
        }
        await RunExclusive(async ct =>
        {
            if (Directory.Exists(settings.SaveRoot))
            {
                var safetyBackup = await backups.CreateAsync(null, false, ct);
                Log("Created pre-import safety backup: " + safetyBackup);
            }
            else
            {
                Log("No SaveGames folder exists yet, so the pre-import safety backup was skipped. The mod import can continue safely before the first world is created.");
            }

            var destination = environment.ImportLocalMod(mod);
            Log($"Imported local mod '{mod.Name}' to {destination}");
        });
        RefreshMods();
        ScanLocalMods_Click(sender, e);
    }


    private async Task LoadUe4ssReleasesAsync(bool preserveSelection = false, bool forceRefresh = false)
    {
        if (Ue4ssSourceCombo is null || Ue4ssReleaseCombo is null || Ue4ssReleaseDetailText is null) return;
        var source = (Ue4ssSourceCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Palworld Fork";
        var previousKey = preserveSelection && Ue4ssReleaseCombo.SelectedItem is Ue4ssReleaseInfo previous ? previous.ReleaseKey : null;

        Ue4ssReleaseCombo.ItemsSource = null;
        Ue4ssReleaseCombo.SelectedItem = null;
        var cachedBefore = ue4ssReleases.GetCachedReleases(source);
        Ue4ssReleaseDetailText.Text = forceRefresh
            ? $"Refreshing {source} release catalog from GitHub..."
            : cachedBefore.Count > 0
                ? $"Loading cached {source} release catalog..."
                : $"No cached {source} catalog yet. Loading releases from GitHub...";
        try
        {
            var releases = forceRefresh
                ? await ue4ssReleases.RefreshReleasesAsync(source)
                : await ue4ssReleases.GetReleasesAsync(source);
            Ue4ssReleaseCombo.ItemsSource = releases;
            if (releases.Count > 0)
            {
                var selected = previousKey is null ? null : releases.FirstOrDefault(r => string.Equals(r.ReleaseKey, previousKey, StringComparison.OrdinalIgnoreCase));
                Ue4ssReleaseCombo.SelectedItem = selected ?? releases[0];
            }

            var updated = ue4ssReleases.GetCacheUpdatedAt(source);
            var cacheNote = updated.HasValue ? $" Cache updated {updated.Value:g}." : "";
            Ue4ssReleaseDetailText.Text = releases.Count == 0
                ? $"No releases are cached or currently available for {source}. Use Refresh Releases or GitHub Releases."
                : forceRefresh
                    ? $"Refreshed {source}: {releases.Count} cached/selectable runtime package(s). Older known releases are retained for rollback history.{cacheNote}"
                    : $"Loaded {releases.Count} runtime package(s) from the persistent {source} catalog.{cacheNote} Use Refresh Releases to check GitHub for changes.";
        }
        catch (Exception ex)
        {
            // If GitHub refresh fails, preserve the existing local catalog whenever one exists.
            var fallback = ue4ssReleases.GetCachedReleases(source);
            if (fallback.Count > 0)
            {
                Ue4ssReleaseCombo.ItemsSource = fallback;
                Ue4ssReleaseCombo.SelectedItem = fallback[0];
                Ue4ssReleaseDetailText.Text = $"GitHub refresh failed, but {fallback.Count} cached {source} package(s) remain available. " + ex.Message;
            }
            else
            {
                Ue4ssReleaseDetailText.Text = "GitHub release check failed and no local cache is available: " + ex.Message;
            }
            Log("[UE4SS] GitHub release check failed: " + ex.Message);
        }
    }

    private async void Ue4ssSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await LoadUe4ssReleasesAsync();
    }

    private async void CheckUe4ssVersions_Click(object sender, RoutedEventArgs e)
        => await LoadUe4ssReleasesAsync(preserveSelection: true, forceRefresh: true);

    private async void VerifyUe4ssRuntimeVersion_Click(object sender, RoutedEventArgs e)
    {
        if (Ue4ssVersionVerificationText is null) return;
        try
        {
            var state = environment.GetUe4ssRuntimeState();
            if (!state.Installed)
            {
                Ue4ssVersionVerificationText.Text = "Installed version verification: UE4SS is not installed.";
                Ue4ssVersionVerificationText.Foreground = Brushes.IndianRed;
                return;
            }

            var identity = environment.GetUe4ssRuntimeIdentity();
            var metadata = environment.GetUe4ssRuntimeMetadata();
            var source = metadata?.Source;
            if (string.IsNullOrWhiteSpace(source))
                source = (Ue4ssSourceCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Palworld Fork";

            var releases = await ue4ssReleases.GetReleasesAsync(source!);
            Ue4ssReleaseCombo.ItemsSource = releases;

            Ue4ssReleaseInfo? matched = null;
            if (metadata is not null)
            {
                matched = releases.FirstOrDefault(r =>
                    string.Equals(r.Tag, metadata.Value.Tag, StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrWhiteSpace(metadata.Value.AssetName) || string.Equals(r.AssetName, metadata.Value.AssetName, StringComparison.OrdinalIgnoreCase)));
            }
            if (matched is null && !string.IsNullOrWhiteSpace(identity.Version) && !identity.Version.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                matched = releases.FirstOrDefault(r =>
                    r.Tag.Contains(identity.Version, StringComparison.OrdinalIgnoreCase) ||
                    r.Name.Contains(identity.Version, StringComparison.OrdinalIgnoreCase) ||
                    r.AssetName.Contains(identity.Version, StringComparison.OrdinalIgnoreCase));
            }

            var latest = releases.FirstOrDefault();
            if (matched is not null)
            {
                Ue4ssReleaseCombo.SelectedItem = matched;
                var isLatest = latest is not null && string.Equals(latest.ReleaseKey, matched.ReleaseKey, StringComparison.OrdinalIgnoreCase);
                Ue4ssVersionVerificationText.Text = $"Installed version verification: VERIFIED — {matched.Tag}{(string.IsNullOrWhiteSpace(matched.AssetName) ? "" : " / " + matched.AssetName)} • {(isLatest ? "Up to date for this source" : "Older than the newest listed package")}. DLL version: {identity.Version}.";
                Ue4ssVersionVerificationText.Foreground = isLatest ? Brushes.LightGreen : Brushes.Gold;
            }
            else
            {
                var latestText = latest is null ? "No GitHub releases were returned." : $"Newest listed: {latest.Tag}{(string.IsNullOrWhiteSpace(latest.AssetName) ? "" : " / " + latest.AssetName)}.";
                Ue4ssVersionVerificationText.Text = $"Installed version verification: UNVERIFIED BUILD — DLL version {identity.Version}. {latestText} Install a release through MystTiq to record an exact source/tag match.";
                Ue4ssVersionVerificationText.Foreground = Brushes.Gold;
            }
        }
        catch (Exception ex)
        {
            Ue4ssVersionVerificationText.Text = "Installed version verification failed: " + ex.Message;
            Ue4ssVersionVerificationText.Foreground = Brushes.IndianRed;
            Log("[UE4SS] Runtime version verification failed: " + ex.Message);
        }
    }

    private void Ue4ssReleaseCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Ue4ssReleaseCombo.SelectedItem is not Ue4ssReleaseInfo release) return;
        Ue4ssReleaseDetailText.Text = $"Source: {release.Source}  •  Published: {(release.PublishedAt == DateTime.MinValue ? "Unknown" : release.PublishedAt.ToLocalTime().ToString("g"))}  •  {(release.Prerelease ? "Pre-release / experimental" : "Release / stable-tagged")}  •  Asset: {(string.IsNullOrWhiteSpace(release.AssetName) ? "No ZIP asset detected" : release.AssetName)}";
    }

    private void OpenUe4ssGithub_Click(object sender, RoutedEventArgs e)
    {
        var source = (Ue4ssSourceCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Palworld Fork";
        var url = source.Contains("Upstream", StringComparison.OrdinalIgnoreCase) ? Ue4ssReleaseService.UpstreamReleasesPage : Ue4ssReleaseService.PalworldReleasesPage;
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private async void InstallSelectedUe4ssRelease_Click(object sender, RoutedEventArgs e)
    {
        if (server.IsRunning())
        {
            AppDialog.Show("Stop PalServer before changing UE4SS versions.", "UE4SS Version Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (Ue4ssReleaseCombo.SelectedItem is not Ue4ssReleaseInfo release)
        {
            AppDialog.Show("Select a GitHub release first.", "UE4SS Version Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (AppDialog.Show($"Install {release.Display}?\n\nMyst will snapshot the current runtime first and preserve user-mod folders.", "Install UE4SS Release", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;

        try
        {
            Ue4ssReleaseDetailText.Text = "Downloading selected runtime...";
            var zip = await ue4ssReleases.DownloadAsync(release);
            var message = environment.ImportUe4ssRuntimeZip(zip);
            environment.SaveUe4ssRuntimeMetadata(release);
            Log($"[UE4SS] GitHub release {release.Tag} installed. {message}");
            RefreshEnvironment();
            RefreshModRuntime();
            Ue4ssReleaseDetailText.Text = $"Installed {release.Display}. Run Verify and test the server before enabling user mods.";
            AppDialog.Show(message + "\n\nSelected GitHub release: " + release.Display, "UE4SS Runtime Installed", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Ue4ssReleaseDetailText.Text = "Runtime installation failed: " + ex.Message;
            Log("[UE4SS] GitHub runtime installation failed: " + ex.Message);
            AppDialog.Show(ex.Message, "UE4SS Runtime Install Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private int GetStabilityDurationMinutes()
    {
        if (StabilityDurationBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var minutes)) return minutes;
        return 15;
    }

    private async void StartStabilityTest_Click(object sender, RoutedEventArgs e)
    {
        if (!server.IsRunning())
        {
            AppDialog.Show("Start PalServer before running an idle stability test.", "Idle Stability Test", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (stabilityTestCts is not null) return;
        stabilityTestCts = new CancellationTokenSource();
        var token = stabilityTestCts.Token;
        stabilitySamples.Clear();
        StabilityHistoryGrid.ItemsSource = stabilitySamples;
        stabilityTestStartedUtc = DateTime.UtcNow;
        var duration = TimeSpan.FromMinutes(GetStabilityDurationMinutes());
        StartStabilityTestButton.IsEnabled = false;
        StopStabilityTestButton.IsEnabled = true;
        StabilityProgressBar.Value = 0;
        StabilityStatusText.Text = $"Running {duration.TotalMinutes:0}-minute {stabilityIsolationMode} stability test...";
        Log($"[DOCTOR] Idle stability test started: mode={stabilityIsolationMode}, duration={duration.TotalMinutes:0} minutes.");

        try
        {
            while (!token.IsCancellationRequested)
            {
                var elapsed = DateTime.UtcNow - stabilityTestStartedUtc;
                var sample = await Task.Run(() => CaptureStabilitySample(elapsed), token);
                stabilitySamples.Add(sample);
                StabilityProgressBar.Value = Math.Clamp(elapsed.TotalMilliseconds / duration.TotalMilliseconds * 100d, 0d, 100d);
                var healthy = sample.Process == "Running" && sample.Responding == "Yes" && sample.GamePort == "Open";
                StabilityStatusText.Text = $"{elapsed:mm\\:ss} / {duration:mm\\:ss} • Process {sample.Process} • Responding {sample.Responding} • RAM {sample.Memory}";
                if (!healthy)
                {
                    StabilityStatusText.Text = $"ATTENTION at {elapsed:mm\\:ss}: process={sample.Process}, responding={sample.Responding}, game port={sample.GamePort}.";
                    Log("[DOCTOR] Stability test detected an unhealthy server state.");
                    break;
                }
                if (elapsed >= duration)
                {
                    StabilityProgressBar.Value = 100;
                    StabilityStatusText.Text = $"PASS — {duration.TotalMinutes:0} minute test completed with the server responding and game port available.";
                    Log("[DOCTOR] Idle stability test passed.");
                    break;
                }
                await Task.Delay(TimeSpan.FromSeconds(30), token);
            }
        }
        catch (OperationCanceledException)
        {
            StabilityStatusText.Text = "Stability test stopped by user.";
        }
        catch (Exception ex)
        {
            StabilityStatusText.Text = "Stability test failed: " + ex.Message;
            Log("[DOCTOR] Stability test error: " + ex.Message);
        }
        finally
        {
            try
            {
                var report = SaveStabilityReport();
                if (!string.IsNullOrWhiteSpace(report)) Log("[DOCTOR] Stability report saved: " + report);
            }
            catch (Exception ex) { Log("[DOCTOR] Could not save stability report: " + ex.Message); }
            stabilityTestCts?.Dispose();
            activeWorldContext.Changed -= ActiveWorldContext_Changed;
            activeWorldContext.Dispose();
            stabilityTestCts = null;
            StartStabilityTestButton.IsEnabled = true;
            StopStabilityTestButton.IsEnabled = false;
        }
    }

    private string SaveStabilityReport()
    {
        if (stabilitySamples.Count == 0) return string.Empty;
        var folder = Path.Combine(settings.LogsRoot, "Diagnostics");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"IdleStability_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv");
        var lines = new List<string>
        {
            $"# MystTiq Idle Stability Test,Mode={stabilityIsolationMode},Started={stabilityTestStartedUtc:O}",
            "Elapsed,Process,Responding,Memory,PrivateMemory,Handles,Threads,Game8211,Steam27015,REST8212,RCON25575"
        };
        lines.AddRange(stabilitySamples.Select(r => string.Join(",", new[]
        {
            r.Elapsed, r.Process, r.Responding, r.Memory, r.PrivateMemory, r.Handles.ToString(), r.Threads.ToString(), r.GamePort, r.SteamPort, r.RestPort, r.RconPort
        }.Select(value => "\"" + value.Replace("\"", "\"\"") + "\""))));
        File.WriteAllLines(path, lines, Encoding.UTF8);
        return path;
    }

    private void StopStabilityTest_Click(object sender, RoutedEventArgs e) => stabilityTestCts?.Cancel();

    private StabilitySampleRow CaptureStabilitySample(TimeSpan elapsed)
    {
        var row = new StabilitySampleRow { Elapsed = elapsed.ToString(@"mm\:ss") };
        var processInfo = server.ScanServerProcesses().FirstOrDefault(p => p.InConfiguredServerRoot);
        if (processInfo is null)
        {
            row.Process = "Stopped";
            row.Responding = "No";
        }
        else
        {
            try
            {
                using var p = Process.GetProcessById(processInfo.ProcessId);
                p.Refresh();
                row.Process = "Running";
                row.Responding = p.Responding ? "Yes" : "No";
                row.Memory = $"{p.WorkingSet64 / 1024d / 1024d / 1024d:0.00} GB";
                row.PrivateMemory = $"{p.PrivateMemorySize64 / 1024d / 1024d / 1024d:0.00} GB";
                row.Handles = p.HandleCount;
                row.Threads = p.Threads.Count;
            }
            catch
            {
                row.Process = "Unknown";
                row.Responding = "Unknown";
            }
        }
        try
        {
            var props = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
            var udp = props.GetActiveUdpListeners().Select(x => x.Port).ToHashSet();
            var tcp = props.GetActiveTcpListeners().Select(x => x.Port).ToHashSet();
            row.GamePort = udp.Contains(8211) ? "Open" : "Closed";
            row.SteamPort = udp.Contains(27015) ? "Open" : "Closed";
            row.RestPort = tcp.Contains(8212) ? "Open" : "Closed";
            row.RconPort = tcp.Contains(25575) ? "Open" : "Closed";
        }
        catch { }
        return row;
    }

    private void CaptureIsolationStateIfNeeded()
    {
        if (stabilitySavedModStates is not null) return;
        stabilitySavedModStates = mods.Scan().Select(m => new ModRow
        {
            Enabled = m.Enabled, Name = m.Name, Package = m.Package, Version = m.Version, Deployed = m.Deployed,
            Source = m.Source, Type = m.Type, Description = m.Description, EnableReason = m.EnableReason
        }).ToList();
        var runtimeState = environment.GetUe4ssRuntimeState();
        stabilitySavedUe4ssEnabled = runtimeState.Installed ? runtimeState.Enabled : null;
    }

    private void PrepareVanillaIsolation_Click(object sender, RoutedEventArgs e)
    {
        if (server.IsRunning()) { AppDialog.Show("Stop PalServer before changing isolation mode.", "Runtime Isolation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        try
        {
            CaptureIsolationStateIfNeeded();
            var rows = mods.Scan().ToList();
            foreach (var row in rows) row.Enabled = false;
            mods.Apply(rows);
            if (environment.GetUe4ssRuntimeState().Enabled) environment.DisableUe4ssRuntime();
            stabilityIsolationMode = "Vanilla";
            StabilityIsolationStatusText.Text = "Prepared VANILLA: all user mods disabled and UE4SS disabled. Start PalServer, then run Idle Stability Test.";
            RefreshMods(); RefreshModRuntime();
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "Runtime Isolation", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void PrepareUe4ssIsolation_Click(object sender, RoutedEventArgs e)
    {
        if (server.IsRunning()) { AppDialog.Show("Stop PalServer before changing isolation mode.", "Runtime Isolation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        try
        {
            CaptureIsolationStateIfNeeded();
            var rows = mods.Scan().ToList();
            foreach (var row in rows) row.Enabled = false;
            mods.Apply(rows);
            var state = environment.GetUe4ssRuntimeState();
            if (state.Installed && !state.Enabled) environment.EnableUe4ssRuntime();
            stabilityIsolationMode = "UE4SS Only";
            StabilityIsolationStatusText.Text = "Prepared UE4SS ONLY: user mods disabled and UE4SS enabled. Start PalServer, then run Idle Stability Test.";
            RefreshMods(); RefreshModRuntime();
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "Runtime Isolation", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void RestoreIsolationState_Click(object sender, RoutedEventArgs e)
    {
        if (server.IsRunning()) { AppDialog.Show("Stop PalServer before restoring the previous mod/runtime state.", "Runtime Isolation", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (stabilitySavedModStates is null && stabilitySavedUe4ssEnabled is null)
        {
            AppDialog.Show("No saved isolation state exists yet.", "Runtime Isolation", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            if (stabilitySavedModStates is not null) mods.Apply(stabilitySavedModStates);
            var state = environment.GetUe4ssRuntimeState();
            if (stabilitySavedUe4ssEnabled == true && state.Installed && !state.Enabled) environment.EnableUe4ssRuntime();
            else if (stabilitySavedUe4ssEnabled == false && state.Enabled) environment.DisableUe4ssRuntime();
            stabilitySavedModStates = null;
            stabilitySavedUe4ssEnabled = null;
            stabilityIsolationMode = "Current";
            StabilityIsolationStatusText.Text = "Previous mod/runtime state restored.";
            RefreshMods(); RefreshModRuntime();
        }
        catch (Exception ex) { AppDialog.Show(ex.Message, "Runtime Isolation Restore", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void RefreshServerEvents_Click(object sender, RoutedEventArgs e)
    {
        RecentServerEventsText.Text = "Reading recent Windows Application events...";
        try
        {
            var text = await Task.Run(() => ReadRecentServerEvents());
            RecentServerEventsText.Text = text;
        }
        catch (Exception ex) { RecentServerEventsText.Text = "Event inspection failed: " + ex.Message; }
    }

    private static string ReadRecentServerEvents()
    {
        var psi = new ProcessStartInfo
        {
            FileName = "wevtutil.exe",
            Arguments = "qe Application /q:\"*[System[(Level=1 or Level=2) and TimeCreated[timediff(@SystemTime) <= 1800000]]]\" /f:text /c:30 /rd:true",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException("wevtutil could not be started.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit(5000);
        var blocks = Regex.Split(output, "(?=Event\\[\\d+\\]:)")
            .Where(block => block.Contains("PalServer", StringComparison.OrdinalIgnoreCase) || block.Contains("Palworld", StringComparison.OrdinalIgnoreCase) || block.Contains("PalworldServerManager", StringComparison.OrdinalIgnoreCase))
            .Take(8)
            .ToList();
        if (blocks.Count == 0) return string.IsNullOrWhiteSpace(error) ? "No recent Palworld/MystTiq Application Error events were found in the last 30 minutes." : error.Trim();
        return string.Join(Environment.NewLine + Environment.NewLine, blocks).Trim();
    }

    private void RunServerDoctor_Click(object sender, RoutedEventArgs e) => _ = RunExclusive(async ct =>
    {
        ServerDoctorSummaryText.Text = "Running diagnostics...";
        var rows = (await serverDoctor.RunAsync(server, ct)).ToList();
        var io = server.GetSessionIoDiagnostics();
        var ioWithinExpectedLimits = io.ProcessRunning
            ? io.StdOutReaders <= 1 && io.StdErrReaders <= 1 && io.PalLogReaders <= 1 && io.RestPollers <= 1 && io.PlayerPollers <= 1
            : io.Clean;
        rows.Add(new DoctorCheckRow
        {
            Component = "Session I/O",
            Status = ioWithinExpectedLimits ? "Healthy" : "Attention",
            Detail = $"Session #{io.SessionId}: process={(io.ProcessRunning ? 1 : 0)}, stdout={io.StdOutReaders}, stderr={io.StdErrReaders}, Pal.log={io.PalLogReaders}, REST={io.RestPollers}, players={io.PlayerPollers}, cleanup={(io.CleanupInProgress ? 1 : 0)}.",
            Recommendation = ioWithinExpectedLimits ? "Session-owned readers are within expected limits." : "A reader/poller leak was detected. Stop the server and run Force Cleanup before starting another session."
        });
        ServerDoctorGrid.ItemsSource = rows;
        var attention = rows.Count(row => !row.Status.Equals("Healthy", StringComparison.OrdinalIgnoreCase));
        var healthy = rows.Count - attention;
        ServerDoctorSummaryText.Text = attention == 0
            ? $"Server Doctor: all {healthy} checks are healthy."
            : $"Server Doctor: {healthy} healthy, {attention} need attention. Review the recommendations below.";
        ServerDoctorSummaryText.Foreground = new SolidColorBrush(attention == 0
            ? Color.FromRgb(84, 217, 140)
            : Color.FromRgb(240, 180, 77));
        Log($"Server Doctor completed: {healthy} healthy, {attention} attention.");
    });


}
