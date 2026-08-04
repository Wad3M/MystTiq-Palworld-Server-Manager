using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using PalworldManager.Models;

namespace PalworldManager;

public partial class MainWindow
{
    private List<BackupRow> backupRetentionPreview = [];

    private void RefreshBackupCenterSummary()
    {
        try
        {
            var summary = backups.GetInventorySummary();
            BackupCenterTotalText.Text = summary.TotalArchives.ToString();
            BackupCenterSizeText.Text = summary.TotalSizeDisplay;
            BackupCenterServerText.Text = summary.ServerBackups.ToString();
            BackupCenterVerifiedText.Text = $"{summary.VerifiedServerBackups} verified";
            BackupCenterWorldText.Text = summary.WorldArchives.ToString();
            BackupCenterRepairText.Text = summary.RepairBackups.ToString();
            BackupCenterModText.Text = summary.ModBackups.ToString();
            BackupCenterRetentionText.Text = summary.RetentionCandidates.ToString();
            BackupCenterPolicyText.Text =
                $"Keep the newest {Math.Max(1, settings.BackupRetention)} managed server backup(s). " +
                "Retention applies only to Palworld_*.zip files in the configured backup root.";
        }
        catch (Exception ex)
        {
            BackupCenterPreviewText.Text = "Backup inventory could not be summarized: " + ex.Message;
            BackupCenterPreviewText.Foreground = Brushes.IndianRed;
        }
    }

    private void BackupCenterPreviewRetention_Click(object sender, RoutedEventArgs e)
    {
        backupRetentionPreview = backups.PreviewRetention();
        if (backupRetentionPreview.Count == 0)
        {
            BackupCenterPreviewText.Text = "No backups exceed the current retention policy.";
            BackupCenterPreviewText.Foreground = Brushes.LightGreen;
            return;
        }

        var totalMb = backupRetentionPreview.Sum(row => row.SizeMb);
        var oldest = backupRetentionPreview.Min(row => row.Created);
        var newest = backupRetentionPreview.Max(row => row.Created);
        BackupCenterPreviewText.Text =
            $"{backupRetentionPreview.Count} managed server backup(s), {totalMb:N2} MB, are eligible for cleanup. " +
            $"Range: {oldest:g} through {newest:g}. No files have been deleted.";
        BackupCenterPreviewText.Foreground = Brushes.Gold;
    }

    private void BackupCenterApplyRetention_Click(object sender, RoutedEventArgs e)
    {
        backupRetentionPreview = backups.PreviewRetention();
        if (backupRetentionPreview.Count == 0)
        {
            MessageBox.Show("No managed server backups exceed the current retention policy.",
                "Backup Retention", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var totalMb = backupRetentionPreview.Sum(row => row.SizeMb);
        var choice = MessageBox.Show(
            $"Delete {backupRetentionPreview.Count} old managed server backup(s)?\n\n" +
            $"Space to reclaim: {totalMb:N2} MB\n" +
            $"Newest {Math.Max(1, settings.BackupRetention)} backup(s) will be preserved.\n\n" +
            "World archives, Repair Center backups, and mod snapshots are not affected.",
            "Apply Backup Retention",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (choice != MessageBoxResult.Yes)
            return;

        var deleted = backups.ApplyRetentionPreview(backupRetentionPreview.Select(row => row.FilePath));
        Log($"Backup retention removed {deleted} old managed server backup(s).");
        backupRetentionPreview = [];
        RefreshBackups();
        BackupCenterPreviewText.Text = $"Retention cleanup completed. Deleted {deleted} backup(s).";
        BackupCenterPreviewText.Foreground = Brushes.LightGreen;
    }

    private void BackupCenterVerifyAll_Click(object sender, RoutedEventArgs e)
    {
        var rows = backups.List();
        if (rows.Count == 0)
        {
            MessageBox.Show("No managed server backups are available to verify.",
                "Verify Backups", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _ = RunExclusive(async ct =>
        {
            var passed = 0;
            var failed = new List<string>();
            BackupsStatusText.Foreground = Brushes.Gold;

            foreach (var row in rows)
            {
                ct.ThrowIfCancellationRequested();
                BackupsStatusText.Text = $"Verifying {passed + failed.Count + 1} of {rows.Count}: {Path.GetFileName(row.FilePath)}";
                try
                {
                    await backups.VerifyAsync(row.FilePath, ct);
                    passed++;
                }
                catch (Exception ex)
                {
                    failed.Add($"{Path.GetFileName(row.FilePath)}: {ex.Message}");
                }
            }

            RefreshBackups();
            if (failed.Count == 0)
            {
                BackupsStatusText.Foreground = Brushes.LightGreen;
                BackupsStatusText.Text = $"All {passed} managed server backup(s) verified successfully.";
            }
            else
            {
                BackupsStatusText.Foreground = Brushes.Gold;
                BackupsStatusText.Text = $"Verification completed: {passed} passed, {failed.Count} failed.";
                MessageBox.Show(string.Join("\n\n", failed.Take(8)),
                    "Backup Verification Results", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        });
    }

    private void BackupCenterOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(settings.BackupRoot);
        Process.Start(new ProcessStartInfo
        {
            FileName = settings.BackupRoot,
            UseShellExecute = true
        });
    }
}
