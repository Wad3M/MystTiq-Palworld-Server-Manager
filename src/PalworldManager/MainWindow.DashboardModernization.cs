using PalworldManager.Models;
using PalworldManager.Services;
using System.Windows.Media;

namespace PalworldManager;

public partial class MainWindow
{
    private readonly DashboardIntelligenceService dashboardIntelligence = new();
    private readonly StartupCoordinator startupCoordinator = new();
    private bool dashboardRefreshInProgress;
    private bool startupWorldDataInitialized;
    private ServerLifecycleState dashboardLifecycleState = ServerLifecycleState.Stopped;

    private async Task InitializeWorldDataOnStartupAsync()
    {
        if (startupWorldDataInitialized) return;
        startupWorldDataInitialized = true;

        DashboardRefreshStatusText.Text = "Loading world data...";
        activeWorldContext.Invalidate();

        var results = await startupCoordinator.RunAsync(
        [
            ("players", async () => await RefreshPlayersAsync(silent: true)),
            ("guilds", () => { RefreshGuilds(); return Task.CompletedTask; }),
            ("bases", () => { RefreshBaseManager(); return Task.CompletedTask; }),
            ("guild/base recovery", () => { RefreshGuildBaseRecovery(); return Task.CompletedTask; }),
            ("dashboard intelligence", () => { RefreshDashboardIntelligence(); return Task.CompletedTask; })
        ], Log);

        var failed = results.Count(x => !x.Success);
        DashboardRefreshStatusText.Text = failed == 0
            ? $"World data loaded {DateTime.Now:HH:mm:ss}"
            : $"World data loaded with {failed} unavailable stage(s)";
        Log($"[STARTUP] World data initialized: {guildRows.Count} guild(s), {currentBaseManagerSummary?.Bases.Count ?? 0} base(s), {playerHistory.Snapshot().Count} known player(s). Failed stages: {failed}.");
    }

    private void DashboardOpenWorld_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage(14);
        InspectActiveWorld_Click(sender, e);
    }

    private void DashboardOpenMods_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage(7);
    }


    private void DashboardOpenPlayers_Click(object sender, RoutedEventArgs e) => NavigateToPage(4);
    private void DashboardOpenGuilds_Click(object sender, RoutedEventArgs e) => NavigateToPage(5);
    private void DashboardOpenBackups_Click(object sender, RoutedEventArgs e) => NavigateToPage(6);
    private void DashboardOpenDoctor_Click(object sender, RoutedEventArgs e) => NavigateToPage(10);

    private async void DashboardRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (dashboardRefreshInProgress) return;
        dashboardRefreshInProgress = true;
        DashboardRefreshStatusText.Text = "Refreshing dashboard intelligence...";
        try
        {
            await RefreshPlayersAsync(silent: true);
            RefreshGuilds();
            RefreshDashboardIntelligence();
            RefreshHistoricalAnalytics();
            DashboardRefreshStatusText.Text = $"Updated {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            DashboardRefreshStatusText.Text = "Refresh incomplete: " + ex.Message;
        }
        finally
        {
            dashboardRefreshInProgress = false;
        }
    }

    private void RefreshDashboardIntelligence()
    {
        var history = playerHistory.Snapshot();
        var online = history.Count(x => x.IsOnline);
        var saveFiles = history.Count(x => x.Source.Contains("save", StringComparison.OrdinalIgnoreCase));
        var orphaned = guildRows.Count(x => x.IsOrphaned);
        var modHealth = modPlatformHealth.Evaluate(modDashboardRows);
        var latestBackup = backups.List().FirstOrDefault();
        var recentBackup = latestBackup is not null && DateTime.Now - latestBackup.Created < TimeSpan.FromHours(24);
        var worldAvailable = !string.IsNullOrWhiteSpace(SaveInspector.FindActiveWorldPath());
        var snapshot = dashboardIntelligence.Build(online, history.Count, saveFiles, guildRows.Count, orphaned, modHealth, recentBackup, worldAvailable, dashboardLifecycleState);

        DashboardPlayersStateText.Text = $"{snapshot.OnlinePlayers} online / {snapshot.KnownPlayers} known";
        DashboardPlayersDetailText.Text = $"{snapshot.PlayerSaveFiles} player save record(s) discovered";
        DashboardGuildsStateText.Text = $"{snapshot.Guilds} guild(s)";
        DashboardGuildsDetailText.Text = snapshot.OrphanedGuilds == 0 ? "No orphaned guilds detected" : $"{snapshot.OrphanedGuilds} orphaned guild(s) need review";
        DashboardOverallHealthText.Text = $"{snapshot.HealthScore}% • {snapshot.HealthLabel}";
        DashboardOverallHealthDetailText.Text = snapshot.WarningCount == 0
            ? $"{snapshot.OperationalState} • no confirmed health issues"
            : $"{snapshot.OperationalState} • {snapshot.WarningCount} confirmed issue(s)";
        DashboardOverallHealthCard.ToolTip = BuildHealthBreakdownTooltip(snapshot);
        DashboardLastUpdatedText.Text = $"Last updated {snapshot.RefreshedUtc.ToLocalTime():HH:mm:ss}";
        RefreshWorldPulse(history);
        RefreshDashboardActivityTicker();
    }

    private void RefreshModernDashboard(ServerHealthSnapshot health, bool isInstalled)
    {
        dashboardLifecycleState = health.State;
        DashboardCommandStateText.Text = health.State switch
        {
            ServerLifecycleState.Running => "Server Online",
            ServerLifecycleState.Starting => "Server Starting",
            ServerLifecycleState.Stopping => "Server Stopping",
            ServerLifecycleState.Hung => "Server Needs Attention",
            ServerLifecycleState.Crashed => "Server Crashed",
            ServerLifecycleState.NotInstalled => "Server Not Installed",
            _ => "Server Offline"
        };
        DashboardCommandDetailText.Text = health.State == ServerLifecycleState.Running
            ? "Palworld is running under MystTiq management. Live monitoring is active."
            : health.Detail;

        var modHealth = modPlatformHealth.Evaluate(modDashboardRows);
        DashboardModStateText.Text = modHealth.Installed == 0 ? "No Mods" : $"{modHealth.Installed} Installed";
        DashboardModDetailText.Text = modHealth.Summary;

        var latestBackup = backups.List().FirstOrDefault();
        if (latestBackup is null)
        {
            DashboardBackupStateText.Text = "No Backup";
            DashboardBackupDetailText.Text = "Create a verified backup to protect the active server.";
            DashboardBackupStateText.Foreground = Brushes.Gold;
        }
        else
        {
            var age = DateTime.Now - latestBackup.Created;
            DashboardBackupStateText.Text = age.TotalHours < 24 ? "Protected" : "Backup Aging";
            DashboardBackupDetailText.Text = $"{latestBackup.Created:MMM d HH:mm} • {latestBackup.Status}";
            DashboardBackupStateText.Foreground = age.TotalHours < 24 ? Brushes.LightGreen : Brushes.Gold;
        }

        try
        {
            var worldPath = SaveInspector.FindActiveWorldPath();
            if (string.IsNullOrWhiteSpace(worldPath))
            {
                DashboardWorldNameText.Text = "No Active World";
                DashboardWorldDetailText.Text = isInstalled ? "Start the server or import a world." : "Install the server to create a world.";
                RefreshDashboardIntelligence();
                return;
            }

            var summary = SaveInspector.Inspect(worldPath);
            var worldHealth = SaveInspector.EvaluateHealth(summary);
            DashboardWorldNameText.Text = summary.WorldId;
            DashboardWorldDetailText.Text = $"{worldHealth.Score}% {worldHealth.Overall} • {summary.PlayerSaveCount} player(s) • {summary.SizeDisplay}";
        }
        catch (Exception ex)
        {
            DashboardWorldNameText.Text = "World Check Unavailable";
            DashboardWorldDetailText.Text = ex.Message;
        }

        RefreshDashboardIntelligence();
    }
    private void RefreshWorldPulse(IReadOnlyList<PlayerHistoryRecord> history)
    {
        try
        {
            var discovery = worldDiscovery.Current();
            var onlinePlayers = history
                .Where(x => x.IsOnline)
                .Select(x => new WorldTelemetryPlayer(
                    FirstNonBlank(x.Key, x.PlayerId, x.UserId, x.SteamId, x.Name),
                    FirstNonBlank(x.Name, x.UserId, x.SteamId, x.PlayerId)))
                .Where(x => !string.IsNullOrWhiteSpace(x.Key))
                .ToList();

            var latestBackup = backups.List().FirstOrDefault();
            var update = worldTelemetry.Update(
                server.ActiveSessionId,
                server.ActiveSessionStartedAt,
                onlinePlayers,
                discovery.DecodedJsonPath,
                discovery.Context.LevelLastWriteUtc,
                latestBackup?.Created);

            var pulse = update.Snapshot;
            DashboardUptimeText.Text = FormatDuration(pulse.SessionUptime);
            DashboardPulseUptimeText.Text = FormatDuration(pulse.SessionUptime);
            DashboardPulseSessionText.Text = pulse.SessionId > 0
                ? $"Session #{pulse.SessionId}"
                : "No active PalServer session";

            if (pulse.WorldClock.Available)
            {
                DashboardWorldClockText.Text = $"{pulse.WorldClock.DayDisplay} • {pulse.WorldClock.TimeDisplay}";
                var freshness = pulse.LastWorldSaveUtc.HasValue
                    ? FormatAge(DateTime.UtcNow - pulse.LastWorldSaveUtc.Value)
                    : "unknown";
                DashboardWorldClockDetailText.Text = $"Exact saved world clock • {freshness} fresh";
                DashboardWorldClockText.Foreground = Brushes.White;
            }
            else
            {
                DashboardWorldClockText.Text = "Day — • --:--";
                DashboardWorldClockDetailText.Text = "World clock unavailable — MystTiq will not estimate it";
                DashboardWorldClockText.Foreground = Brushes.Gold;
            }

            DashboardPulsePlayersText.Text = $"{pulse.OnlinePlayers} online • Peak {pulse.PeakPlayers}";
            DashboardPulsePlayerDetailText.Text =
                $"{pulse.SessionJoins} joins • {pulse.SessionLeaves} leaves • {pulse.UniquePlayers} unique";
            DashboardPulseLastEventText.Text = pulse.LastPlayerEvent;

            if (pulse.LastWorldSaveUtc.HasValue)
            {
                var saveAge = DateTime.UtcNow - pulse.LastWorldSaveUtc.Value;
                DashboardPulseSaveText.Text = $"{FormatAge(saveAge)} ago";
                DashboardPulseSaveText.Foreground = saveAge < TimeSpan.FromMinutes(10)
                    ? Brushes.LightGreen
                    : Brushes.Gold;
            }
            else
            {
                DashboardPulseSaveText.Text = "Unavailable";
                DashboardPulseSaveText.Foreground = Brushes.Gold;
            }

            DashboardPulseBackupText.Text = pulse.LastBackupLocal.HasValue
                ? $"Backup: {FormatAge(DateTime.Now - pulse.LastBackupLocal.Value)} ago"
                : "Backup: none detected";

            foreach (var worldEvent in update.Events)
            {
                var action = worldEvent.Kind switch
                {
                    "PlayerJoined" => "Player joined",
                    "PlayerLeft" => "Player left",
                    "WorldDayChanged" => "World day changed",
                    _ => worldEvent.Kind
                };
                RecordAudit("Information", "World Pulse", action,
                    $"{worldEvent.Summary} • {worldEvent.Detail}", MainPageIndex.Dashboard);
            }
        }
        catch (Exception ex)
        {
            DashboardWorldClockText.Text = "Day — • --:--";
            DashboardWorldClockDetailText.Text = "World telemetry unavailable: " + ex.Message;
            DashboardPulseUptimeText.Text = "00:00:00";
            DashboardPulseSessionText.Text = "Telemetry unavailable";
            DashboardPulsePlayersText.Text = "—";
            DashboardPulsePlayerDetailText.Text = "Session metrics unavailable";
            DashboardPulseSaveText.Text = "Unavailable";
            DashboardPulseBackupText.Text = "Backup: —";
            DashboardPulseLastEventText.Text = "Telemetry unavailable";
        }
    }

    private static string FirstNonBlank(params string[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string FormatDuration(TimeSpan value)
    {
        value = value < TimeSpan.Zero ? TimeSpan.Zero : value;
        return value.TotalDays >= 1
            ? $"{(int)value.TotalDays}d {value.Hours:00}h {value.Minutes:00}m"
            : value.ToString(@"hh\:mm\:ss");
    }

    private static string FormatAge(TimeSpan value)
    {
        if (value < TimeSpan.Zero) value = TimeSpan.Zero;
        if (value.TotalSeconds < 60) return $"{Math.Max(0, (int)value.TotalSeconds)}s";
        if (value.TotalMinutes < 60) return $"{(int)value.TotalMinutes}m";
        if (value.TotalHours < 24) return $"{(int)value.TotalHours}h {value.Minutes}m";
        return $"{(int)value.TotalDays}d {value.Hours}h";
    }

    private static string BuildHealthBreakdownTooltip(DashboardIntelligenceSnapshot snapshot)
    {
        var lines = new List<string>
        {
            "Overall Health",
            $"{snapshot.HealthScore}% • {snapshot.HealthLabel}",
            $"Operational State: {snapshot.OperationalState}",
            string.Empty
        };

        foreach (var signal in snapshot.Signals)
        {
            var marker = signal.Severity switch
            {
                DashboardHealthSeverity.Critical or DashboardHealthSeverity.Error or DashboardHealthSeverity.Warning => "!",
                DashboardHealthSeverity.Informational => "i",
                _ => "✓"
            };
            lines.Add($"{marker} {signal.Name}: {signal.Status}");
            lines.Add($"   {signal.Detail}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private void RefreshDashboardActivityTicker()
    {
        if (DashboardActivityTicker is null) return;
        DashboardActivityTicker.ItemsSource = auditEntries
            .OrderByDescending(x => x.TimestampUtc)
            .Take(4)
            .Select(x => $"{x.TimestampUtc.ToLocalTime():HH:mm:ss}  {x.Action} — {x.Details}")
            .ToList();
    }

    private static bool IsDashboardModHealthy(string? health)
    {
        if (string.IsNullOrWhiteSpace(health)) return false;
        return health.Equals("Healthy", StringComparison.OrdinalIgnoreCase);
    }

}
