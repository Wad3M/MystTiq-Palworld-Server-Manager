using PalworldManager.Models;
using PalworldManager.Services;

namespace PalworldManager;

public partial class MainWindow
{
    private readonly ObservableCollection<TransactionHistoryRow> transactionHistoryRows = [];
    private ICollectionView? transactionHistoryView;

    private void InitializeTransactionCenter()
    {
        TransactionHistoryGrid.ItemsSource = transactionHistoryRows;
        transactionHistoryView = CollectionViewSource.GetDefaultView(transactionHistoryRows);
        transactionHistoryView.Filter = FilterTransactionHistory;
        TransactionStateFilter.SelectedIndex = 0;
        TransactionOperationFilter.SelectedIndex = 0;
        RefreshTransactionHistory();
    }

    private void RefreshTransactionHistory_Click(object sender, RoutedEventArgs e) => RefreshTransactionHistory();

    private void RefreshTransactionHistory()
    {
        try
        {
            var snapshot = new TransactionHistoryService(settings).Load();
            transactionHistoryRows.Clear();
            foreach (var row in snapshot.Rows) transactionHistoryRows.Add(row);
            transactionHistoryView?.Refresh();
            UpdateTransactionSummary(snapshot.Diagnostics);
            TransactionDetailText.Text = transactionHistoryRows.Count == 0
                ? "No durable transaction records were found. New world-changing operations will appear here when they create transaction journals or reports."
                : "Select a transaction to inspect its stages, validation details, backup reference, and diagnostics.";
        }
        catch (Exception ex)
        {
            Log($"[TRANSACTIONS] Refresh failed: {ex.Message}");
            TransactionStatusText.Text = $"Transaction history could not be loaded: {ex.Message}";
        }
    }

    private bool FilterTransactionHistory(object item)
    {
        if (item is not TransactionHistoryRow row) return false;
        var search = TransactionSearchBox?.Text?.Trim() ?? "";
        var state = (TransactionStateFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All states";
        var operation = (TransactionOperationFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All operations";

        if (!state.Equals("All states", StringComparison.OrdinalIgnoreCase) && !row.State.Equals(state, StringComparison.OrdinalIgnoreCase)) return false;
        if (!operation.Equals("All operations", StringComparison.OrdinalIgnoreCase) && !row.Operation.Equals(operation, StringComparison.OrdinalIgnoreCase)) return false;
        if (string.IsNullOrWhiteSpace(search)) return true;
        return row.TransactionId.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Operation.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.State.Contains(search, StringComparison.OrdinalIgnoreCase)
            || row.Target.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private void TransactionFilter_Changed(object sender, EventArgs e)
    {
        transactionHistoryView?.Refresh();
        UpdateTransactionSummary([]);
    }

    private void TransactionHistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TransactionHistoryGrid.SelectedItem is not TransactionHistoryRow row)
        {
            TransactionDetailText.Text = "Select a transaction to inspect its details.";
            OpenTransactionBackupButton.IsEnabled = false;
            OpenTransactionReportButton.IsEnabled = false;
            return;
        }
        TransactionDetailText.Text = row.Details;
        OpenTransactionBackupButton.IsEnabled = !string.IsNullOrWhiteSpace(row.BackupPath) && File.Exists(row.BackupPath);
        OpenTransactionReportButton.IsEnabled = !string.IsNullOrWhiteSpace(row.ReportPath) && (File.Exists(row.ReportPath) || Directory.Exists(row.ReportPath));
    }

    private void OpenTransactionBackup_Click(object sender, RoutedEventArgs e)
    {
        if (TransactionHistoryGrid.SelectedItem is TransactionHistoryRow row) OpenTransactionPath(row.BackupPath);
    }

    private void OpenTransactionReport_Click(object sender, RoutedEventArgs e)
    {
        if (TransactionHistoryGrid.SelectedItem is TransactionHistoryRow row) OpenTransactionPath(row.ReportPath);
    }

    private void OpenTransactionFolder_Click(object sender, RoutedEventArgs e)
    {
        var root = new TransactionHistoryService(settings).HistoryRoot;
        Directory.CreateDirectory(root);
        Process.Start(new ProcessStartInfo("explorer.exe", root) { UseShellExecute = true });
    }

    private static void OpenTransactionPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        else if (File.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    private void UpdateTransactionSummary(IReadOnlyCollection<string> diagnostics)
    {
        var visible = transactionHistoryView?.Cast<TransactionHistoryRow>().ToList() ?? transactionHistoryRows.ToList();
        TransactionTotalCountText.Text = visible.Count.ToString(CultureInfo.InvariantCulture);
        TransactionSuccessCountText.Text = visible.Count(x => x.State.Equals("Committed", StringComparison.OrdinalIgnoreCase) || x.State.Equals("Activated", StringComparison.OrdinalIgnoreCase) || x.State.Equals("Verified", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture);
        TransactionFailedCountText.Text = visible.Count(x => x.State.Equals("Failed", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture);
        TransactionRollbackCountText.Text = visible.Count(x => x.RollbackAvailable).ToString(CultureInfo.InvariantCulture);
        TransactionStatusText.Text = diagnostics.Count == 0
            ? $"Loaded {transactionHistoryRows.Count} durable transaction record(s). This center is read-only; rollback execution is not enabled in this release."
            : $"Loaded {transactionHistoryRows.Count} record(s) with {diagnostics.Count} skipped or malformed journal(s). See the session log for details.";
        foreach (var diagnostic in diagnostics) Log($"[TRANSACTIONS] {diagnostic}");
    }
}
