// MystTiq v1.1.1.0: file reviewed for this release (2026-10-14).
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
/// v1.0.8.5 (roadmap C-5): the Backups page (the archive list, create, delete, restore, verify one or all with the scan's
/// progress, retention) and the Save Tools page -- moved out of MainWindowViewModel unchanged. The window shows them as
/// vm.Backups; Backup All stays with the fleet actions.
/// </summary>
public sealed class BackupsViewModel : PageViewModel
{
    public BackupsViewModel(SessionContext session) : base(session, NavigationPage.Backups)
    {
        FixSaveFolderAccessCommand = new AsyncCommand(FixSaveFolderAccessAsync, () => !Session.IsBusy);
        RefreshSaveToolsCommand = new AsyncCommand(() => RefreshSaveToolsAsync(false), () => !Session.IsBusy);
        RunSaveToolsSelfTestCommand = new AsyncCommand(() => RefreshSaveToolsAsync(true), () => !Session.IsBusy && Session.IsConnected);
        RefreshBackupsCommand = new AsyncCommand(RefreshBackupsAsync, () => !Session.IsBusy);
        CreateBackupCommand = new AsyncCommand(CreateBackupAsync, () => !Session.IsBusy && Session.IsConnected);
        DeleteBackupCommand = new AsyncCommand(DeleteBackupAsync, () => !Session.IsBusy && SelectedBackup is not null);
        RestoreBackupCommand = new AsyncCommand(RestoreBackupAsync, () => !Session.IsBusy && SelectedBackup is not null && BackupRestoreConfirmed);
        VerifySelectedBackupCommand = new AsyncCommand(VerifySelectedBackupAsync, () => !Session.IsBusy && SelectedBackup is not null);
        VerifyAllBackupsCommand = new AsyncCommand(VerifyAllBackupsAsync, () => !Session.IsBusy && Session.IsConnected);
        CancelBackupScanCommand = new RelayCommand(() => Session.RunUi(() => CancelBackupScanAsync()));
        PreviewBackupRetentionCommand = new AsyncCommand(PreviewBackupRetentionAsync, () => !Session.IsBusy && Session.IsConnected);
        ApplyBackupRetentionCommand = new AsyncCommand(ApplyBackupRetentionAsync, () => !Session.IsBusy && !string.IsNullOrWhiteSpace(BackupRetentionToken));
        OpenBackupRootCommand = new RelayCommand(OpenBackupRoot, () => !string.IsNullOrWhiteSpace(BackupRootPath));
    }

    private BackupItemDto? _selectedBackup;

    private string _backupState = "Not loaded";

    private string _backupTotalSizeText = "—";

    private string _backupDetail = "Connect to load backup inventory.";

    private string _backupRootPath = string.Empty;

    private bool _backupRestoreConfirmed;

    private int _backupRetentionKeepLatest = 10;

    private int _backupRetentionMaxAgeDays = 30;

    private string _backupRetentionToken = string.Empty;

    private string _backupRetentionState = "Preview a policy before cleanup.";

    private string _saveToolsState = "Open Save Tools to inspect server-side dependencies and saves.";

    private string _saveToolsPaths = "Not inspected";

    public ObservableCollection<SaveToolsTestDto> SaveToolsTests { get; } = [];

    public ObservableCollection<BackupItemDto> BackupItems { get; } = [];

    public ObservableCollection<BackupRetentionItemDto> BackupRetentionItems { get; } = [];

    public BackupItemDto? SelectedBackup { get => _selectedBackup; set { if (SetField(ref _selectedBackup, value)) { BackupRestoreConfirmed = false; RaiseBackupCommandStates(); } } }

    public string BackupState { get => _backupState; internal set => SetField(ref _backupState, value); }

    public string BackupTotalSizeText { get => _backupTotalSizeText; internal set => SetField(ref _backupTotalSizeText, value); }

    public string BackupDetail { get => _backupDetail; internal set => SetField(ref _backupDetail, value); }

    public string BackupRootPath { get => _backupRootPath; private set { if (SetField(ref _backupRootPath, value)) (OpenBackupRootCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }

    public bool BackupRestoreConfirmed { get => _backupRestoreConfirmed; set { if (SetField(ref _backupRestoreConfirmed, value)) RaiseBackupCommandStates(); } }

    public int BackupRetentionKeepLatest { get => _backupRetentionKeepLatest; set { if (SetField(ref _backupRetentionKeepLatest, Math.Clamp(value, 1, 1000))) InvalidateRetentionPreview(); } }

    public int BackupRetentionMaxAgeDays { get => _backupRetentionMaxAgeDays; set { if (SetField(ref _backupRetentionMaxAgeDays, Math.Clamp(value, 1, 3650))) InvalidateRetentionPreview(); } }

    public string BackupRetentionToken { get => _backupRetentionToken; private set { if (SetField(ref _backupRetentionToken, value)) (ApplyBackupRetentionCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }

    public string BackupRetentionState { get => _backupRetentionState; private set => SetField(ref _backupRetentionState, value); }

    public ICommand RefreshSaveToolsCommand { get; }

    public ICommand RunSaveToolsSelfTestCommand { get; }

    public ICommand RefreshBackupsCommand { get; }

    public ICommand CreateBackupCommand { get; }

    public ICommand DeleteBackupCommand { get; }

    public ICommand RestoreBackupCommand { get; }

    public ICommand VerifySelectedBackupCommand { get; }

    public ICommand VerifyAllBackupsCommand { get; }

    public ICommand CancelBackupScanCommand { get; }

    // v1.0.7.2 (roadmap Q-12): the backup scan's progress ("Verifying backups: 3 of 47"), whether it runs, and what it could not read.
    private string _backupScanText = string.Empty;

    private bool _isBackupScanRunning;

    public string BackupScanText { get => _backupScanText; private set { if (SetField(ref _backupScanText, value)) RaisePropertyChanged(nameof(HasBackupScan)); } }

    public bool HasBackupScan => BackupScanText.Length > 0;

    public bool IsBackupScanRunning { get => _isBackupScanRunning; private set => SetField(ref _isBackupScanRunning, value); }

    public ObservableCollection<BackupScanProblemDto> BackupScanProblems { get; } = [];

    private void ApplyBackupScan(BackupScanProgressDto? scan)
    {
        BackupScanText = scan?.Text ?? string.Empty;
        IsBackupScanRunning = scan?.Running == true;
        BackupScanProblems.Clear();
        foreach (var problem in scan?.Unreadable ?? []) BackupScanProblems.Add(problem);
    }

    private async Task CancelBackupScanAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { BackupDetail = ex.Message; return; }
        await Session.Api.CancelBackupScanAsync(profile, Session.BearerToken);
        BackupDetail = "Cancelling: the scan stops after the backup in hand.";
    }

    public ICommand PreviewBackupRetentionCommand { get; }

    public ICommand ApplyBackupRetentionCommand { get; }

    public ICommand OpenBackupRootCommand { get; }

    public string SaveToolsState { get => _saveToolsState; private set => SetField(ref _saveToolsState, value); }

    public string SaveToolsPaths { get => _saveToolsPaths; private set => SetField(ref _saveToolsPaths, value); }

    public string BackupArchiveCountText => BackupItems.Count.ToString();

    public string BackupVerifiedCountText => BackupItems.Count(x => x.VerificationText.Contains("Verified", StringComparison.OrdinalIgnoreCase)).ToString();

    public string BackupPendingCountText => BackupItems.Count(x => !x.VerificationText.Contains("Verified", StringComparison.OrdinalIgnoreCase)).ToString();

    // v0.7.30.0 bug fix: BackupItems used to be populated at three separate call sites, each with
    // its own Clear()+foreach(Add) -- but only one of the three also raised change notifications for
    // the three computed properties above, so the summary cards silently stayed at 0 no matter how
    // many backups actually existed, even though the list itself (which observes BackupItems'
    // CollectionChanged directly) rendered correctly. All three call sites now go through this one
    // helper so the notification can't be forgotten a fourth time.
    internal void PopulateBackupItems(BackupInventoryDto inventory)
    {
        ApplySaveFolderAccess(inventory);
        ApplyBackupScan(inventory.Scan);
        BackupItems.Clear();
        foreach (var item in inventory.Items) BackupItems.Add(item);
        RaisePropertyChanged(nameof(BackupArchiveCountText));
        RaisePropertyChanged(nameof(BackupVerifiedCountText));
        RaisePropertyChanged(nameof(BackupPendingCountText));
    }

    internal async Task RefreshSaveToolsAsync(bool selfTest)
    {
        if (Session.SelectedProfile is null) return; Session.IsBusy = true; SaveToolsState = selfTest ? "Running bounded server-side self-tests…" : "Inspecting Save Tools…";
        try
        {
            var diagnostics = selfTest
                ? await Session.Api.RunSaveToolsSelfTestAsync(Session.SelectedProfile, Session.BearerToken)
                : await Session.Api.GetSaveToolsDiagnosticsAsync(Session.SelectedProfile, Session.BearerToken);
            var inventory = await Session.Api.GetSaveFilesAsync(Session.SelectedProfile, Session.BearerToken);
            SaveToolsTests.Clear(); foreach (var test in diagnostics.Tests) SaveToolsTests.Add(test);
            SaveFiles.Clear(); foreach (var file in inventory.Items) SaveFiles.Add(file);
            SelectedSaveFile = SaveFiles.FirstOrDefault();
            SaveToolsState = $"{(diagnostics.Ready ? "READY" : "INCOMPLETE")} · {diagnostics.Detail} {inventory.Detail}";
            SaveToolsPaths = $"Python: {diagnostics.PythonPath ?? "Missing"}\nLegacy converter: {diagnostics.LegacyConverterPath ?? "Missing"}\nPlM converter: {diagnostics.PlmConverterPath ?? "Missing"}\nOodle: {diagnostics.OodlePath ?? "Missing"}\nActive Level.sav: {diagnostics.ActiveLevelSavePath ?? "Not found"}\nSignature: {diagnostics.ActiveLevelSignature}";
        }
        catch (Exception ex) { SaveToolsState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    public string ExportSaveToolsDiagnostics() => JsonSerializer.Serialize(new { state = SaveToolsState, paths = SaveToolsPaths, tests = SaveToolsTests, files = SaveFiles }, new JsonSerializerOptions { WriteIndented = true });

    internal async Task RefreshBackupsAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { BackupDetail = ex.Message; return; }

        Session.IsBusy = true;
        try
        {
            var selectedFileName = SelectedBackup?.FileName;
            var inventory = await Session.Api.GetBackupsAsync(profile, Session.BearerToken);
            PopulateBackupItems(inventory);

            SelectedBackup = BackupItems.FirstOrDefault(x => x.FileName == selectedFileName) ?? BackupItems.FirstOrDefault();
            BackupState = $"{inventory.Count} backup(s)";
            BackupTotalSizeText = $"{inventory.TotalSizeBytes / 1024d / 1024d:F2} MB";
            BackupRootPath = inventory.RootPath;
            BackupDetail = inventory.Detail;
        }
        catch (Exception ex)
        {
            BackupState = "Unavailable";
            BackupDetail = ex.Message;
        }
        finally { Session.IsBusy = false; }
    }

    private async Task CreateBackupAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { BackupDetail = ex.Message; return; }

        Session.BusyReason = "Creating backup…";
        Session.IsBusy = true;
        BackupState = "Creating…";
        try
        {
            var result = await Session.Api.CreateBackupAsync(profile, Session.BearerToken);
            BackupDetail = result.Message;
            await RefreshBackupsCoreAsync(profile);
        }
        catch (Exception ex) { BackupDetail = ex.Message; }
        finally { Session.IsBusy = false; Session.BusyReason = null; }
    }

    private async Task DeleteBackupAsync()
    {
        if (SelectedBackup is null) return;

        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { BackupDetail = ex.Message; return; }

        var fileName = SelectedBackup.FileName;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.DeleteBackupAsync(profile, fileName, Session.BearerToken);
            BackupDetail = result.Message;
            await RefreshBackupsCoreAsync(profile);
        }
        catch (Exception ex) { BackupDetail = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task RestoreBackupAsync()
    {
        if (SelectedBackup is null) return;

        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { BackupDetail = ex.Message; return; }

        var fileName = SelectedBackup.FileName;
        Session.BusyReason = "Restoring backup…";
        Session.IsBusy = true;
        BackupState = "Restoring…";
        try
        {
            var result = await Session.Api.RestoreBackupAsync(profile, fileName, Session.BearerToken);
            BackupDetail = result.Message;
            await RefreshBackupsCoreAsync(profile);
        }
        catch (Exception ex) { BackupDetail = ex.Message; }
        finally { Session.IsBusy = false; Session.BusyReason = null; }
    }

    private async Task VerifySelectedBackupAsync()
    {
        if (SelectedBackup is null) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { BackupDetail = ex.Message; return; }
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.VerifyBackupAsync(profile, SelectedBackup.FileName, Session.BearerToken);
            BackupDetail = $"{result.Message} SHA-256: {result.Sha256 ?? "unavailable"}; entries: {result.EntryCount}.";
            await RefreshBackupsCoreAsync(profile);
        }
        catch (Exception ex) { BackupDetail = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task VerifyAllBackupsAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { BackupDetail = ex.Message; return; }
        Session.IsBusy = true;
        try
        {
            // v1.0.7.2 (roadmap Q-12): the progress is read every 1.5 s while Verify All runs.
            var verify = Session.Api.VerifyAllBackupsAsync(profile, Session.BearerToken);
            while (await Task.WhenAny(verify, Task.Delay(1500)) != verify)
            {
                try { ApplyBackupScan((await Session.Api.GetBackupsAsync(profile, Session.BearerToken)).Scan); }
                catch (Exception) { /* progress is a convenience; the verify itself still reports */ }
            }
            var result = await verify;
            BackupDetail = $"{result.Message} Passed: {result.Results.Count(x => x.Success)}; failed: {result.Results.Count(x => !x.Success)}.";
            await RefreshBackupsCoreAsync(profile);
        }
        catch (Exception ex) { BackupDetail = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    internal async Task PreviewBackupRetentionAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { BackupRetentionState = ex.Message; return; }
        Session.IsBusy = true;
        try
        {
            var preview = await Session.Api.PreviewBackupRetentionAsync(profile, BackupRetentionKeepLatest, BackupRetentionMaxAgeDays, Session.BearerToken);
            BackupRetentionItems.Clear();
            foreach (var item in preview.Items) BackupRetentionItems.Add(item);
            BackupRetentionToken = preview.Token;
            BackupRetentionState = $"{preview.Message} Reclaim: {preview.ReclaimBytes / 1024d / 1024d:F2} MB. Preview expires {preview.ExpiresAt.ToLocalTime():HH:mm:ss}.";
        }
        catch (Exception ex) { InvalidateRetentionPreview(); BackupRetentionState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    internal async Task ApplyBackupRetentionAsync()
    {
        if (string.IsNullOrWhiteSpace(BackupRetentionToken)) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { BackupRetentionState = ex.Message; return; }
        var token = BackupRetentionToken;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ApplyBackupRetentionAsync(profile, token, Session.BearerToken);
            BackupRetentionState = $"{result.Message} Deleted: {result.DeletedCount}; reclaimed: {result.ReclaimedBytes / 1024d / 1024d:F2} MB.";
            BackupRetentionToken = string.Empty;
            BackupRetentionItems.Clear();
            await RefreshBackupsCoreAsync(profile);
        }
        catch (Exception ex) { InvalidateRetentionPreview(); BackupRetentionState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private void InvalidateRetentionPreview()
    {
        BackupRetentionToken = string.Empty;
        BackupRetentionItems.Clear();
        BackupRetentionState = "Policy changed. Preview again before cleanup.";
    }

    private void OpenBackupRoot()
    {
        if (Session.SelectedProfile?.Id != ConnectionProfile.LocalDefault.Id)
        {
            BackupDetail = $"Remote backup root: {BackupRootPath}. Use the server-side inventory; MystTiq will not open a path on the GUI computer.";
            return;
        }
        try
        {
            var start = OperatingSystem.IsWindows()
                ? new ProcessStartInfo("explorer.exe", BackupRootPath)
                : new ProcessStartInfo("xdg-open", BackupRootPath);
            start.UseShellExecute = true;
            Process.Start(start);
            BackupDetail = $"Opened local backup root: {BackupRootPath}";
        }
        catch (Exception ex) { BackupDetail = "Unable to open the local backup root: " + ex.Message; }
    }

    private void RaiseBackupCommandStates()
    {
        (DeleteBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RestoreBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (VerifySelectedBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
    }

    private async Task RefreshBackupsCoreAsync(ConnectionProfile profile)
    {
        var selectedFileName = SelectedBackup?.FileName;
        var inventory = await Session.Api.GetBackupsAsync(profile, Session.BearerToken);
        PopulateBackupItems(inventory);
        SelectedBackup = BackupItems.FirstOrDefault(x => x.FileName == selectedFileName) ?? BackupItems.FirstOrDefault();
        BackupState = $"{inventory.Count} backup(s)";
        BackupTotalSizeText = $"{inventory.TotalSizeBytes / 1024d / 1024d:F2} MB";
        BackupRootPath = inventory.RootPath;
    }

    public ObservableCollection<SaveFileDto> SaveFiles { get; } = [];

    public SaveFileDto? SelectedSaveFile { get => _selectedSaveFile; set => SetField(ref _selectedSaveFile, value); }

    private SaveFileDto? _selectedSaveFile;

    // v1.0.0.4: the Backups page's save-folder access warning and its fix (see SaveFolderAccessFix).
    private bool _saveFolderReplaceable = true;
    private string _savedFolderPathVerbatim = string.Empty;
    private string _saveFolderAccessStatus = string.Empty;

    // Shown for a server on this computer whose save folder restores cannot replace.
    public bool ShowSaveFolderAccessFix => !_saveFolderReplaceable && ElevatedFirewall.CanRun(Session.SelectedProfile) && _savedFolderPathVerbatim.Length > 0;
    public string SavedFolderPathVerbatim { get => _savedFolderPathVerbatim; private set => SetField(ref _savedFolderPathVerbatim, value); }
    public string SaveFolderAccessStatus { get => _saveFolderAccessStatus; private set => SetField(ref _saveFolderAccessStatus, value); }

    public ICommand FixSaveFolderAccessCommand { get; }

    private void ApplySaveFolderAccess(BackupInventoryDto inventory)
    {
        _saveFolderReplaceable = inventory.SaveFolderReplaceable;
        SavedFolderPathVerbatim = inventory.SavedFolderPath ?? string.Empty;
        RaisePropertyChanged(nameof(ShowSaveFolderAccessFix));
    }

    private async Task FixSaveFolderAccessAsync()
    {
        if (Session.SelectedProfile is null || SavedFolderPathVerbatim.Length == 0) return;
        Session.IsBusy = true;
        try
        {
            var (_, message) = await SaveFolderAccessFix.RunAsync(SavedFolderPathVerbatim);
            SaveFolderAccessStatus = message;
            await RefreshBackupsCoreAsync(Session.SelectedProfile);
        }
        catch (Exception ex) { SaveFolderAccessStatus = ex.Message; }
        finally { Session.IsBusy = false; }
    }
}
