using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MystTiq.Desktop.ViewModels;
using System.Diagnostics;
using System.IO.Compression;
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

    private void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (Application.Current is App app && !app.IsExplicitExitRequested)
        {
            e.Cancel = true;
            app.HideMainWindowToTray();
        }
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
