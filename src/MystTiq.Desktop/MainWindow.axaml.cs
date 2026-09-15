using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.ViewModels;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace MystTiq.Desktop;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += MainWindow_Closing;
        PropertyChanged += (_, args) =>
        {
            if (args.Property == WindowStateProperty)
                UpdateMaximizeGlyph();
        };
        UpdateMaximizeGlyph();
    }

    private void Window_OnSizeChanged(object? sender, SizeChangedEventArgs e) => UpdateMaximizeGlyph();

    // v0.7.16.0: the tab strip's own host panel, not the whole window -- it resizes whenever the
    // window does, but also whenever the fixed-width brand column or the window-chrome column
    // change width, so this is a more accurate trigger than Window_OnSizeChanged for how much room
    // is actually left for tabs.
    private void TabStripHost_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.UpdateTabStripWidth(e.NewSize.Width);
    }

    // v0.7.25.0: same reasoning as TabStripHost_OnSizeChanged above, applied to the ribbon.
    private void RibbonHost_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.UpdateRibbonWidth(e.NewSize.Width);
    }

    // Mirrors TabOverflowButton_OnClick below: the hidden-groups list is dynamic, so the flyout is
    // built fresh each click from whatever RecomputeRibbonLayout most recently decided didn't fit.
    private void RibbonOverflowButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || sender is not Control anchor)
            return;

        var flyout = new MenuFlyout();
        var first = true;
        foreach (var group in vm.OverflowRibbonGroups)
        {
            if (!first) flyout.Items.Add(new Separator());
            first = false;
            flyout.Items.Add(new MenuItem { Header = group.Title, IsEnabled = false });
            foreach (var action in group.Actions)
            {
                var item = new MenuItem { Header = action.Label };
                item.Click += (_, _) => InvokeRibbonAction(action);
                flyout.Items.Add(item);
            }
        }

        flyout.ShowAt(anchor);
    }

    // Fires for every ribbon button regardless of whether it's Command-driven or needs a native
    // dialog -- when Command is set, Avalonia already invoked it before this runs, so
    // InvokeRibbonAction's Command branch below is a no-op for those; when Command is null (an
    // action needing a real file picker), this is the only thing that runs it.
    private void RibbonAction_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: RibbonActionViewModel action })
            InvokeRibbonAction(action);
    }

    private void InvokeRibbonAction(RibbonActionViewModel action)
    {
        if (action.Command is not null)
        {
            if (action.Command.CanExecute(action.CommandParameter))
                action.Command.Execute(action.CommandParameter);
            return;
        }

        switch (action.NativeDialogAction)
        {
            case "ImportConfig": ImportPalworldConfiguration_Click(this, new RoutedEventArgs()); break;
            case "ExportConfig": ExportPalworldConfiguration_Click(this, new RoutedEventArgs()); break;
            case "ExportConsole": ExportConsole_Click(this, new RoutedEventArgs()); break;
            // v0.7.42.0: cross-page consistency sweep -- three more page-local Export buttons
            // relocated into the ribbon, dispatched the same way as the three above.
            case "ExportWorldValidation": ExportWorldValidation_Click(this, new RoutedEventArgs()); break;
            case "ExportPlayersCsv": ExportPlayersCsv_Click(this, new RoutedEventArgs()); break;
            case "ExportNotifications": ExportNotifications_Click(this, new RoutedEventArgs()); break;
        }
    }

    // Same reasoning as AddTabButton_OnClick below: the hidden-tabs list is dynamic, built fresh
    // each click from whatever RecomputeTabLayout most recently decided didn't fit.
    private void TabOverflowButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || sender is not Control anchor)
            return;

        var flyout = new MenuFlyout();
        foreach (var tab in vm.OverflowTabs)
        {
            var item = new MenuItem { Header = string.IsNullOrWhiteSpace(tab.ProfileName) ? "(unnamed)" : tab.ProfileName };
            item.Click += (_, _) => vm.ActiveTab = tab;
            flyout.Items.Add(item);
        }

        flyout.Items.Add(new Separator());
        var newServerItem = new MenuItem { Header = "Set Up New Server" };
        newServerItem.Click += (_, _) => vm.SetUpNewServerTabCommand.Execute(null);
        flyout.Items.Add(newServerItem);

        flyout.ShowAt(anchor);
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void Minimize_OnClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_OnClick(object? sender, RoutedEventArgs e) => ToggleMaximize();
    private void Close_OnClick(object? sender, RoutedEventArgs e) => Close();

    // The "+" tab button's chooser is built here rather than as static XAML because its
    // "Connect to Existing Server" entries are dynamic -- filtered against whichever profiles
    // are already open in a tab, which changes as tabs open and close. Follows this file's
    // existing convention of handling UI-only concerns (file pickers, window chrome) in
    // code-behind rather than the view model.
    private void AddTabButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || sender is not Control anchor)
            return;

        var flyout = new MenuFlyout();

        // v0.7.52.0 "+" Flow Restructure (item 53): four explicit top-level choices instead of the
        // one generic "Set Up New Server" entry that used to be the flyout's only way in (Local vs.
        // Remote was previously one step deeper, inside the wizard itself).
        var newServerItem = new MenuItem { Header = "Set Up New Server" };
        newServerItem.Click += (_, _) => vm.SetUpNewServerTabCommand.Execute(null);
        flyout.Items.Add(newServerItem);

        var connectLocalItem = new MenuItem { Header = "Connect to Local Server" };
        connectLocalItem.Click += (_, _) => vm.ConnectLocalServerTabCommand.Execute(null);
        flyout.Items.Add(connectLocalItem);

        var connectRemoteItem = new MenuItem { Header = "Connect to Remote Server" };
        connectRemoteItem.Click += (_, _) => vm.ConnectRemoteServerTabCommand.Execute(null);
        flyout.Items.Add(connectRemoteItem);

        // Only shown when a source actually exists to clone from (an already-connected local tab) --
        // matches the existing pattern below of only adding "Connect to {profile}" entries for
        // profiles that are actually reachable, rather than showing a permanently-disabled item.
        if (vm.HasCloneableLocalTab)
        {
            var cloneItem = new MenuItem { Header = "Clone a Server" };
            cloneItem.Click += (_, _) => vm.CloneServerFlowCommand.Execute(null);
            flyout.Items.Add(cloneItem);
        }

        var availableProfiles = vm.Profiles.Where(p => vm.Tabs.All(t => t.Profile?.Id != p.Id)).ToList();
        if (availableProfiles.Count > 0)
        {
            flyout.Items.Add(new Separator());
            foreach (var profile in availableProfiles)
            {
                var item = new MenuItem { Header = $"Connect to {profile.Name}" };
                item.Click += (_, _) => vm.ConnectExistingProfileTabCommand.Execute(profile);
                flyout.Items.Add(item);
            }
        }

        flyout.ShowAt(anchor);
    }

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        UpdateMaximizeGlyph();
    }

    private void UpdateMaximizeGlyph()
    {
        if (MaximizeGlyph is not null)
            MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    // v0.7.11.0: previously always cancelled the close and hid to tray, regardless of whether
    // anything was actually running -- so there was no way to fully quit by pressing the window's
    // own close button, ever. Now checks every open tab (not just the active one; Fleet supports
    // several servers running at once) rather than just the currently-focused tab's state.
    //
    // v0.7.73.0: minimizing to tray in this case previously happened silently -- reported live as
    // surprising ("why is the server still running, I closed it"). Now confirms first via
    // ConfirmMinimizeToTrayDialog, same pattern as CloseTabButton_OnClick's own confirm dialog.
    // e.Cancel is set synchronously (Avalonia reads it right after this handler returns, not after
    // the awaited dialog resolves), so the close is always blocked while the dialog decides what
    // happens next; on confirmation this calls HideMainWindowToTray() directly rather than Close()
    // again, which would just re-enter this same handler.
    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (Application.Current is not App app || app.IsExplicitExitRequested) return;

        var runningCount = DataContext is MainWindowViewModel vm ? vm.Tabs.Count(t => t.ServerIsRunning) : 0;
        if (runningCount == 0)
        {
            app.ExitGuiOnlyIfNothingIsRunning();
            return;
        }

        e.Cancel = true;

        var dialog = new Views.ConfirmMinimizeToTrayDialog(runningCount);
        var result = await dialog.ShowDialog<Views.ConfirmMinimizeToTrayResult>(this);
        if (result != Views.ConfirmMinimizeToTrayResult.MinimizeToTray) return;

        app.HideMainWindowToTray();
        app.ShowTrayStillRunningReminder("A Palworld server is still running. MystTiq will keep managing it in the background -- open the tray icon to bring the window back, or to stop the server and exit.");
    }

    // v0.7.11.0: closing a tab whose server is running previously left it running silently with
    // no confirmation at all. Now asks first -- Cancel keeps the tab open, Leave Running closes the
    // tab without touching the server (the old behavior), Stop & Close stops it first.
    private async void CloseTabButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TabSession tab } || DataContext is not MainWindowViewModel vm)
            return;
        if (vm.Tabs.Count <= 1) return; // matches CloseTabCommand's own CanExecute guard

        if (tab.ServerIsRunning)
        {
            var dialog = new Views.ConfirmCloseTabDialog(tab.ProfileName);
            var result = await dialog.ShowDialog<Views.ConfirmCloseTabResult>(this);
            if (result == Views.ConfirmCloseTabResult.Cancel) return;
            if (result == Views.ConfirmCloseTabResult.StopAndClose)
                await vm.StopTabServerAsync(tab);
        }

        (vm.CloseTabCommand as RelayCommand<TabSession>)?.Execute(tab);
    }

    private async void CopyActiveWorldId_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || string.IsNullOrWhiteSpace(vm.ActiveWorldIdText) || vm.ActiveWorldIdText == "Not resolved")
            return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(vm.ActiveWorldIdText);
    }

    private async void CopyGuildId_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { SelectedExplorerGuild: { } guild }) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null) await clipboard.SetTextAsync(guild.GuildId);
    }

    private async void CopyBaseId_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel { SelectedExplorerBase: { } item }) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null) await clipboard.SetTextAsync(item.BaseId);
    }

    private void EnvironmentAction_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MystTiq.Desktop.Models.EnvironmentChecklistItemDto item } && DataContext is MainWindowViewModel vm)
            vm.EnvironmentActionCommand.Execute(item);
    }

    private async void CopyNetworkDiagnosticReport_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || string.IsNullOrWhiteSpace(vm.NetworkReportText))
            return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(vm.NetworkReportText);
    }

    private async void CopyWanPublicIpPort_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || string.IsNullOrWhiteSpace(vm.WanPublicIpPort) || vm.WanPublicIpPort == "Not checked")
            return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(vm.WanPublicIpPort);
    }

    private async void ExportNetworkDiagnosticReport_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !StorageProvider.CanSave) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export MystTiq diagnostic report",
            SuggestedFileName = $"MystTiq-Diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            DefaultExtension = "txt",
            FileTypeChoices = [new FilePickerFileType("Text report") { Patterns = ["*.txt"] }]
        });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(vm.NetworkReportText);
    }

    private async void CreateSupportPackage_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !StorageProvider.CanSave) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Create redacted MystTiq support package",
            SuggestedFileName = $"MystTiq-Support-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            DefaultExtension = "zip",
            FileTypeChoices = [new FilePickerFileType("ZIP archive") { Patterns = ["*.zip"] }]
        });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        var report = archive.CreateEntry("diagnostics.txt", CompressionLevel.Optimal);
        await using (var reportStream = report.Open())
        await using (var writer = new StreamWriter(reportStream))
            await writer.WriteAsync(vm.BuildRedactedSupportReport());
        var manifest = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
        await using (var manifestStream = manifest.Open())
        await JsonSerializer.SerializeAsync(manifestStream, new
        {
            product = "MystTiq Palworld Server Manager",
            version = vm.Version,
            generatedAt = DateTimeOffset.UtcNow,
            redacted = true,
            contents = new[] { "diagnostics.txt" }
        });
    }

    private async void WorkspaceBrowse_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || sender is not Button { Tag: string key } || !vm.IsLocalProfile) return;
        if (key == "steamcmd")
        {
            if (!StorageProvider.CanOpen) return;
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Select SteamCMD executable", AllowMultiple = false });
            var selected = files.FirstOrDefault();
            if (selected is not null) vm.ConfigSteamCmdPath = selected.Path.LocalPath;
            return;
        }
        if (!StorageProvider.CanPickFolder) return;
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = $"Select {key} folder", AllowMultiple = false });
        var folder = folders.FirstOrDefault();
        if (folder is null) return;
        var value = folder.Path.LocalPath;
        if (key == "server") vm.ConfigServerRoot = value;
        else if (key == "backup") vm.ConfigBackupRoot = value;
        else if (key == "runtime") vm.ConfigRuntimeRoot = value;
    }

    private void WorkspaceOpen_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || sender is not Button { Tag: string key } || !vm.TryGetLocalWorkspacePath(key, out var path)) return;
        var target = File.Exists(path) ? Path.GetDirectoryName(path) : path;
        if (string.IsNullOrWhiteSpace(target) || !Directory.Exists(target)) return;
        Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });
    }

    // v0.7.55.0 polish: the DESCRIPTION panel's SourceUrl was inert text -- opens it in the
    // default browser instead, matching WorkspaceOpen_Click's own established
    // UseShellExecute pattern. Only http/https is ever passed to Process.Start, since
    // SetModDescriptionSourceAsync (backend) already rejects any non-http(s) URL before it can
    // be persisted and shown here.
    private void OpenModDescriptionSource_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var url = vm.SelectedModDescription?.SourceUrl;
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return;
        Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
    }
    private async void ImportPalworldConfiguration_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !vm.PalworldConfigLoaded || !StorageProvider.CanOpen)
            return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import MystTiq Palworld configuration",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("MystTiq configuration") { Patterns = ["*.json"] }]
        });
        var file = files.FirstOrDefault();
        if (file is null) return;
        try
        {
            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            vm.ImportPalworldConfigurationJson(await reader.ReadToEndAsync());
        }
        catch (Exception ex)
        {
            vm.ReportPalworldConfigurationFileError($"Import failed: {ex.Message}");
        }
    }

    private async void ExportConsole_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !StorageProvider.CanSave) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export MystTiq console view",
            SuggestedFileName = $"MystTiq-Console-{DateTime.Now:yyyyMMdd-HHmmss}.log",
            DefaultExtension = "log",
            FileTypeChoices = [new FilePickerFileType("Log file") { Patterns = ["*.log", "*.txt"] }]
        });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        using var writer = new StreamWriter(stream);
        foreach (var line in vm.FilteredLogLines) await writer.WriteLineAsync(line);
    }

    private async void ExportPlayersCsv_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !StorageProvider.CanSave) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export MystTiq player directory",
            SuggestedFileName = $"MystTiq-Players-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExtension = "csv",
            FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
        });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(vm.ExportVisiblePlayersCsv());
    }

    private async void ExportGuildsCsv_Click(object? sender, RoutedEventArgs e) =>
        await ExportExplorerCsvAsync("Guilds", "Export MystTiq guild directory", vm => vm.ExportVisibleGuildsCsv());

    private async void ExportBasesCsv_Click(object? sender, RoutedEventArgs e) =>
        await ExportExplorerCsvAsync("Bases", "Export MystTiq base evidence", vm => vm.ExportVisibleBasesCsv());

    private async Task ExportExplorerCsvAsync(string suffix, string title, Func<MainWindowViewModel, string> content)
    {
        if (DataContext is not MainWindowViewModel vm || !StorageProvider.CanSave) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = $"MystTiq-{suffix}-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExtension = "csv",
            FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
        });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync();
        stream.SetLength(0);
        using var writer = new StreamWriter(stream);
        await writer.WriteAsync(content(vm));
    }

    private async void ExportPalworldConfiguration_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !vm.PalworldConfigLoaded || !StorageProvider.CanSave)
            return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export MystTiq Palworld configuration",
            SuggestedFileName = $"MystTiq-PalworldConfig-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
        });
        if (file is null) return;
        try
        {
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            using var writer = new StreamWriter(stream);
            await writer.WriteAsync(vm.ExportPalworldConfigurationJson());
            vm.ReportPalworldConfigurationFileSuccess($"Exported configuration to {file.Name}.");
        }
        catch (Exception ex)
        {
            vm.ReportPalworldConfigurationFileError($"Export failed: {ex.Message}");
        }
    }

    private async void InstallModZip_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !StorageProvider.CanOpen) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Install MOD ZIP on the managed Palworld server",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("ZIP archive") { Patterns = ["*.zip"] }]
        });
        var file = files.FirstOrDefault(); if (file is null) return;
        await using var stream = await file.OpenReadAsync();
        await vm.InstallModZipAsync(stream, file.Name);
    }

    private async void AnalyzeWorldArchive_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !StorageProvider.CanOpen) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Analyze world or player recovery ZIP on the managed server",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("ZIP archive") { Patterns = ["*.zip"] }]
        });
        var file = files.FirstOrDefault(); if (file is null) return;
        await using var stream = await file.OpenReadAsync();
        await vm.AnalyzeWorldArchiveAsync(stream, file.Name);
    }

    private async void ExportWorldValidation_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !StorageProvider.CanSave) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export MystTiq world validation report",
            SuggestedFileName = $"MystTiq-WorldValidation-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            DefaultExtension = "txt",
            FileTypeChoices = [new FilePickerFileType("Text report") { Patterns = ["*.txt"] }]
        });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync(); stream.SetLength(0);
        using var writer = new StreamWriter(stream); await writer.WriteAsync(vm.ExportWorldValidationReport());
    }

    private async void BrowseMapBackground_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || !StorageProvider.CanOpen) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select a map background image",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Image") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp"] }]
        });
        var file = files.FirstOrDefault(); if (file is null) return;
        vm.SetMapBackgroundImagePath(file.Path.LocalPath);
    }

    private async void ExportActivity_Click(object? sender, RoutedEventArgs e) =>
        await ExportTextAsync("MystTiq-Activity", "Export visible MystTiq activity", vm => vm.ExportVisibleActivity());

    private async void ExportNotifications_Click(object? sender, RoutedEventArgs e) =>
        await ExportTextAsync("MystTiq-Notifications", "Export visible MystTiq notifications", vm => vm.ExportVisibleNotifications());

    private async Task ExportTextAsync(string prefix, string title, Func<MainWindowViewModel, string> content)
    {
        if (DataContext is not MainWindowViewModel vm || !StorageProvider.CanSave) return;
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title, SuggestedFileName = $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss}.txt", DefaultExtension = "txt",
            FileTypeChoices = [new FilePickerFileType("Text file") { Patterns = ["*.txt"] }]
        });
        if (file is null) return;
        await using var stream = await file.OpenWriteAsync(); stream.SetLength(0);
        using var writer = new StreamWriter(stream); await writer.WriteAsync(content(vm));
    }

}
