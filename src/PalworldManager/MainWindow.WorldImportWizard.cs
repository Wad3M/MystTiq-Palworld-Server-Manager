using PalworldManager.Models;
using PalworldManager.Services;

namespace PalworldManager;

public partial class MainWindow
{
    private string? worldImportArchivePath;
    private WorldImportScanResult? worldImportScan;
    private WorldImportPlan? worldImportPlan;
    private WorldImportResult? worldImportResult;

    private void SetWorldImportStepState(int availableThrough, int completedThrough = 0)
    {
        var cards = new[]
        {
            WorldImportStep1Card, WorldImportStep2Card, WorldImportStep3Card,
            WorldImportStep4Card, WorldImportStep5Card, WorldImportStep6Card, WorldImportStep7Card
        };
        for (var i = 0; i < cards.Length; i++)
        {
            var step = i + 1;
            cards[i].IsEnabled = step <= availableThrough;
            cards[i].Opacity = step <= availableThrough ? 1.0 : 0.45;
            cards[i].BorderBrush = step <= completedThrough
                ? new SolidColorBrush(Color.FromRgb(39, 174, 96))
                : step == availableThrough
                    ? new SolidColorBrush(Color.FromRgb(74, 122, 162))
                    : new SolidColorBrush(Color.FromRgb(38, 58, 80));
        }
        WorldImportStatusBannerText.Text = availableThrough switch
        {
            <= 1 => "Status: Waiting for a source archive.  Next step: Select a ZIP archive to begin.",
            2 when completedThrough < 2 => "Status: Source selected.  Next step: Analyze the archive structure and player saves.",
            3 when completedThrough < 3 => "Status: Analysis complete.  Next step: Choose the destination and safety options.",
            4 when completedThrough < 4 => "Status: Options accepted.  Next step: Review the complete migration plan.",
            5 when completedThrough < 5 => "Status: Plan reviewed.  Next step: Run final safety validation.",
            6 when completedThrough < 6 => "Status: Validation passed.  Next step: Stop PalServer, then import and activate.",
            _ when completedThrough >= 6 => "Status: Migration completed.  Next step: Verify the imported world and review refreshed world data.",
            _ => "Status: Migration workflow ready.  Complete the active blue step to continue."
        };
    }

    private void ResetWorldImportWizard(bool preserveArchive = false)
    {
        if (!preserveArchive) worldImportArchivePath = null;
        worldImportScan = null;
        worldImportPlan = null;
        worldImportResult = null;
        WorldImportArchiveText.Text = preserveArchive && !string.IsNullOrWhiteSpace(worldImportArchivePath)
            ? worldImportArchivePath
            : "Choose a .zip archive containing Level.sav and optional Players data.";
        WorldImportAnalysisText.Text = "Archive layout, required files, player saves, blocked entries, and warnings will appear here.";
        WorldImportPlanText.Text = "Review the source, destination, player-save count, backup choice, and WorldOption handling.";
        WorldImportValidationText.Text = "MystTiq will verify the archive, destination, server state, save root, and backup location.";
        WorldImportResultText.Text = "Import results, backup path, manifest, and destination will appear here.";
        SetWorldImportStepState(preserveArchive ? 2 : 1);
    }

    private void WorldImportSelectArchive_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Palworld World Archive",
            Filter = "ZIP archives (*.zip)|*.zip",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        worldImportArchivePath = dialog.FileName;
        ResetWorldImportWizard(preserveArchive: true);
        WorldImportArchiveText.Text = $"Selected: {worldImportArchivePath}";
        SetWorldImportStepState(2, 1);
    }

    private void WorldImportAnalyze_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(worldImportArchivePath)) return;
        try
        {
            var importer = new WorldImportService(settings);
            worldImportScan = importer.Scan(worldImportArchivePath);
            var warningText = worldImportScan.Warnings.Count == 0
                ? "No archive warnings."
                : string.Join(" • ", worldImportScan.Warnings);
            WorldImportAnalysisText.Text = $"{worldImportScan.Summary}\nReadiness: {worldImportScan.Readiness}\nInstallable entries: {worldImportScan.InstallableEntryCount:N0}\n{warningText}";
            if (!worldImportScan.IsValid)
            {
                SetWorldImportStepState(2, 1);
                AppDialog.Show("The archive is not ready for import. Review the analysis and correct the blocked or missing content.", "World Import", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SetWorldImportStepState(3, 2);
        }
        catch (Exception ex)
        {
            WorldImportAnalysisText.Text = "Analysis failed: " + ex.Message;
            SetWorldImportStepState(2, 1);
            AppDialog.Show("MystTiq could not analyze the selected archive.\n\n" + ex.Message, "World Import", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void WorldImportAcceptOptions_Click(object sender, RoutedEventArgs e)
    {
        if (worldImportScan?.IsValid != true) return;
        var mode = (WorldImportWorldOptionModeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Quarantine";
        worldImportPlan = new WorldImportPlan
        {
            ArchivePath = worldImportScan.ArchivePath,
            DestinationWorldId = WorldImportDestinationIdBox.Text.Trim(),
            CreateBackup = WorldImportBackupCheck.IsChecked == true,
            WorldOptionMode = Enum.TryParse<WorldOptionImportMode>(mode, true, out var parsed) ? parsed : WorldOptionImportMode.Quarantine,
            ValidateAfterExtraction = true,
            OpenGuildRecovery = true
        };
        WorldImportPlanText.Text = BuildWorldImportPlanSummary();
        SetWorldImportStepState(4, 3);
    }

    private string BuildWorldImportPlanSummary()
    {
        if (worldImportScan is null || worldImportPlan is null) return "No import plan is available.";
        var destination = string.IsNullOrWhiteSpace(worldImportPlan.DestinationWorldId) ? "Generate a new world ID" : worldImportPlan.DestinationWorldId;
        return $"Source: {Path.GetFileName(worldImportScan.ArchivePath)}\nDestination: {destination}\nPlayers: {worldImportScan.PlayerSaveCount}\nWorld profile: {worldImportScan.WorldProfile}\nWorldOption.sav: {worldImportPlan.WorldOptionMode}\nPre-import backup: {(worldImportPlan.CreateBackup ? "Yes" : "No")}";
    }

    private void WorldImportReviewPlan_Click(object sender, RoutedEventArgs e)
    {
        if (worldImportPlan is null) return;
        WorldImportPlanText.Text = BuildWorldImportPlanSummary() + "\n\nPlayer-save validation and guild/base recovery data will be refreshed after activation.";
        SetWorldImportStepState(5, 4);
    }

    private void WorldImportValidate_Click(object sender, RoutedEventArgs e)
    {
        if (worldImportScan?.IsValid != true || worldImportPlan is null) return;
        var issues = new List<string>();
        if (server.IsRunning()) issues.Add("PalServer is running.");
        if (string.IsNullOrWhiteSpace(settings.SaveRoot)) issues.Add("Save Root is not configured.");
        else
        {
            try { Directory.CreateDirectory(settings.SaveRoot); }
            catch (Exception ex) { issues.Add("Save Root is unavailable: " + ex.Message); }
        }
        if (worldImportPlan.CreateBackup)
        {
            try { Directory.CreateDirectory(settings.BackupRoot); }
            catch (Exception ex) { issues.Add("Backup Root is unavailable: " + ex.Message); }
        }
        if (issues.Count > 0)
        {
            WorldImportValidationText.Text = "Blocked:\n• " + string.Join("\n• ", issues);
            SetWorldImportStepState(5, 4);
            return;
        }
        WorldImportValidationText.Text = "Passed: archive valid, server stopped, save root available, and backup root available.";
        SetWorldImportStepState(6, 5);
    }

    private void WorldImportApply_Click(object sender, RoutedEventArgs e)
    {
        if (worldImportScan?.IsValid != true || worldImportPlan is null) return;
        if (server.IsRunning())
        {
            AppDialog.Show("Stop PalServer before importing and activating a world.", "World Import", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var summary = BuildWorldImportPlanSummary();
        if (AppDialog.Show(summary + "\n\nThis will install and activate the imported world. Continue?", "Confirm World Import", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        try
        {
            var importer = new WorldImportService(settings);
            worldImportResult = importer.Install(worldImportScan, worldImportPlan, serverRunning: false);
            var activationMarker = importer.Activate(worldImportResult.DestinationWorldPath, serverRunning: false);
            WorldImportResultText.Text = $"{worldImportResult.Message}\nDestination: {worldImportResult.DestinationWorldPath}\nBackup: {(string.IsNullOrWhiteSpace(worldImportResult.BackupPath) ? "Not requested" : worldImportResult.BackupPath)}\nManifest: {worldImportResult.ManifestPath}\nActivation: {activationMarker}";
            SetWorldImportStepState(7, 7);
            Log($"[WORLD IMPORT] Imported and activated {worldImportResult.DestinationWorldPath}");
            _ = InitializeWorldDataOnStartupAsync();
        }
        catch (Exception ex)
        {
            WorldImportResultText.Text = "Import failed: " + ex.Message;
            SetWorldImportStepState(6, 5);
            AppDialog.Show("The world import did not complete.\n\n" + ex.Message, "World Import", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void WorldImportOpenResult_Click(object sender, RoutedEventArgs e)
    {
        if (worldImportResult is null || string.IsNullOrWhiteSpace(worldImportResult.DestinationWorldPath) || !Directory.Exists(worldImportResult.DestinationWorldPath)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", worldImportResult.DestinationWorldPath) { UseShellExecute = true });
    }
}
