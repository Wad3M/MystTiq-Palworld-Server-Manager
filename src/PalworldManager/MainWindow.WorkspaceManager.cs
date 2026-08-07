using PalworldManager.Services;

namespace PalworldManager;

public partial class MainWindow
{
    private void RefreshWorkspaceManager()
    {
        var paths = ApplicationPathService.Current;

        WorkspaceModeText.Text = paths.IsPortable ? "PORTABLE" : "INSTALLED";
        WorkspaceModeText.Foreground = new SolidColorBrush(paths.IsPortable
            ? Color.FromRgb(86, 217, 135)
            : Color.FromRgb(127, 200, 255));
        WorkspaceModeDetailText.Text = paths.IsPortable
            ? "Workspace and MystTiq data are rooted beside the executable."
            : "Windows application-data locations are active; server paths remain configurable.";

        WorkspaceExecutableRootBox.Text = paths.ExecutableRoot;
        WorkspaceRootBox.Text = paths.WorkspaceRoot;
        WorkspaceDataRootBox.Text = paths.DataRoot;
        WorkspaceServerRootBox.Text = settings.ServerRoot;
        WorkspaceSteamCmdBox.Text = settings.SteamCmdPath;
        WorkspaceBackupsBox.Text = settings.BackupRoot;
        WorkspaceDownloadsBox.Text = paths.DownloadsRoot;
        WorkspaceExportsBox.Text = paths.ExportsRoot;
        WorkspaceLogsBox.Text = paths.LogsRoot;

        var serverValid = File.Exists(Path.Combine(settings.ServerRoot, "PalServer.exe"));
        var steamCmdValid = File.Exists(settings.SteamCmdPath);
        WorkspaceDiscoveryText.Text = serverValid ? "SERVER READY" : "SERVER NOT FOUND";
        WorkspaceDiscoveryText.Foreground = new SolidColorBrush(serverValid
            ? Color.FromRgb(86, 217, 135)
            : Color.FromRgb(240, 178, 70));
        WorkspaceDiscoveryDetailText.Text = serverValid
            ? $"PalServer.exe detected. SteamCMD is {(steamCmdValid ? "ready" : "not configured")}."
            : "Select the folder containing PalServer.exe or place a server beneath Workspace\\Servers in portable mode.";

        WorkspaceHealthText.Text = "NOT VALIDATED";
        WorkspaceHealthText.Foreground = new SolidColorBrush(Color.FromRgb(240, 178, 70));
        WorkspaceHealthDetailText.Text = "Run Validate All to inspect folder access and required files.";
        WorkspaceStatusText.Text = "Workspace paths refreshed. No files were modified.";
    }

    private void WorkspaceRefresh_Click(object sender, RoutedEventArgs e) => RefreshWorkspaceManager();

    private void WorkspaceValidate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var paths = ApplicationPathService.Current;
            var findings = new List<string>();
            var failures = 0;

            ValidateWorkspaceDirectory(paths.DataRoot, "Application data", findings, ref failures, requireWritable: true);
            ValidateWorkspaceDirectory(paths.WorkspaceRoot, "Workspace root", findings, ref failures, requireWritable: paths.IsPortable);
            ValidateWorkspaceDirectory(settings.BackupRoot, "Backup root", findings, ref failures, requireWritable: true);

            var serverExe = Path.Combine(WorkspaceServerRootBox.Text.Trim(), "PalServer.exe");
            if (File.Exists(serverExe)) findings.Add("✓ PalServer.exe detected");
            else { findings.Add("✗ PalServer.exe was not found"); failures++; }

            if (File.Exists(WorkspaceSteamCmdBox.Text.Trim())) findings.Add("✓ steamcmd.exe detected");
            else { findings.Add("⚠ steamcmd.exe was not found"); }

            WorkspaceHealthText.Text = failures == 0 ? "HEALTHY" : "ATTENTION";
            WorkspaceHealthText.Foreground = new SolidColorBrush(failures == 0
                ? Color.FromRgb(86, 217, 135)
                : Color.FromRgb(240, 178, 70));
            WorkspaceHealthDetailText.Text = failures == 0
                ? "Required workspace locations are accessible."
                : $"{failures} required workspace check(s) need attention.";
            WorkspaceStatusText.Text = string.Join("  •  ", findings);
            Log($"[WORKSPACE] Validation completed with {failures} required failure(s).");
        }
        catch (Exception exception)
        {
            WorkspaceHealthText.Text = "FAILED";
            WorkspaceHealthText.Foreground = new SolidColorBrush(Color.FromRgb(240, 91, 87));
            WorkspaceHealthDetailText.Text = exception.Message;
            WorkspaceStatusText.Text = "Workspace validation failed: " + exception.Message;
            Log("[WORKSPACE ERROR] Validation failed: " + exception.Message);
        }
    }

    private static void ValidateWorkspaceDirectory(
        string directory,
        string label,
        ICollection<string> findings,
        ref int failures,
        bool requireWritable)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            findings.Add($"✗ {label} is not configured");
            failures++;
            return;
        }

        try
        {
            Directory.CreateDirectory(directory);
            if (requireWritable)
            {
                var probe = Path.Combine(directory, $".mysttiq-workspace-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probe, "MystTiq workspace validation");
                File.Delete(probe);
            }
            findings.Add($"✓ {label} accessible");
        }
        catch (Exception exception)
        {
            findings.Add($"✗ {label}: {exception.Message}");
            failures++;
        }
    }

    private void WorkspaceSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            settings.ServerRoot = WorkspaceServerRootBox.Text.Trim();
            settings.SteamCmdPath = WorkspaceSteamCmdBox.Text.Trim();
            settings.BackupRoot = WorkspaceBackupsBox.Text.Trim();
            store.Save(settings);

            ServerRootBox.Text = settings.ServerRoot;
            SteamCmdBox.Text = settings.SteamCmdPath;
            BackupRootBox.Text = settings.BackupRoot;

            Log("[WORKSPACE] Server, SteamCMD, and backup paths saved.");
            RefreshWorkspaceManager();
            WorkspaceStatusText.Text = "Workspace paths saved. Restart MystTiq after changing the active server location.";
        }
        catch (Exception exception)
        {
            WorkspaceStatusText.Text = "Save failed: " + exception.Message;
            Log("[WORKSPACE ERROR] Save failed: " + exception.Message);
            AppDialog.Show(
                "Workspace paths could not be saved." + Environment.NewLine + Environment.NewLine + exception.Message,
                "Workspace Save Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void WorkspaceBrowseServer_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select the folder containing PalServer.exe",
            InitialDirectory = Directory.Exists(WorkspaceServerRootBox.Text)
                ? WorkspaceServerRootBox.Text
                : ApplicationPathService.Current.ServersRoot,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
            WorkspaceServerRootBox.Text = dialog.FolderName;
    }

    private void WorkspaceBrowseSteamCmd_Click(object sender, RoutedEventArgs e)
    {
        var currentDirectory = Path.GetDirectoryName(WorkspaceSteamCmdBox.Text);
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select steamcmd.exe",
            Filter = "SteamCMD executable (steamcmd.exe)|steamcmd.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = Directory.Exists(currentDirectory)
                ? currentDirectory
                : ApplicationPathService.Current.SteamCmdRoot
        };
        if (dialog.ShowDialog(this) == true)
            WorkspaceSteamCmdBox.Text = dialog.FileName;
    }

    private void WorkspaceBrowseBackups_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select the Palworld backup folder",
            InitialDirectory = Directory.Exists(WorkspaceBackupsBox.Text)
                ? WorkspaceBackupsBox.Text
                : ApplicationPathService.Current.BackupsRoot,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
            WorkspaceBackupsBox.Text = dialog.FolderName;
    }

    private void WorkspaceOpenExecutable_Click(object sender, RoutedEventArgs e) => OpenWorkspaceLocation(ApplicationPathService.Current.ExecutableRoot, "application folder");
    private void WorkspaceOpenRoot_Click(object sender, RoutedEventArgs e) => OpenWorkspaceLocation(ApplicationPathService.Current.WorkspaceRoot, "workspace root");
    private void WorkspaceOpenData_Click(object sender, RoutedEventArgs e) => OpenWorkspaceLocation(ApplicationPathService.Current.DataRoot, "application data");
    private void WorkspaceOpenServer_Click(object sender, RoutedEventArgs e) => OpenWorkspaceLocation(WorkspaceServerRootBox.Text, "Palworld server folder");
    private void WorkspaceOpenSteamCmd_Click(object sender, RoutedEventArgs e) => OpenWorkspaceLocation(Path.GetDirectoryName(WorkspaceSteamCmdBox.Text), "SteamCMD folder");
    private void WorkspaceOpenBackups_Click(object sender, RoutedEventArgs e) => OpenWorkspaceLocation(WorkspaceBackupsBox.Text, "backup folder");
    private void WorkspaceOpenDownloads_Click(object sender, RoutedEventArgs e) => OpenWorkspaceLocation(ApplicationPathService.Current.DownloadsRoot, "downloads folder");
    private void WorkspaceOpenExports_Click(object sender, RoutedEventArgs e) => OpenWorkspaceLocation(ApplicationPathService.Current.ExportsRoot, "exports folder");
    private void WorkspaceOpenLogs_Click(object sender, RoutedEventArgs e) => OpenWorkspaceLocation(ApplicationPathService.Current.LogsRoot, "logs folder");

    private void OpenWorkspaceLocation(string? directory, string description)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new DirectoryNotFoundException($"The {description} is not configured.");

            Directory.CreateDirectory(directory);
            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
            WorkspaceStatusText.Text = $"Opened {description}: {directory}";
        }
        catch (Exception exception)
        {
            WorkspaceStatusText.Text = $"Could not open {description}: {exception.Message}";
            AppDialog.Show(
                $"MystTiq could not open the {description}." + Environment.NewLine + Environment.NewLine + exception.Message,
                "Open Folder Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
