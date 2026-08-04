using PalworldManager.Models;
using PalworldManager.Services;

namespace PalworldManager;

public partial class MainWindow
{
    private SaveInspectorService? saveInspectorService;
    private SaveInspectorSummary? currentSaveInspection;
    private SaveInspectorService SaveInspector => saveInspectorService ??= new SaveInspectorService(settings, activeWorldContext);


    // v2.11.8.3: entering World Inspector is equivalent to clicking Inspect Active World.
    private void Tabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Nested TabControls bubble SelectionChanged through the main window.
        // Only react when the main page TabControl itself changed.
        if (!ReferenceEquals(e.Source, Tabs) || Tabs.SelectedIndex != MainPageIndex.WorldInspector)
            return;

        InspectActiveWorld_Click(Tabs, new RoutedEventArgs());
    }

    private void BrowseSaveInspector_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Level.sav",
            Filter = "Palworld level save (Level.sav)|Level.sav|Palworld saves (*.sav)|*.sav|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            SaveInspectorPathBox.Text = dialog.FileName;
            InspectSelectedSave();
        }
    }

    private void InspectActiveWorld_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var active = activeWorldContext.Current(forceRefresh: true).WorldPath;
            if (string.IsNullOrWhiteSpace(active)) throw new DirectoryNotFoundException("No active Palworld world containing Level.sav was found under the configured SaveGames folder.");
            SaveInspectorPathBox.Text = active;
            InspectSelectedSave();
        }
        catch (Exception ex) { ShowSaveInspectorError(ex); }
    }

    private void InspectSelectedSave_Click(object sender, RoutedEventArgs e) => InspectSelectedSave();

    private void InspectSelectedSave()
    {
        try
        {
            currentSaveInspection = SaveInspector.Inspect(SaveInspectorPathBox.Text);
            SaveInspectorWorldIdText.Text = currentSaveInspection.WorldId;
            SaveInspectorContainerText.Text = currentSaveInspection.ContainerDisplay;
            SaveInspectorPlayersText.Text = currentSaveInspection.PlayerSaveCount.ToString();
            SaveInspectorSizeText.Text = currentSaveInspection.SizeDisplay;
            SaveInspectorLevelPathText.Text = currentSaveInspection.LevelSavePath;
            SaveInspectorHeaderText.Text = $"Magic: {currentSaveInspection.Header.MagicText} ({currentSaveInspection.Header.MagicHex})  •  Level size: {currentSaveInspection.LevelSizeDisplay}  •  Updated: {currentSaveInspection.LastWriteDisplay}";
            SaveInspectorCodecText.Text = currentSaveInspection.CodecStatus;
            SaveInspectorTotalFilesText.Text = currentSaveInspection.TotalFileCount.ToString();
            SaveInspectorLiveFilesText.Text = currentSaveInspection.LiveFileCount.ToString();
            SaveInspectorBackupFilesText.Text = currentSaveInspection.BackupFileCount.ToString();
            SaveInspectorLatestWriteText.Text = currentSaveInspection.LatestFileWriteDisplay;
            SaveInspectorLargestFileText.Text = currentSaveInspection.LargestFileDisplay;
            SaveInspectorRequiredFilesText.Text = currentSaveInspection.RequiredFileCount.ToString();
            SaveInspectorOptionalFilesText.Text = currentSaveInspection.OptionalFileCount.ToString();
            SaveInspectorUnknownFilesText.Text = currentSaveInspection.UnknownFileCount.ToString();
            PopulateConsolidatedInspectorViews(currentSaveInspection);
            SaveInspectorCategoryFilter.ItemsSource = SaveInspector.GetFileCategories(currentSaveInspection);
            SaveInspectorCategoryFilter.SelectedIndex = 0;
            SaveInspectorFileSearchBox.Text = "";
            ApplySaveInspectorFileFilter();
            PopulateSaveExplorer(SaveInspector.BuildExplorer(currentSaveInspection));
            var health = SaveInspector.EvaluateHealth(currentSaveInspection);
            SaveInspectorHealthScoreText.Text = health.Score + "%";
            SaveInspectorHealthOverallText.Text = health.Overall;
            SaveInspectorHealthCountsText.Text = $"Healthy checks: {health.HealthyCount}   Warnings: {health.WarningCount}   Errors: {health.ErrorCount}";
            SaveInspectorHealthFindings.ItemsSource = health.Findings;
            SaveInspectorIntegrityGrid.ItemsSource = SaveInspector.AnalyzeIntegrity(currentSaveInspection);
            SaveInspectorRepairGrid.ItemsSource = SaveInspector.BuildRepairSuggestions(currentSaveInspection);
            SaveInspectorWarningText.Text = currentSaveInspection.Warnings.Count == 0
                ? "No file-level warnings were detected. The world passed file-level checks. Decoded entity inspection remains available when a codec is configured."
                : string.Join(Environment.NewLine, currentSaveInspection.Warnings.Select(w => "• " + w));
            SaveInspectorWarningText.Foreground = currentSaveInspection.Warnings.Count == 0 ? Brushes.LightGreen : Brushes.Gold;
            SaveInspectorStatusText.Text = $"Inspection complete: {currentSaveInspection.PlayerSaveCount} player save(s), {currentSaveInspection.DerivedPlayerFileCount} derived file(s), {currentSaveInspection.Files.Count} total file(s). No save data was modified.";
            SaveInspectorStatusText.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex) { ShowSaveInspectorError(ex); }
    }

    private void PopulateConsolidatedInspectorViews(SaveInspectorSummary summary)
    {
        InspectorSaveWorldFolderText.Text = summary.WorldPath;
        InspectorSaveLevelText.Text = summary.LevelSavePath;
        InspectorSaveContainerDetailText.Text = summary.ContainerDisplay;
        InspectorSaveLevelSizeText.Text = summary.LevelSizeDisplay;
        InspectorSaveModifiedText.Text = summary.LastWriteDisplay;
        InspectorSaveCodecStateText.Text = summary.CodecStatus;
        InspectorSaveHeaderDetailText.Text =
            $"Magic: {summary.Header.MagicText} ({summary.Header.MagicHex}){Environment.NewLine}" +
            $"Container: {summary.ContainerDisplay}{Environment.NewLine}" +
            $"World ID: {summary.WorldId}";
        InspectorSaveInventoryText.Text =
            $"Total: {summary.TotalFileCount}  •  Live: {summary.LiveFileCount}  •  Backups: {summary.BackupFileCount}{Environment.NewLine}" +
            $"Required: {summary.RequiredFileCount}  •  Optional: {summary.OptionalFileCount}  •  Derived player files: {summary.DerivedPlayerFileCount}  •  Unclassified: {summary.UnknownFileCount}";

        InspectorStatsPlayersText.Text = summary.PlayerSaveCount.ToString();
        InspectorStatsDerivedText.Text = summary.DerivedPlayerFileCount.ToString();
        InspectorStatsLiveText.Text = summary.LiveFileCount.ToString();
        InspectorStatsBackupsText.Text = summary.BackupFileCount.ToString();
        InspectorStatsRequiredText.Text = summary.RequiredFileCount.ToString();
        InspectorStatsOptionalText.Text = summary.OptionalFileCount.ToString();
        InspectorStatsUnknownText.Text = summary.UnknownFileCount.ToString();
        InspectorStatsSizeText.Text = summary.SizeDisplay;

        var oldest = summary.OldestFileWriteUtc == default
            ? "Unknown"
            : summary.OldestFileWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        InspectorStatsAgeRangeText.Text =
            $"Oldest file: {oldest}{Environment.NewLine}" +
            $"Newest file: {summary.LatestFileWriteDisplay}";
        InspectorStatsSummaryText.Text =
            $"{summary.PlayerSaveCount} player save(s), {summary.TotalFileCount} total file(s), {summary.SizeDisplay} total size.{Environment.NewLine}" +
            $"Largest file: {summary.LargestFileDisplay}";
    }

    private void SaveInspectorFileSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplySaveInspectorFileFilter();

    private void SaveInspectorCategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplySaveInspectorFileFilter();

    private void ApplySaveInspectorFileFilter()
    {
        if (currentSaveInspection is null || SaveInspectorFilesGrid is null) return;
        var category = SaveInspectorCategoryFilter?.SelectedItem?.ToString();
        var query = SaveInspectorFileSearchBox?.Text;
        var rows = SaveInspector.FilterFiles(currentSaveInspection, query, category);
        SaveInspectorFilesGrid.ItemsSource = rows;
        if (SaveInspectorFilteredFilesText is not null)
            SaveInspectorFilteredFilesText.Text = $"Showing {rows.Count} of {currentSaveInspection.TotalFileCount} files";
    }

    private void SaveInspectorFilesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SaveInspectorFilesGrid.SelectedItem is not SaveInspectorFileRow row) return;
        SaveInspectorSelectedFileText.Text =
            $"FILE: {row.RelativePath}{Environment.NewLine}" +
            $"CATEGORY: {row.Category}{Environment.NewLine}" +
            $"STATUS: {row.Status}{Environment.NewLine}" +
            $"SIZE: {row.SizeDisplay} ({row.SizeBytes:N0} bytes){Environment.NewLine}" +
            $"UPDATED: {row.LastWriteDisplay}{Environment.NewLine}" +
            $"ROLE: {(row.IsRequired ? "Required" : row.IsOptional ? "Optional" : row.IsBackup ? "Backup" : row.IsDerived ? "Derived" : "Supporting")}";
    }

    private void PopulateSaveExplorer(IEnumerable<SaveExplorerNode> nodes)
    {
        SaveInspectorExplorerTree.Items.Clear();
        foreach (var node in nodes) SaveInspectorExplorerTree.Items.Add(CreateExplorerItem(node));
    }

    private static TreeViewItem CreateExplorerItem(SaveExplorerNode node)
    {
        var item = new TreeViewItem { Header = node.Display, Tag = node };
        foreach (var child in node.Children) item.Items.Add(CreateExplorerItem(child));
        return item;
    }

    private void SaveInspectorExplorerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item && item.Tag is SaveExplorerNode node)
            SaveInspectorEntityDetailText.Text = $"TYPE: {node.Kind}\nNAME: {node.Name}\nDETAIL: {node.Detail}\nSOURCE: {node.SourcePath}";
    }

    private void RefreshSaveInspector_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(SaveInspectorPathBox.Text)) InspectActiveWorld_Click(sender, e);
        else InspectSelectedSave();
    }

    private void ExportSaveDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentSaveInspection is null) throw new InvalidOperationException("Inspect a world first.");
            var dialog = new Microsoft.Win32.SaveFileDialog { Title = "Export Save Diagnostics", Filter = "Text report (*.txt)|*.txt", FileName = $"Myst_WorldDiagnostics_{currentSaveInspection.WorldId}_{DateTime.Now:yyyyMMdd_HHmm}.txt" };
            if (dialog.ShowDialog() != true) return;
            File.WriteAllText(dialog.FileName, SaveInspector.BuildDiagnosticsReport(currentSaveInspection));
            SaveInspectorStatusText.Text = $"Diagnostics report exported: {dialog.FileName}";
            SaveInspectorStatusText.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex) { ShowSaveInspectorError(ex); }
    }

    private void PreviewSelectedRepairs_Click(object sender, RoutedEventArgs e)
    {
        if (currentSaveInspection is null) { ShowSaveInspectorError(new InvalidOperationException("Inspect a world first.")); return; }
        var selected = SaveInspectorRepairGrid.Items.Cast<SaveRepairSuggestion>().Where(x => x.Selected).ToList();
        SaveInspectorRepairStatusText.Text = selected.Count == 0 ? "No repair suggestions selected. Nothing will be changed." : $"Preview ready for {selected.Count} selected action(s). This release does not write save data.";
    }

    private void OpenInspectedWorldFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = currentSaveInspection?.WorldPath;
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) throw new DirectoryNotFoundException("Inspect a world first.");
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex) { ShowSaveInspectorError(ex); }
    }

    private void ShowSaveInspectorError(Exception ex)
    {
        SaveInspectorStatusText.Text = ex.Message;
        SaveInspectorStatusText.Foreground = Brushes.OrangeRed;
        MessageBox.Show(ex.Message, "World Inspector", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

// v2.11.8: world-backed pages share one active-world context and invalidate together.
public partial class MainWindow
{
    private void ActiveWorldContext_Changed(object? sender, ActiveWorldContext context)
    {
        Dispatcher.BeginInvoke(() =>
        {
            playerHistory.DiscoverWorldPlayerSaves(context.WorldPath);
            scanCache.Invalidate("players.live");
            currentGuildSnapshot = null;
            currentSaveInspection = null;
            if (Tabs.SelectedIndex == MainPageIndex.WorldInspector) AutoLoadActiveWorldInspector();
        });
    }

    private void AutoLoadActiveWorldInspector()
    {
        try
        {
            var context = activeWorldContext.Current();
            if (!context.IsResolved)
            {
                SaveInspectorStatusText.Text = "World status: NOT FOUND. Manual Browse remains available.";
                SaveInspectorStatusText.Foreground = Brushes.Gold;
                return;
            }
            SaveInspectorPathBox.Text = context.LevelSavePath;
            InspectSelectedSave();
            SaveInspectorStatusText.Text = $"World status: {context.Status} • Active Level.sav: {context.LevelSavePath}";
        }
        catch (Exception ex) { ShowSaveInspectorError(ex); }
    }
}
