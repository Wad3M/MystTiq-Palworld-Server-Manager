using PalworldManager.Models;
using PalworldManager.Services;

namespace PalworldManager;

public partial class MainWindow
{
    private BaseManagerService? baseManagerService;
    private BaseManagerSummary? currentBaseManagerSummary;
    private List<BaseManagerRow> baseManagerView = [];
    private OwnershipPreview? currentOwnershipPreview;
    private BaseManagerService BaseManager => baseManagerService ??= new BaseManagerService(settings, activeWorldContext, worldDiscovery);

    private void BaseManagerRefresh_Click(object sender, RoutedEventArgs e) => RefreshBaseManager();

    private void RefreshBaseManager()
    {
        try
        {
            var world = worldDiscovery.Current(forceRefresh: true).Context.WorldPath;
            if (string.IsNullOrWhiteSpace(world)) throw new DirectoryNotFoundException("No active world containing Level.sav was found.");
            currentBaseManagerSummary = BaseManager.Scan(world);
            BaseManagerWorldText.Text = world;
            BaseManagerTotalText.Text = currentBaseManagerSummary.Bases.Count.ToString();
            BaseManagerHealthyText.Text = currentBaseManagerSummary.HealthyCount.ToString();
            BaseManagerOrphanedText.Text = currentBaseManagerSummary.OrphanedCount.ToString();
            BaseManagerPalboxText.Text = currentBaseManagerSummary.PalboxCount.ToString();
            BaseManagerGuildsText.Text = currentBaseManagerSummary.GuildCount.ToString();
            ApplyBaseManagerFilter();
            BaseManagerStatusText.Text = currentBaseManagerSummary.Bases.Count == 0
                ? currentBaseManagerSummary.Warnings.LastOrDefault() ?? "No bases were discovered."
                : $"Discovered {currentBaseManagerSummary.Bases.Count} base(s). Ownership changes are preview-first and transactional in v0.2.12.";
            BaseManagerStatusText.Foreground = currentBaseManagerSummary.OrphanedCount > 0 ? Brushes.Gold : Brushes.LightGreen;
        }
        catch (Exception ex) { BaseManagerStatusText.Text = ex.Message; BaseManagerStatusText.Foreground = Brushes.OrangeRed; }
    }

    private void BaseManagerFilter_Changed(object sender, RoutedEventArgs e) => ApplyBaseManagerFilter();
    private void BaseManagerSearch_TextChanged(object sender, TextChangedEventArgs e) => ApplyBaseManagerFilter();

    private void ApplyBaseManagerFilter()
    {
        if (currentBaseManagerSummary is null) return;
        var query = BaseManagerSearchBox.Text.Trim();
        var status = (BaseManagerStatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";
        baseManagerView = currentBaseManagerSummary.Bases.Where(b =>
            (status.Equals("All", StringComparison.OrdinalIgnoreCase) || b.Health.Equals(status, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(query) || b.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || b.BaseId.Contains(query, StringComparison.OrdinalIgnoreCase) || b.GuildName.Contains(query, StringComparison.OrdinalIgnoreCase) || b.GuildId.Contains(query, StringComparison.OrdinalIgnoreCase) || b.PalboxId.Contains(query, StringComparison.OrdinalIgnoreCase) || b.Location.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
        BaseManagerGrid.ItemsSource = baseManagerView;
        BaseManagerFilteredText.Text = baseManagerView.Count.ToString();
    }

    private void BaseManagerSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (BaseManagerGrid.SelectedItem is not BaseManagerRow row)
        {
            BaseManagerDetailsText.Text = "Select a base to inspect ownership, location and Palbox information.";
            return;
        }
        BaseManagerDetailsText.Text = $"Name: {row.Name}\nBase ID: {row.BaseId}\nGuild: {row.GuildName}\nGuild ID: {row.GuildId}\nPalbox ID: {row.PalboxDisplay}\nCoordinates: {row.Location}\nHealth: {row.Health}\nInternal name: {BaseDisplayNameResolver.DescribeRawName(row.InternalName)}\nSource: {row.SourcePath}";
        BaseManagerTargetGuildBox.Text = row.GuildId;
        currentOwnershipPreview = null;
        BaseManagerApplyOwnershipButton.IsEnabled = false;
        BaseManagerOwnershipPreviewText.Text = "Choose an operation and click Preview. No save data is changed during preview.";
    }

    private void BaseManagerExport_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentBaseManagerSummary is null) RefreshBaseManager();
            if (currentBaseManagerSummary is null) return;
            var path = BaseManager.ExportCsv(currentBaseManagerSummary, baseManagerView);
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            BaseManagerStatusText.Text = "Base inventory exported: " + path; BaseManagerStatusText.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex) { BaseManagerStatusText.Text = ex.Message; BaseManagerStatusText.Foreground = Brushes.OrangeRed; }
    }

    private void BaseManagerBackup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentBaseManagerSummary is null) RefreshBaseManager();
            if (currentBaseManagerSummary is null) return;
            if (server.IsRunning()) throw new InvalidOperationException("Stop PalServer before creating a Base Manager safety backup.");
            var path = BaseManager.CreateSafetyBackup(currentBaseManagerSummary.WorldPath);
            BaseManagerStatusText.Text = "Verified safety backup created: " + path; BaseManagerStatusText.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex) { BaseManagerStatusText.Text = ex.Message; BaseManagerStatusText.Foreground = Brushes.OrangeRed; }
    }

    private void BaseManagerPlan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentBaseManagerSummary is null || BaseManagerGrid.SelectedItem is not BaseManagerRow row) throw new InvalidOperationException("Select a base first.");
            var path = BaseManager.SaveOwnershipPlan(currentBaseManagerSummary, row, BaseManagerTargetGuildBox.Text);
            BaseManagerStatusText.Text = "Ownership plan saved: " + path; BaseManagerStatusText.Foreground = Brushes.LightGreen;
        }
        catch (Exception ex) { BaseManagerStatusText.Text = ex.Message; BaseManagerStatusText.Foreground = Brushes.OrangeRed; }
    }

    private OwnershipOperationType SelectedOwnershipOperation()
    {
        var text = (BaseManagerOperationBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Transfer Ownership";
        return text.StartsWith("Delete", StringComparison.OrdinalIgnoreCase)
            ? OwnershipOperationType.DeleteBaseAndOwnedObjects
            : OwnershipOperationType.TransferOwnership;
    }

    private void BaseManagerOwnershipPreview_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (currentBaseManagerSummary is null || BaseManagerGrid.SelectedItem is not BaseManagerRow row)
                throw new InvalidOperationException("Select a base first.");
            Mouse.OverrideCursor = Cursors.Wait;
            var engine = new OwnershipEngineService(settings);
            currentOwnershipPreview = engine.Preview(currentBaseManagerSummary, row, SelectedOwnershipOperation(), BaseManagerTargetGuildBox.Text, server.IsRunning());
            var categories = currentOwnershipPreview.Categories.Count == 0
                ? "No categorized ownership scopes"
                : string.Join(Environment.NewLine, currentOwnershipPreview.Categories.OrderBy(x => x.Key).Select(x => $"• {x.Key}: {x.Value}"));
            var paths = currentOwnershipPreview.SamplePaths.Count == 0
                ? "No matching decoded paths"
                : string.Join(Environment.NewLine, currentOwnershipPreview.SamplePaths.Select(x => "• " + x));
            var findings = string.Join(Environment.NewLine, currentOwnershipPreview.Findings.Select(x => "• " + x));
            BaseManagerOwnershipPreviewText.Text =
                $"Operation: {currentOwnershipPreview.Operation}\n" +
                $"Matched scopes: {currentOwnershipPreview.MatchedScopeCount}\n" +
                $"Base references: {currentOwnershipPreview.BaseReferenceCount}\n" +
                $"Palbox references: {currentOwnershipPreview.PalboxReferenceCount}\n" +
                $"Current guild references: {currentOwnershipPreview.GuildReferenceCount}\n\n" +
                $"Categories:\n{categories}\n\n" +
                $"Sample decoded paths:\n{paths}\n\n" +
                $"Findings:\n{findings}";
            BaseManagerApplyOwnershipButton.IsEnabled = currentOwnershipPreview.CanApply;
            BaseManagerStatusText.Text = currentOwnershipPreview.CanApply
                ? "Ownership preview is ready. Review it before applying the transaction."
                : "Ownership preview contains blocking findings.";
            BaseManagerStatusText.Foreground = currentOwnershipPreview.CanApply ? Brushes.LightGreen : Brushes.Gold;
        }
        catch (Exception ex)
        {
            currentOwnershipPreview = null;
            BaseManagerApplyOwnershipButton.IsEnabled = false;
            BaseManagerStatusText.Text = ex.Message;
            BaseManagerStatusText.Foreground = Brushes.OrangeRed;
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private void BaseManagerOwnershipApply_Click(object sender, RoutedEventArgs e)
    {
        if (currentOwnershipPreview is null || !currentOwnershipPreview.CanApply)
        {
            MessageBox.Show("Create a valid ownership preview first.", "Ownership Engine", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var operationText = currentOwnershipPreview.Operation == OwnershipOperationType.TransferOwnership
            ? $"transfer {currentOwnershipPreview.Base.Name} to guild {currentOwnershipPreview.TargetGuildId}"
            : $"delete {currentOwnershipPreview.Base.Name} and matching decoded ownership scopes";
        var confirmationMessage =
            "Apply this ownership transaction?\n\n" +
            operationText + "\n\n" +
            "MystTiq will create a complete rollback ZIP, stage the edit, re-encode Level.sav, " +
            "independently verify it, and restore the original save if any stage fails.";

        if (MessageBox.Show(
                confirmationMessage,
                "Ownership Engine — Confirm Transaction",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        try
        {
            Mouse.OverrideCursor = Cursors.Wait;
            var result = new OwnershipEngineService(settings).Apply(currentOwnershipPreview);
            RecordAudit("Warning", "World", "Ownership transaction completed", $"{operationText} • transaction {result.TransactionId} • backup {result.BackupPath}", 23);
            Log($"[OWNERSHIP] Completed transaction {result.TransactionId}. Scopes: {result.ScopesChanged}; values: {result.ValuesChanged}; backup: {result.BackupPath}");
            var completionMessage =
                "Ownership transaction completed.\n\n" +
                $"Scopes changed: {result.ScopesChanged}\n" +
                $"Values changed: {result.ValuesChanged}\n" +
                $"Verification: {(result.VerificationPassed ? "PASS" : "FAIL")}\n\n" +
                "Rollback backup:\n" + result.BackupPath + "\n\n" +
                "Report:\n" + result.ReportPath;

            MessageBox.Show(
                completionMessage,
                "Ownership Engine Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            currentOwnershipPreview = null;
            BaseManagerApplyOwnershipButton.IsEnabled = false;
            RefreshBaseManager();
        }
        catch (Exception ex)
        {
            RecordAudit("Critical", "World", "Ownership transaction failed", ex.Message, 23);
            Log("[OWNERSHIP] Failed: " + ex);
            var failureMessage =
                "The ownership transaction failed. MystTiq attempted to restore the original Level.sav. " +
                "Review the rollback package and logs before starting PalServer.\n\n" +
                ex.Message;
            MessageBox.Show(failureMessage, "Ownership Engine Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { Mouse.OverrideCursor = null; }
    }

    private void BaseManagerOpenRecovery_Click(object sender, RoutedEventArgs e)
    {
        NavigateToPage(15);
        RefreshGuildBaseRecovery();
    }
}
