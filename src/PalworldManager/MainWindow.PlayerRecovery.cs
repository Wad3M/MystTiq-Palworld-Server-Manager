using Microsoft.Win32;
using PalworldManager.Models;
using PalworldManager.Services;

namespace PalworldManager;

public partial class MainWindow
{
    private PlayerRecoveryService? playerRecoveryService;
    private PlayerRecoverySummary? currentPlayerRecovery;
    private PlayerRecoveryPlan? currentPlayerRecoveryPlan;
    private PlayerRecoveryService PlayerRecovery => playerRecoveryService ??= new PlayerRecoveryService(settings, playerHistory);

    private void RefreshPlayerRecovery()
    {
        try
        {
            var world = SaveInspector.FindActiveWorldPath();
            if (string.IsNullOrWhiteSpace(world))
                throw new DirectoryNotFoundException("No active world containing Level.sav was found.");
            PlayerRecoveryWorldPathText.Text = world;
            currentPlayerRecovery = PlayerRecovery.Scan(world);
            PlayerRecoveryGrid.ItemsSource = currentPlayerRecovery.Players;
            PlayerRecoveryTotalText.Text = currentPlayerRecovery.Players.Count.ToString();
            PlayerRecoveryHostText.Text = currentPlayerRecovery.HostCandidateCount.ToString();
            PlayerRecoveryMappedText.Text = currentPlayerRecovery.MappedCount.ToString();
            PlayerRecoveryUnmappedText.Text = currentPlayerRecovery.UnmappedCount.ToString();
            PlayerRecoveryCodecText.Text = PlayerRecovery.CodecAvailable ? "Configured" : "Not configured";
            PlayerRecoveryStatusText.Text = currentPlayerRecovery.Players.Count == 0
                ? "No primary player saves were found. Derived _dps files are excluded."
                : $"Scan complete: {currentPlayerRecovery.Players.Count} primary player save(s), {currentPlayerRecovery.TotalSizeDisplay} total.";
            PlayerRecoveryStatusText.Foreground = Brushes.LightGreen;
            PlayerRecoveryDetailsText.Text = "Select a player save to review recovery options.";
            currentPlayerRecoveryPlan = null;
            PlayerRecoveryPlanGrid.ItemsSource = null;
        }
        catch (Exception ex)
        {
            PlayerRecoveryStatusText.Text = ex.Message;
            PlayerRecoveryStatusText.Foreground = Brushes.OrangeRed;
        }
    }

    private void PlayerRecoveryRefresh_Click(object sender, RoutedEventArgs e) => RefreshPlayerRecovery();

    private void PlayerRecoverySelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (PlayerRecoveryGrid.SelectedItem is not PlayerRecoveryRow row)
        {
            PlayerRecoveryDetailsText.Text = "Select a player save to review recovery options.";
            return;
        }
        PlayerRecoveryDestinationGuidBox.Text = row.IsHostCandidate ? "" : row.PlayerGuid;
        PlayerRecoveryDetailsText.Text = $"Player: {row.DisplayName}\nGUID: {row.PlayerGuid}\nStatus: {row.Status}\nSave: {row.SavePath}\nCompanion: {(row.HasCompanion ? row.CompanionPath : "None")}\nLast updated: {row.LastWriteDisplay}";
    }

    private void PlayerRecoveryPreview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentPlayerRecovery is null || PlayerRecoveryGrid.SelectedItem is not PlayerRecoveryRow row)
                throw new InvalidOperationException("Select a player save first.");
            currentPlayerRecoveryPlan = PlayerRecovery.BuildPlan(row, PlayerRecoveryDestinationGuidBox.Text, currentPlayerRecovery.WorldPath);
            PlayerRecoveryPlanGrid.ItemsSource = new[]
            {
                new { Check = "Source player", Result = currentPlayerRecoveryPlan.SourcePlayerGuid, Status = File.Exists(currentPlayerRecoveryPlan.SourceSavePath) ? "Ready" : "Missing" },
                new { Check = "Destination player", Result = currentPlayerRecoveryPlan.DestinationPlayerGuid, Status = currentPlayerRecoveryPlan.DestinationExists ? "Conflict" : "Available" },
                new { Check = "Migration type", Result = currentPlayerRecoveryPlan.MappingMethod, Status = currentPlayerRecoveryPlan.SourceIsHostCandidate ? "Host" : "Manual" },
                new { Check = "Save codec", Result = currentPlayerRecoveryPlan.CodecAvailable ? "Configured" : "Not configured", Status = currentPlayerRecoveryPlan.CodecAvailable ? "Ready" : "Plan only" },
                new { Check = "Level.sav references", Result = "Coordinated rewrite required", Status = "Required" }
            };
            PlayerRecoveryStatusText.Text = string.Join("  ", currentPlayerRecoveryPlan.ValidationMessages);
            PlayerRecoveryStatusText.Foreground = currentPlayerRecoveryPlan.ValidationMessages.Any(x => x.Contains("missing", StringComparison.OrdinalIgnoreCase) || x.Contains("already exists", StringComparison.OrdinalIgnoreCase)) ? Brushes.OrangeRed : Brushes.Gold;
        }
        catch (Exception ex)
        {
            PlayerRecoveryStatusText.Text = ex.Message;
            PlayerRecoveryStatusText.Foreground = Brushes.OrangeRed;
        }
    }

    private void PlayerRecoveryBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentPlayerRecovery is null || PlayerRecoveryGrid.SelectedItem is not PlayerRecoveryRow row)
                throw new InvalidOperationException("Select a player save first.");
            var path = PlayerRecovery.CreateSafetyBackup(row, currentPlayerRecovery.WorldPath);
            PlayerRecoveryStatusText.Text = "Safety backup created: " + path;
            PlayerRecoveryStatusText.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex) { PlayerRecoveryStatusText.Text = ex.Message; PlayerRecoveryStatusText.Foreground = Brushes.OrangeRed; }
    }

    private void PlayerRecoveryExport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentPlayerRecovery is null || PlayerRecoveryGrid.SelectedItem is not PlayerRecoveryRow row)
                throw new InvalidOperationException("Select a player save first.");
            var dialog = new SaveFileDialog
            {
                Title = "Export player recovery package",
                Filter = "ZIP archive (*.zip)|*.zip",
                FileName = $"Myst_Player_{row.PlayerGuid}_{DateTime.Now:yyyyMMdd_HHmm}.zip"
            };
            if (dialog.ShowDialog() != true) return;
            PlayerRecovery.ExportPlayerPackage(row, currentPlayerRecovery.WorldPath, dialog.FileName);
            PlayerRecoveryStatusText.Text = "Player recovery package exported: " + dialog.FileName;
            PlayerRecoveryStatusText.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex) { PlayerRecoveryStatusText.Text = ex.Message; PlayerRecoveryStatusText.Foreground = Brushes.OrangeRed; }
    }

    private void PlayerRecoverySavePlan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentPlayerRecoveryPlan is null)
                throw new InvalidOperationException("Preview a recovery mapping first.");
            var path = PlayerRecovery.SavePlan(currentPlayerRecoveryPlan);
            PlayerRecoveryStatusText.Text = "Recovery plan saved: " + path;
            PlayerRecoveryStatusText.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex) { PlayerRecoveryStatusText.Text = ex.Message; PlayerRecoveryStatusText.Foreground = Brushes.OrangeRed; }
    }

    private void PlayerRecoveryOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = currentPlayerRecovery?.WorldPath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{Path.Combine(path, "Level.sav")}\"") { UseShellExecute = true });
    }
}
