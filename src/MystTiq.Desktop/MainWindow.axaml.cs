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
            // v0.7.87.0: DataContext is assigned after this constructor runs (see App.axaml.cs's
            // object-initializer `new MainWindow { DataContext = viewModel }`), so the subscription
            // has to happen reactively here rather than inline above.
            else if (args.Property == DataContextProperty && args.NewValue is MainWindowViewModel vm)
                vm.ConfigurationUnsavedChangesNavigationBlocked += () => _ = OnConfigurationUnsavedChangesNavigationBlockedAsync();
        };
        UpdateMaximizeGlyph();

        // v0.7.78.0: drag-and-drop MOD ZIP install (direct request). Avalonia's DragOver/Drop are
        // attached routed events, not plain CLR events, so they're wired via AddHandler here rather
        // than a XAML Click-style attribute.
        ZipInstallDropZone.AddHandler(DragDrop.DragOverEvent, ZipInstallDropZone_OnDragOver);
        ZipInstallDropZone.AddHandler(DragDrop.DropEvent, ZipInstallDropZone_OnDrop);
    }

    private void Window_OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateMaximizeGlyph();
        if (DataContext is MainWindowViewModel vm)
            vm.UpdateServerSetupTableHeight(e.NewSize.Height);
    }

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
        switch (result)
        {
            case Views.ConfirmMinimizeToTrayResult.MinimizeToTray:
                app.HideMainWindowToTray();
                app.ShowTrayStillRunningReminder("A Palworld server is still running. MystTiq will keep managing it in the background -- open the tray icon to bring the window back, or to stop the server and exit.");
                break;
            // v0.7.74.0: same real exit paths the tray menu's own Safe Exit/Force Exit already use,
            // now reachable directly from the close-confirm dialog instead of requiring an extra
            // minimize-then-find-the-tray-icon round trip.
            case Views.ConfirmMinimizeToTrayResult.SafeExit:
                await app.SafeExitAsync();
                break;
            case Views.ConfirmMinimizeToTrayResult.ForceExit:
                await app.ForceExitAsync();
                break;
        }
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

    // v0.7.87.0: direct live feedback -- navigating away from Configuration with unsaved changes
    // previously discarded them silently. The ViewModel raises ConfigurationUnsavedChangesNavigationBlocked
    // (it has no Window reference of its own, same constraint as CloseTabButton_OnClick above) and
    // this shows the confirm dialog, then calls back into whichever continuation the user picked.
    private async Task OnConfigurationUnsavedChangesNavigationBlockedAsync()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var dialog = new Views.ConfirmSaveDiscardDialog(vm.PalworldConfigDirtyText);
        var result = await dialog.ShowDialog<Views.ConfirmSaveDiscardResult>(this);
        switch (result)
        {
            case Views.ConfirmSaveDiscardResult.Save:
                await vm.ContinuePendingNavigationSavingConfigurationAsync();
                break;
            case Views.ConfirmSaveDiscardResult.Discard:
                vm.ContinuePendingNavigationDiscardingConfigurationChanges();
                break;
            case Views.ConfirmSaveDiscardResult.Cancel:
                vm.CancelPendingConfigurationNavigation();
                break;
        }
    }

    // v0.7.75.0: "Delete Player Completely" -- the ViewModel has no Window reference for the
    // dialog, so code-behind owns the Preview -> confirm dialog -> Apply sequence, matching
    // CloseTabButton_OnClick's own split above.
    private async void DeletePlayer_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedPlayerRecord is null) return;
        var preview = await vm.PreviewDeleteSelectedPlayerAsync();
        if (preview is not { CanApply: true }) return;

        var dialog = new Views.ConfirmDeletePlayerDialog(preview.PlayerName, preview.Findings);
        var result = await dialog.ShowDialog<Views.ConfirmDeletePlayerResult>(this);
        if (result != Views.ConfirmDeletePlayerResult.Delete) return;

        await vm.ApplyDeleteSelectedPlayerAsync(preview.PreviewToken);
    }

    // v0.7.75.0: "Copy Player" -- picks a source player from a populated dropdown (not a
    // hand-typed ID) via SelectPlayerDialog, then the same Preview -> confirm -> Apply shape as
    // deletion above.
    private async void CopyIntoPlayer_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedPlayerRecord is null) return;
        var destination = vm.SelectedPlayerRecord;

        var options = vm.FilteredPlayerRecords
            .Where(p => p.PlayerId != destination.PlayerId)
            .Select(p => new Views.SelectPlayerOption(p.PlayerId, p.PlayerName))
            .ToList();
        if (options.Count == 0) return;

        var picker = new Views.SelectPlayerDialog($"Copy data onto {destination.PlayerName} from which player?", options);
        var sourceId = await picker.ShowDialog<string?>(this);
        if (string.IsNullOrWhiteSpace(sourceId)) return;

        var preview = await vm.PreviewCopyPlayerAsync(sourceId, destination.PlayerId);
        if (preview is not { CanApply: true }) return;

        var dialog = new Views.ConfirmCopyPlayerDialog(preview.SourcePlayerName, preview.DestinationPlayerName, preview.Findings);
        var result = await dialog.ShowDialog<Views.ConfirmCopyPlayerResult>(this);
        if (result != Views.ConfirmCopyPlayerResult.Copy) return;

        await vm.ApplyCopyPlayerAsync(preview.PreviewToken);
    }

    // v0.7.76.0: Base/Guild right-click workflow -- same Preview -> confirm -> Apply shape as
    // Player Delete/Copy above, driving the exact same already-shipped Base/Guild ownership API
    // the in-page cards use, just via a populated dropdown picker instead of a hand-typed ID.
    private async void TransferBase_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedExplorerBase is null) return;
        var baseItem = vm.SelectedExplorerBase;

        var options = vm.FilteredExplorerGuilds
            .Where(g => g.GuildId != baseItem.GuildId)
            .Select(g => new Views.SelectGuildOption(g.GuildId, g.GuildName))
            .ToList();
        if (options.Count == 0) return;

        var picker = new Views.SelectGuildDialog($"Transfer base {baseItem.BaseId} to which guild?", options);
        var targetGuildId = await picker.ShowDialog<string?>(this);
        if (string.IsNullOrWhiteSpace(targetGuildId)) return;

        var preview = await vm.PreviewTransferBaseAsync(baseItem.BaseId, targetGuildId);
        if (preview is not { CanApply: true }) return;

        var dialog = new Views.ConfirmOperationDialog(
            $"Transfer base {baseItem.BaseId} from {preview.SourceGuildName} to {preview.TargetGuildName}?",
            preview.Findings, "Transfer", danger: false);
        if (await dialog.ShowDialog<bool>(this) != true) return;

        await vm.ApplyTransferBaseAsync(preview.PreviewToken);
    }

    private async void WipeBase_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedExplorerBase is null) return;
        var baseItem = vm.SelectedExplorerBase;

        var preview = await vm.PreviewWipeBaseAsync(baseItem.BaseId);
        if (preview is not { CanApply: true }) return;

        var dialog = new Views.ConfirmOperationDialog(
            $"Permanently wipe base {baseItem.BaseId}?",
            preview.Findings, "Wipe Permanently", danger: true);
        if (await dialog.ShowDialog<bool>(this) != true) return;

        await vm.ApplyWipeBaseAsync(preview.PreviewToken);
    }

    private async void CopyBaseInfo_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedExplorerBase is null) return;
        var b = vm.SelectedExplorerBase;
        var text = $"Base ID: {b.BaseId}\nGuild: {b.GuildName} ({b.GuildId})\nLeader: {b.LeaderName} ({b.LeaderPlayerId})\nHealth: {b.OwnerHealth}\nEvidence: {b.Evidence}";
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null) await clipboard.SetTextAsync(text);
    }

    private async void TransferGuildLeadership_OnClick(object? sender, RoutedEventArgs e) =>
        await RunGuildOperationAsync("transfer-leadership", "Transfer leadership of {0} to which player?", "Transfer Leadership", danger: false);

    private async void AddPlayerToGuild_OnClick(object? sender, RoutedEventArgs e) =>
        await RunGuildOperationAsync("add-player", "Add which player to {0}?", "Add Player", danger: false);

    private async void RemoveBrokenGuildMember_OnClick(object? sender, RoutedEventArgs e) =>
        await RunGuildOperationAsync("remove-broken-member", "Remove which broken member reference from {0}?", "Remove Member", danger: true);

    private async void ClaimOrphanedGuild_OnClick(object? sender, RoutedEventArgs e) =>
        await RunGuildOperationAsync("claim", "Which player should claim orphaned guild {0}?", "Claim Guild", danger: false);

    private async Task RunGuildOperationAsync(string operationApiName, string promptFormat, string confirmLabel, bool danger)
    {
        if (DataContext is not MainWindowViewModel vm || vm.SelectedExplorerGuild is null) return;
        var guild = vm.SelectedExplorerGuild;

        var options = vm.FilteredPlayerRecords
            .Select(p => new Views.SelectPlayerOption(p.PlayerId, p.PlayerName))
            .ToList();
        if (options.Count == 0) return;

        var picker = new Views.SelectPlayerDialog(string.Format(promptFormat, guild.GuildName), options);
        var playerId = await picker.ShowDialog<string?>(this);
        if (string.IsNullOrWhiteSpace(playerId)) return;

        var preview = await vm.PreviewGuildOperationForRowAsync(operationApiName, guild.GuildId, playerId);
        if (preview is not { CanApply: true }) return;

        var dialog = new Views.ConfirmOperationDialog(
            $"{confirmLabel}: {preview.PlayerName} in {preview.GuildName}?",
            preview.Findings, confirmLabel, danger);
        if (await dialog.ShowDialog<bool>(this) != true) return;

        await vm.ApplyGuildOperationForRowAsync(preview.PreviewToken);
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
        if (DataContext is not MainWindowViewModel vm || sender is not Button { Tag: string key }) return;
        // v0.7.88.0: the New Server wizard's Install Directory step reuses this same handler --
        // always local (see OpenNewServerTab), so it doesn't gate on IsLocalProfile like the
        // Workspace page's own rows below do (that guard reflects the currently-connected profile,
        // which for a not-yet-registered new server isn't meaningful).
        if (key == "installDirectory")
        {
            if (!StorageProvider.CanPickFolder) return;
            var installFolders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select install directory", AllowMultiple = false });
            var installFolder = installFolders.FirstOrDefault();
            if (installFolder is not null) vm.NewServerInstallDirectoryEffectivePath = installFolder.Path.LocalPath;
            return;
        }
        if (!vm.IsLocalProfile) return;
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

    // v0.7.82.0: Update Center's per-row "Open" link, direct live feedback ("we have a couple that
    // say unknown, we need to do better and have a way to check"). The row's own DataContext is the
    // ComponentVersionDto itself (an ItemsControl row, not a "selected item" like
    // OpenModDescriptionSource_Click above), so the URL comes from the clicked control, not the
    // page-level ViewModel.
    private void OpenComponentSourceUrl_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not ComponentVersionDto component) return;
        var url = component.SourceUrl;
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

    // v0.7.78.0: drag-and-drop counterpart to InstallModZip_Click above -- same install path, just
    // reached by dropping a .zip onto the card instead of using the file picker dialog.
    private void ZipInstallDropZone_OnDragOver(object? sender, DragEventArgs e)
    {
        var hasZip = e.DataTransfer.TryGetFiles()?.Any(f => f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) == true;
        e.DragEffects = hasZip ? DragDropEffects.Copy : DragDropEffects.None;
    }

    private async void ZipInstallDropZone_OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var file = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>()
            .FirstOrDefault(f => f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
        if (file is null) return;
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
