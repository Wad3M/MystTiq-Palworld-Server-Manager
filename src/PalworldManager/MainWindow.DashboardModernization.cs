using PalworldManager.Models;
using PalworldManager.Services;
using System.Windows.Media;

namespace PalworldManager;

public partial class MainWindow
{
    private readonly DashboardIntelligenceService dashboardIntelligence = new();
    private bool dashboardRefreshInProgress;
    private ServerLifecycleState dashboardLifecycleState = ServerLifecycleState.Stopped;
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
        var healthyMods = modDashboardRows.Count(x => IsDashboardModHealthy(x.Health));
        var latestBackup = backups.List().FirstOrDefault();
        var recentBackup = latestBackup is not null && DateTime.Now - latestBackup.Created < TimeSpan.FromHours(24);
        var worldAvailable = !string.IsNullOrWhiteSpace(SaveInspector.FindActiveWorldPath());
        var snapshot = dashboardIntelligence.Build(online, history.Count, saveFiles, guildRows.Count, orphaned, modDashboardRows.Count, healthyMods, recentBackup, worldAvailable, dashboardLifecycleState);

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
        DashboardUptimeText.Text = (DateTime.UtcNow - managerStartedUtc).ToString(@"hh\:mm\:ss");

        DashboardModStateText.Text = modDashboardRows.Count == 0 ? "No Mods" : $"{modDashboardRows.Count} Installed";
        var healthyMods = modDashboardRows.Count(x => IsDashboardModHealthy(x.Health));
        DashboardModDetailText.Text = modDashboardRows.Count == 0
            ? "Vanilla server profile"
            : $"{healthyMods} healthy • {modDashboardRows.Count - healthyMods} need review";

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
            lines.Add($"{(signal.IsWarning ? "!" : "✓")} {signal.Name}: {signal.Status}");
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
        return health.Equals("Healthy", StringComparison.OrdinalIgnoreCase)
            || health.Equals("Active", StringComparison.OrdinalIgnoreCase)
            || health.Equals("Installed", StringComparison.OrdinalIgnoreCase)
            || health.Equals("Disabled", StringComparison.OrdinalIgnoreCase);
    }

}
