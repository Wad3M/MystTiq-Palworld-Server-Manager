using Microsoft.Win32;
using PalworldManager.Models;
using PalworldManager.Services;

namespace PalworldManager;

public partial class MainWindow
{
    private GuildBaseRecoveryService? guildBaseRecoveryService;
    private GuildBaseRecoverySummary? currentGuildBaseRecovery;
    private GuildBaseRecoveryPlan? currentGuildBaseRecoveryPlan;
    private GuildBaseRecoveryService GuildBaseRecovery => guildBaseRecoveryService ??= new GuildBaseRecoveryService(settings);

    private void RefreshGuildBaseRecovery()
    {
        try
        {
            var world = SaveInspector.FindActiveWorldPath();
            if (string.IsNullOrWhiteSpace(world)) throw new DirectoryNotFoundException("No active world containing Level.sav was found.");
            currentGuildBaseRecovery = GuildBaseRecovery.Scan(world);
            currentGuildBaseRecoveryPlan = null;
            GuildBaseRecoveryWorldText.Text = world;
            GuildBaseRecoveryGrid.ItemsSource = currentGuildBaseRecovery.Findings;
            GuildBaseRecoveryPlanGrid.ItemsSource = null;
            GuildBaseRecoveryGuildsText.Text = currentGuildBaseRecovery.Guilds.Count.ToString();
            GuildBaseRecoveryOrphanedText.Text = currentGuildBaseRecovery.OrphanedGuildCount.ToString();
            GuildBaseRecoveryBasesText.Text = currentGuildBaseRecovery.BaseCount.ToString();
            GuildBaseRecoveryMissingPlayersText.Text = currentGuildBaseRecovery.MissingPlayerSaveCount.ToString();
            GuildBaseRecoveryCodecText.Text = currentGuildBaseRecovery.CodecAvailable ? "Configured" : "Preview only";
            GuildBaseRecoveryStatusText.Text = currentGuildBaseRecovery.Findings.Count == 0
                ? "No broken guild, membership or base relationships were discovered. A fresh world with no guilds is normal."
                : $"Scan completed with {currentGuildBaseRecovery.Findings.Count} recovery finding(s). Select only the changes you understand, then prepare a preview.";
            GuildBaseRecoveryStatusText.Foreground = currentGuildBaseRecovery.Findings.Count == 0 ? Brushes.LightGreen : Brushes.Gold;
        }
        catch (Exception ex)
        {
            GuildBaseRecoveryStatusText.Text = ex.Message;
            GuildBaseRecoveryStatusText.Foreground = Brushes.OrangeRed;
        }
    }

    private void GuildBaseRecoveryRefresh_Click(object sender, RoutedEventArgs e) => RefreshGuildBaseRecovery();

    private void GuildBaseRecoverySelectRecommended_Click(object sender, RoutedEventArgs e)
    {
        if (currentGuildBaseRecovery is null) RefreshGuildBaseRecovery();
        if (currentGuildBaseRecovery is null) return;
        foreach (var finding in currentGuildBaseRecovery.Findings)
            finding.IsSelected = !finding.Risk.Equals("High", StringComparison.OrdinalIgnoreCase);
        GuildBaseRecoveryGrid.Items.Refresh();
        GuildBaseRecoveryStatusText.Text = "Recommended low/medium-risk findings selected. High-risk base transfers remain unselected for manual review.";
        GuildBaseRecoveryStatusText.Foreground = Brushes.LightGreen;
    }

    private void GuildBaseRecoveryPrepare_Click(object sender, RoutedEventArgs e)
    {
        if (currentGuildBaseRecovery is null) RefreshGuildBaseRecovery();
        if (currentGuildBaseRecovery is null) return;
        currentGuildBaseRecoveryPlan = GuildBaseRecovery.BuildPlan(currentGuildBaseRecovery, currentGuildBaseRecovery.Findings.Where(f => f.IsSelected));
        GuildBaseRecoveryPlanGrid.ItemsSource = currentGuildBaseRecoveryPlan.Operations;
        GuildBaseRecoveryValidationText.Text = string.Join(Environment.NewLine, currentGuildBaseRecoveryPlan.ValidationMessages);
        GuildBaseRecoveryStatusText.Text = currentGuildBaseRecoveryPlan.Operations.Count == 0
            ? "No operations were selected."
            : $"Prepared a preview containing {currentGuildBaseRecoveryPlan.Operations.Count} operation(s). No save data was changed.";
        GuildBaseRecoveryStatusText.Foreground = currentGuildBaseRecoveryPlan.Operations.Count == 0 ? Brushes.Gold : Brushes.LightGreen;
    }

    private void GuildBaseRecoveryBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentGuildBaseRecovery is null) RefreshGuildBaseRecovery();
            if (currentGuildBaseRecovery is null) return;
            if (server.IsRunning()) throw new InvalidOperationException("Stop PalServer before creating a recovery safety backup.");
            var path = GuildBaseRecovery.CreateSafetyBackup(currentGuildBaseRecovery.WorldPath);
            GuildBaseRecoveryStatusText.Text = "Verified safety backup created: " + path;
            GuildBaseRecoveryStatusText.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex) { GuildBaseRecoveryStatusText.Text = ex.Message; GuildBaseRecoveryStatusText.Foreground = Brushes.OrangeRed; }
    }

    private void GuildBaseRecoverySavePlan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentGuildBaseRecoveryPlan is null) throw new InvalidOperationException("Prepare a recovery preview first.");
            if (!GuildBaseRecovery.VerifySourceUnchanged(currentGuildBaseRecoveryPlan)) throw new InvalidOperationException("Level.sav changed after the scan. Refresh and prepare the plan again.");
            var path = GuildBaseRecovery.SavePlan(currentGuildBaseRecoveryPlan);
            GuildBaseRecoveryStatusText.Text = "Recovery plan saved: " + path;
            GuildBaseRecoveryStatusText.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex) { GuildBaseRecoveryStatusText.Text = ex.Message; GuildBaseRecoveryStatusText.Foreground = Brushes.OrangeRed; }
    }

    private void GuildBaseRecoveryExport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentGuildBaseRecovery is null) RefreshGuildBaseRecovery();
            if (currentGuildBaseRecovery is null) return;
            var path = GuildBaseRecovery.ExportReport(currentGuildBaseRecovery, currentGuildBaseRecoveryPlan);
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            GuildBaseRecoveryStatusText.Text = "Recovery report exported: " + path;
            GuildBaseRecoveryStatusText.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex) { GuildBaseRecoveryStatusText.Text = ex.Message; GuildBaseRecoveryStatusText.Foreground = Brushes.OrangeRed; }
    }

    private void GuildBaseRecoveryOpenGuilds_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage(5);
        RefreshGuilds();
    }
}
