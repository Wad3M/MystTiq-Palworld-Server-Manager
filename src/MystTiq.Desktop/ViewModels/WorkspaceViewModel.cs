// MystTiq v1.1.5.0: file reviewed for this release (2026-10-22).
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;
using MystTiq.Core.Services;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Windows.Input;

namespace MystTiq.Desktop.ViewModels;

/// <summary>
/// v1.0.8.4 (roadmap C-4): the Workspace page -- where MystTiq and the server keep their files, the active world and the
/// world files beside it, their integrity check, and getting ready for a world import -- moved out of MainWindowViewModel
/// unchanged. The window shows it as vm.Workspace; the paths come from the Configuration page.
/// </summary>
public sealed class WorkspaceViewModel : PageViewModel
{
    private ConfigurationViewModel Configuration { get; }
    private ObservableCollection<EnvironmentChecklistItemDto> EnvironmentItems { get; }

    public WorkspaceViewModel(SessionContext session, ConfigurationViewModel configuration, ObservableCollection<EnvironmentChecklistItemDto> environmentItems) : base(session, NavigationPage.Workspace)
    {
        Configuration = configuration;
        EnvironmentItems = environmentItems;
        ValidateWorkspaceCommand = new RelayCommand(ValidateWorkspacePaths, () => Configuration.ConfigLoaded);
        ValidateActiveWorldCommand = new AsyncCommand(ValidateActiveWorldAsync, () => !Session.IsBusy && Session.IsConnected);
    }

    private string _workspaceState = "Load the managed configuration to inspect server paths.";

    private string _activeWorldIdText = "—";

    private string _worldCountText = "—";

    private string _worldFileCountText = "—";

    private string _worldPlayerSaveCountText = "—";

    private string _worldSizeText = "—";

    private string _worldSaveRootText = "—";

    private string _worldSaveDataCountText = "—";

    private string _worldDiagnosticCountText = "—";

    private string _worldEmptyCountText = "—";

    private string _worldAgeRangeText = "—";

    private string _worldIntegrityState = "Not checked";

    private WorldFileDto? _selectedWorldFile;

    public ObservableCollection<WorldCandidateDto> WorldCandidates { get; } = [];

    public ObservableCollection<WorldFileDto> WorldFiles { get; } = [];

    public ObservableCollection<string> WorldIntegrityFindings { get; } = [];

    public string WorkspaceState { get => _workspaceState; internal set => SetField(ref _workspaceState, value); }

    public string ActiveWorldIdText { get => _activeWorldIdText; internal set => SetField(ref _activeWorldIdText, value); }

    public string WorldCountText { get => _worldCountText; internal set => SetField(ref _worldCountText, value); }

    public string WorldFileCountText { get => _worldFileCountText; internal set => SetField(ref _worldFileCountText, value); }

    public string WorldPlayerSaveCountText { get => _worldPlayerSaveCountText; internal set => SetField(ref _worldPlayerSaveCountText, value); }

    public string WorldSizeText { get => _worldSizeText; internal set => SetField(ref _worldSizeText, value); }

    public string WorldSaveRootText { get => _worldSaveRootText; internal set => SetField(ref _worldSaveRootText, value); }

    public string WorldSaveDataCountText { get => _worldSaveDataCountText; internal set => SetField(ref _worldSaveDataCountText, value); }

    public string WorldDiagnosticCountText { get => _worldDiagnosticCountText; internal set => SetField(ref _worldDiagnosticCountText, value); }

    public string WorldEmptyCountText { get => _worldEmptyCountText; internal set => SetField(ref _worldEmptyCountText, value); }

    public string WorldAgeRangeText { get => _worldAgeRangeText; internal set => SetField(ref _worldAgeRangeText, value); }

    public string WorldIntegrityState { get => _worldIntegrityState; internal set => SetField(ref _worldIntegrityState, value); }

    public WorldFileDto? SelectedWorldFile { get => _selectedWorldFile; set => SetField(ref _selectedWorldFile, value); }

    public ICommand ValidateActiveWorldCommand { get; }

    public string WorkspaceDeploymentMode => !Session.IsLocalProfile ? "REMOTE" : AppContext.BaseDirectory.Contains("artifacts", StringComparison.OrdinalIgnoreCase) ? "PORTABLE / DEVELOPMENT" : "INSTALLED";

    public string WorkspaceHealthText => Configuration.ConfigLoaded ? "CONFIGURATION LOADED" : "NOT VALIDATED";

    public string WorkspaceDiscoveryText => EnvironmentItems.Count == 0 ? "NOT CHECKED" : EnvironmentItems.All(x => x.IsReady) ? "SERVER READY" : "ATTENTION REQUIRED";

    public string WorkspaceExecutableRoot => AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public string WorkspaceApplicationData => Path.Combine(PortableMode.CommonAppData, "MystTiqPalworldServer");

    public string WorkspaceRoot => Path.Combine(WorkspaceApplicationData, "Workspace");

    public string WorkspaceDownloadsRoot => Path.Combine(WorkspaceRoot, "Downloads");

    public string WorkspaceExportsRoot => Path.Combine(WorkspaceApplicationData, "Exports");

    public string WorkspaceLogsRoot => Path.Combine(WorkspaceApplicationData, "Logs");

    public ICommand ValidateWorkspaceCommand { get; }

    public bool TryGetLocalWorkspacePath(string key, out string path)
    {
        path = key switch
        {
            "server" => Configuration.ConfigServerRoot,
            "steamcmd" => Configuration.ConfigSteamCmdPath,
            "backup" => Configuration.ConfigBackupRoot,
            "runtime" => Configuration.ConfigRuntimeRoot,
            "executable" => WorkspaceExecutableRoot,
            "workspace" => WorkspaceRoot,
            "appdata" => WorkspaceApplicationData,
            "downloads" => WorkspaceDownloadsRoot,
            "exports" => WorkspaceExportsRoot,
            "logs" => WorkspaceLogsRoot,
            _ => string.Empty
        };
        if (!Session.IsLocalProfile)
        {
            WorkspaceState = "Open is unavailable for remote profiles because server paths do not belong to this computer.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(path))
        {
            WorkspaceState = "The selected workspace path is empty.";
            return false;
        }
        return true;
    }

    internal void RaiseWorkspaceSummaryProperties()
    {
        RaisePropertyChanged(nameof(WorkspaceDeploymentMode));
        RaisePropertyChanged(nameof(WorkspaceHealthText));
        RaisePropertyChanged(nameof(WorkspaceDiscoveryText));
    }

    internal void ValidateWorkspacePaths()
    {
        var paths = new[] { ("Server root", Configuration.ConfigServerRoot), ("SteamCMD", Configuration.ConfigSteamCmdPath), ("Backup root", Configuration.ConfigBackupRoot), ("Runtime root", Configuration.ConfigRuntimeRoot) };
        var invalid = paths.Where(x => string.IsNullOrWhiteSpace(x.Item2) || !IsAbsoluteServerPath(x.Item2.Trim())).Select(x => x.Item1).ToArray();
        WorkspaceState = invalid.Length == 0
            ? "Path syntax is valid. Save Paths uses the authenticated headless configuration route for authoritative validation and rollback-safe persistence."
            : $"Path validation failed: {string.Join(", ", invalid)} must use absolute server paths.";
    }

    internal async Task ValidateActiveWorldAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var report = await Session.Api.ValidateActiveWorldAsync(Session.SelectedProfile, Session.BearerToken);
            WorldValidationFindings.Clear();
            foreach (var finding in report.Findings) WorldValidationFindings.Add(finding);
            WorldValidationState = report.Summary;
        }
        catch (Exception ex) { WorldValidationState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    public ObservableCollection<WorldValidationFindingDto> WorldValidationFindings { get; } = [];

    public string WorldValidationState { get => _worldValidationState; private set => SetField(ref _worldValidationState, value); }

    private string _worldValidationState = "Not validated";

    private static bool IsAbsoluteServerPath(string value) =>
        value.StartsWith("/", StringComparison.Ordinal) || value.StartsWith("\\\\", StringComparison.Ordinal) ||
        (value.Length >= 3 && char.IsLetter(value[0]) && value[1] == ':' && (value[2] == '\\' || value[2] == '/'));
}
