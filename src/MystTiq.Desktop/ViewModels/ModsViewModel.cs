// MystTiq v1.0.9.0: file reviewed for this release (2026-10-12).
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
/// v1.0.8.6 (roadmap C-6): the MOD pages -- the MOD dashboard and library, UE4SS and its releases, Safe Start, the Workshop
/// scan, Nexus Mods and the MOD browser -- moved out of MainWindowViewModel unchanged. The window shows them as vm.Mods.
/// </summary>
public sealed class ModsViewModel : PageViewModel
{
    /// <summary>Raised when Safe Start's status changes (the Dashboard's stuck-start panel follows it).</summary>
    public event Action? SafeStartStatusChanged;

    private INexusModsClient _nexus { get; }
    private CredentialStore _credentialStore { get; }
    private Action<string?> Navigate { get; }

    public ModsViewModel(SessionContext session, INexusModsClient nexus, CredentialStore credentialStore, Action<string?> navigate) : base(session, NavigationPage.ModDashboard)
    {
        _nexus = nexus;
        _credentialStore = credentialStore;
        Navigate = navigate;
        RefreshModsCommand = new AsyncCommand(RefreshModsAsync, () => !Session.IsBusy);
        VerifyModsCommand = new AsyncCommand(VerifyModsAsync, () => !Session.IsBusy);
        EnableSelectedModCommand = new AsyncCommand(() => SetSelectedModEnabledAsync(true), () => !Session.IsBusy);
        DisableSelectedModCommand = new AsyncCommand(() => SetSelectedModEnabledAsync(false), () => !Session.IsBusy);
        DeleteSelectedModCommand = new AsyncCommand(DeleteSelectedModAsync, () => !Session.IsBusy && SelectedMod is not null);
        RollbackSelectedModCommand = new AsyncCommand(RollbackSelectedModAsync, () => !Session.IsBusy && SelectedMod is not null);
        RepairSelectedModCommand = new AsyncCommand(RepairSelectedModAsync, () => !Session.IsBusy && SelectedMod is not null);
        EnableAllModsCommand = new AsyncCommand(() => SetAllModsEnabledAsync(true), () => !Session.IsBusy);
        DisableAllModsCommand = new AsyncCommand(() => SetAllModsEnabledAsync(false), () => !Session.IsBusy);
        RepairModsCommand = new AsyncCommand(RepairModsAsync, () => !Session.IsBusy);
        ScanWorkshopModsCommand = new AsyncCommand(ScanWorkshopModsAsync, () => !Session.IsBusy);
        ImportSelectedWorkshopModCommand = new AsyncCommand(ImportSelectedWorkshopModAsync, () => !Session.IsBusy && SelectedWorkshopItem is not null && !SelectedWorkshopItem.AlreadyInstalled);
        CheckSelectedModUpdateCommand = new AsyncCommand(CheckSelectedModUpdateAsync, () => !Session.IsBusy && SelectedMod is not null);
        PreviewUe4ssInstallCommand = new AsyncCommand(PreviewUe4ssInstallAsync, () => !Session.IsBusy && SelectedUe4ssRelease is not null);
        ApplyUe4ssInstallCommand = new AsyncCommand(ApplyUe4ssInstallAsync, () => !Session.IsBusy && !string.IsNullOrWhiteSpace(Ue4ssInstallToken));
        RollbackUe4ssInstallCommand = new AsyncCommand(RollbackUe4ssInstallAsync, () => !Session.IsBusy && Ue4ssRollbackAvailable);
        UpdateSelectedModCommand = new AsyncCommand(UpdateSelectedModAsync, () => !Session.IsBusy && _selectedModUpdateWorkshopId is not null);
        FetchSelectedModDescriptionCommand = new AsyncCommand(FetchSelectedModDescriptionAsync, () => !Session.IsBusy && SelectedMod is not null);
        SetSelectedModDescriptionSourceCommand = new AsyncCommand(SetSelectedModDescriptionSourceAsync, () => !Session.IsBusy && SelectedMod is not null);
        BeginModSafeStartCommand = new AsyncCommand(BeginModSafeStartAsync, () => !Session.IsBusy && ModSafeStartStatus is not { IsRunning: true });
        CancelModSafeStartCommand = new AsyncCommand(CancelModSafeStartAsync, () => ModSafeStartStatus is { IsRunning: true });
        DismissModSafeStartCommand = new RelayCommand(() => ModSafeStartStatus = null, () => ModSafeStartStatus is { IsRunning: false });
        InitializeNexusMods();
        InitializeModBrowser();
    }

    private string _modState = "Not loaded";

    private string _modSummary = "Connect to scan MODs.";

    private string _modHealth = "—";

    private string _modInstalledText = "—";

    private string _modConfirmedText = "—";

    private string _modUnverifiedText = "—";

    private string _modDisabledText = "—";

    private string _modIssuesText = "—";

    private string _ue4ssHealth = "—";

    private string _ue4ssDetection = "—";

    private string _ue4ssActiveRoot = "—";

    private string _ue4ssRuntimeRoot = "—";

    private string _ue4ssWarning = string.Empty;

    private string _ue4ssInstalledVersion = "Not detected";

    private string _selectedUe4ssFork = "Palworld Fork";

    private string _ue4ssReleaseCatalogStatusText = "Refresh Runtime also checks the release catalog.";

    private Ue4ssReleaseDto? _selectedUe4ssRelease;

    private string _ue4ssInstallToken = string.Empty;

    private string _ue4ssInstallState = "Select a release, then Preview Install.";

    private bool _ue4ssRollbackAvailable;

    private ModItemDto? _selectedMod;

    private WorkshopItemDto? _selectedWorkshopItem;

    private string _workshopScanState = "Refresh to scan local Steam Workshop content.";

    public ObservableCollection<ModItemDto> ModItems { get; } = [];

    public IEnumerable<ModItemDto> ModsNeedingAttention => ModItems.Where(m => m.Health is not ("Healthy" or "Disabled" or "Active / Unverified"));

    public bool HasModsNeedingAttention => ModsNeedingAttention.Any();

    public ObservableCollection<WorkshopItemDto> WorkshopItems { get; } = [];

    public string ModState { get => _modState; private set => SetField(ref _modState, value); }

    public string ModSummary { get => _modSummary; internal set => SetField(ref _modSummary, value); }

    public string ModHealth { get => _modHealth; private set => SetField(ref _modHealth, value); }

    // v0.7.78.0: drives the MOD Dashboard hero health banner's color (direct request to make
    // Dashboard feel distinct from MOD Library rather than a near-duplicate layout).
    public bool IsModHealthGood => ModHealth == "Healthy";

    public bool IsModHealthDegraded => ModHealth == "Degraded";

    public string ModInstalledText { get => _modInstalledText; private set => SetField(ref _modInstalledText, value); }

    public string ModConfirmedText { get => _modConfirmedText; private set => SetField(ref _modConfirmedText, value); }

    public string ModUnverifiedText { get => _modUnverifiedText; private set => SetField(ref _modUnverifiedText, value); }

    public string ModDisabledText { get => _modDisabledText; private set => SetField(ref _modDisabledText, value); }

    public string ModIssuesText { get => _modIssuesText; private set => SetField(ref _modIssuesText, value); }

    public string Ue4ssHealth { get => _ue4ssHealth; private set => SetField(ref _ue4ssHealth, value); }

    public string Ue4ssDetection { get => _ue4ssDetection; private set => SetField(ref _ue4ssDetection, value); }

    public string Ue4ssActiveRoot { get => _ue4ssActiveRoot; private set => SetField(ref _ue4ssActiveRoot, value); }

    public string Ue4ssRuntimeRoot { get => _ue4ssRuntimeRoot; private set => SetField(ref _ue4ssRuntimeRoot, value); }

    public string Ue4ssWarning { get => _ue4ssWarning; private set => SetField(ref _ue4ssWarning, value); }

    public string Ue4ssInstalledVersion { get => _ue4ssInstalledVersion; private set => SetField(ref _ue4ssInstalledVersion, value); }

    public IReadOnlyList<string> Ue4ssForkOptions { get; } = ["Palworld Fork", "Official Upstream"];

    public string SelectedUe4ssFork
    {
        get => _selectedUe4ssFork;
        set
        {
            if (SetField(ref _selectedUe4ssFork, value ?? "Palworld Fork"))
            {
                RaisePropertyChanged(nameof(Ue4ssVisibleReleases));
                RaisePropertyChanged(nameof(HasUe4ssReleases));
            }
        }
    }

    // v0.7.48.0: UE4SS Release Catalog -- real data for the "Release source" picker, replacing the
    // former client-side-only stub. Two source collections (one per GitHub repo), with a single
    // computed property the XAML binds to that switches based on SelectedUe4ssFork.
    public ObservableCollection<Ue4ssReleaseDto> Ue4ssPalworldForkReleases { get; } = [];

    public ObservableCollection<Ue4ssReleaseDto> Ue4ssOfficialUpstreamReleases { get; } = [];

    public IReadOnlyList<Ue4ssReleaseDto> Ue4ssVisibleReleases =>
        SelectedUe4ssFork == "Official Upstream" ? Ue4ssOfficialUpstreamReleases : Ue4ssPalworldForkReleases;

    public bool HasUe4ssReleases => Ue4ssVisibleReleases.Count > 0;

    public string Ue4ssReleaseCatalogStatusText { get => _ue4ssReleaseCatalogStatusText; private set => SetField(ref _ue4ssReleaseCatalogStatusText, value); }

    // v0.7.49.0: UE4SS Install/Rollback -- same Preview-then-Apply shape as backup retention cleanup
    // (Backups.BackupRetentionToken above): selecting a release or changing it invalidates any outstanding
    // preview token, so Apply can never fire against a preview that no longer matches the selection.
    public Ue4ssReleaseDto? SelectedUe4ssRelease
    {
        get => _selectedUe4ssRelease;
        set
        {
            if (SetField(ref _selectedUe4ssRelease, value))
            {
                var hadToken = !string.IsNullOrWhiteSpace(Ue4ssInstallToken);
                InvalidateUe4ssInstallPreview();
                if (hadToken) Ue4ssInstallState = "Selection changed. Preview Install again before confirming.";
                (PreviewUe4ssInstallCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public string Ue4ssInstallToken { get => _ue4ssInstallToken; private set { if (SetField(ref _ue4ssInstallToken, value)) (ApplyUe4ssInstallCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }

    public string Ue4ssInstallState { get => _ue4ssInstallState; private set => SetField(ref _ue4ssInstallState, value); }

    public bool Ue4ssRollbackAvailable { get => _ue4ssRollbackAvailable; private set { if (SetField(ref _ue4ssRollbackAvailable, value)) (RollbackUe4ssInstallCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }

    public ModItemDto? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (SetField(ref _selectedMod, value))
            {
                RaisePropertyChanged(nameof(HasSelectedMod));
                // v0.7.41.0: an update check result is only valid for the MOD it was run against;
                // switching selection must not leave a stale "update available" claim on screen.
                SelectedModUpdateText = string.Empty;
                _selectedModUpdateWorkshopId = null;
                (DeleteSelectedModCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                (RollbackSelectedModCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                (CheckSelectedModUpdateCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                (UpdateSelectedModCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                // v0.7.55.0: a fetched description is only valid for the MOD it was fetched for.
                SelectedModDescription = null;
                SelectedModDescriptionSourceInput = string.Empty;
                (FetchSelectedModDescriptionCommand as AsyncCommand)?.RaiseCanExecuteChanged();
                (SetSelectedModDescriptionSourceCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    // v0.7.40.0: MOD Library's new per-selection details panel (item 40) gates its whole content
    // on a selection existing, showing a placeholder otherwise.
    public bool HasSelectedMod => SelectedMod is not null;

    private string _selectedModUpdateText = string.Empty;

    private string? _selectedModUpdateWorkshopId;

    // v0.7.41.0: MOD update detection (item 52) result text for the currently selected MOD.
    public string SelectedModUpdateText { get => _selectedModUpdateText; private set => SetField(ref _selectedModUpdateText, value); }

    private ModDescriptionResultDto? _selectedModDescription;

    // v0.7.55.0: website-sourced MOD descriptions (item 40's deferred half). Null until the user
    // explicitly clicks Fetch/Refresh for the current selection -- never populated automatically.
    public ModDescriptionResultDto? SelectedModDescription
    {
        get => _selectedModDescription;
        private set
        {
            if (SetField(ref _selectedModDescription, value))
            {
                RaisePropertyChanged(nameof(HasSelectedModDescription));
                RaisePropertyChanged(nameof(HasModDescriptionResult));
                RaisePropertyChanged(nameof(ShowNoModDescriptionMatchMessage));
            }
        }
    }

    public bool HasSelectedModDescription => SelectedModDescription is { Available: true };

    public bool HasModDescriptionResult => SelectedModDescription is not null;

    // v0.7.55.0 polish: distinct from HasModDescriptionResult (true for both a match and a miss) --
    // this is only true after an actual fetch came back with no match, so the "set a Source URL"
    // guidance doesn't show preemptively before Fetch has ever been clicked for this selection.
    public bool ShowNoModDescriptionMatchMessage => HasModDescriptionResult && !HasSelectedModDescription;

    // v0.7.59.0: Safe-Start MOD Diagnostic. Polled independently of Session.IsBusy -- the diagnostic itself
    // runs server-side over many minutes (a start/stop cycle per candidate MOD), so holding Session.IsBusy
    // for its whole duration would disable unrelated UI the entire time. Only the initial Begin/
    // Cancel POST calls go through Session.IsBusy; the poll loop runs on its own DispatcherTimer.
    private SafeStartStatusDto? _modSafeStartStatus;

    public SafeStartStatusDto? ModSafeStartStatus
    {
        get => _modSafeStartStatus;
        private set
        {
            if (!SetField(ref _modSafeStartStatus, value)) return;
            RaisePropertyChanged(nameof(HasModSafeStartStatus));
            // v1.0.0.1: the Dashboard's stuck-start panel shows the test while it runs and its result afterwards.
            SafeStartStatusChanged?.Invoke();
            (DismissModSafeStartCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool HasModSafeStartStatus => ModSafeStartStatus is not null;

    private DispatcherTimer? _modSafeStartPollTimer;

    private string _selectedModDescriptionSourceInput = string.Empty;

    public string SelectedModDescriptionSourceInput { get => _selectedModDescriptionSourceInput; set => SetField(ref _selectedModDescriptionSourceInput, value ?? string.Empty); }

    public WorkshopItemDto? SelectedWorkshopItem { get => _selectedWorkshopItem; set { if (SetField(ref _selectedWorkshopItem, value)) (ImportSelectedWorkshopModCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }

    public string WorkshopScanState { get => _workshopScanState; private set => SetField(ref _workshopScanState, value); }

    public ICommand RefreshModsCommand { get; }

    public ICommand VerifyModsCommand { get; }

    public ICommand EnableSelectedModCommand { get; }

    public ICommand DisableSelectedModCommand { get; }

    public ICommand DeleteSelectedModCommand { get; }

    public ICommand RollbackSelectedModCommand { get; }

    public ICommand RepairSelectedModCommand { get; }

    public ICommand EnableAllModsCommand { get; }

    public ICommand DisableAllModsCommand { get; }

    public ICommand RepairModsCommand { get; }

    public ICommand ScanWorkshopModsCommand { get; }

    public ICommand ImportSelectedWorkshopModCommand { get; }

    public ICommand CheckSelectedModUpdateCommand { get; }

    public ICommand PreviewUe4ssInstallCommand { get; }

    public ICommand ApplyUe4ssInstallCommand { get; }

    public ICommand RollbackUe4ssInstallCommand { get; }

    public ICommand UpdateSelectedModCommand { get; }

    public ICommand FetchSelectedModDescriptionCommand { get; }

    public ICommand SetSelectedModDescriptionSourceCommand { get; }

    public ICommand BeginModSafeStartCommand { get; }

    public ICommand CancelModSafeStartCommand { get; }

    public ICommand DismissModSafeStartCommand { get; }

    internal async Task InstallLatestUe4ssStableAsync()
    {
        if (!await SelectAndPreviewLatestUe4ssAsync()) return;
        if (!string.IsNullOrWhiteSpace(Ue4ssInstallToken))
            await ApplyUe4ssInstallAsync();
    }

    internal async Task RefreshModsAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { ModSummary = ex.Message; return; }
        Session.IsBusy = true;
        try { ApplyModInventory(await Session.Api.GetModsAsync(profile, Session.BearerToken)); }
        catch (Exception ex) { ModState = "Unavailable"; ModSummary = ex.Message; }
        finally { Session.IsBusy = false; }
        // v0.7.48.0: "Refresh Runtime" (this command's own ribbon label on the UE4SS page) is the
        // natural place to also pull the release catalog -- same page, same "what's available for
        // this runtime" concern. Gated to the UE4SS page specifically since this same command also
        // serves MOD Dashboard/Library's plain "Refresh MODs", which has no reason to hit GitHub.
        if ((Session.Page == NavigationPage.Ue4ss)) await RefreshUe4ssReleaseCatalogAsync();
    }

    private async Task RefreshUe4ssReleaseCatalogAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch { return; }
        Ue4ssReleaseCatalogStatusText = "Checking GitHub for release data…";
        try
        {
            var catalog = await Session.Api.GetUe4ssReleaseCatalogAsync(profile, Session.BearerToken);
            Ue4ssPalworldForkReleases.Clear();
            foreach (var release in catalog.PalworldForkReleases) Ue4ssPalworldForkReleases.Add(release);
            Ue4ssOfficialUpstreamReleases.Clear();
            foreach (var release in catalog.OfficialUpstreamReleases) Ue4ssOfficialUpstreamReleases.Add(release);
            RaisePropertyChanged(nameof(Ue4ssVisibleReleases));
            RaisePropertyChanged(nameof(HasUe4ssReleases));
            Ue4ssReleaseCatalogStatusText = $"Checked {MainWindowViewModel.FormatTime(catalog.ObservedAt)}.";
            var status = await Session.Api.GetUe4ssInstallStatusAsync(profile, Session.BearerToken);
            Ue4ssRollbackAvailable = status.RollbackAvailable;
        }
        catch (Exception ex) { Ue4ssReleaseCatalogStatusText = $"Could not reach GitHub: {ex.Message}"; }
    }

    // v0.7.49.0: UE4SS Install/Rollback -- same Preview-then-Apply shape as backup retention
    // cleanup (Backups.PreviewBackupRetentionAsync/Backups.ApplyBackupRetentionAsync above): Preview resolves the
    // selected release against the live catalog server-side (the client never sends a raw download
    // URL) and returns a short-lived token; Apply only proceeds against that exact token.
    private async Task PreviewUe4ssInstallAsync()
    {
        if (SelectedUe4ssRelease is null) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { Ue4ssInstallState = ex.Message; return; }
        Session.IsBusy = true;
        try
        {
            var preview = await Session.Api.PreviewUe4ssInstallAsync(profile, SelectedUe4ssRelease.Source, SelectedUe4ssRelease.TagName, Session.BearerToken);
            if (preview is null)
            {
                InvalidateUe4ssInstallPreview();
                Ue4ssInstallState = "This release no longer has a clear primary download asset -- open the release page and install it manually instead.";
                return;
            }
            Ue4ssInstallToken = preview.Token;
            Ue4ssInstallState = $"{preview.Summary} Current layout: {preview.CurrentLayout}. Preview expires {preview.ExpiresAt.ToLocalTime():HH:mm:ss}.";
        }
        catch (Exception ex) { InvalidateUe4ssInstallPreview(); Ue4ssInstallState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task ApplyUe4ssInstallAsync()
    {
        if (string.IsNullOrWhiteSpace(Ue4ssInstallToken)) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { Ue4ssInstallState = ex.Message; return; }
        var token = Ue4ssInstallToken;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ApplyUe4ssInstallAsync(profile, token, Session.BearerToken);
            Ue4ssInstallState = result.Message;
            InvalidateUe4ssInstallPreview();
            if (result.Success)
            {
                Ue4ssRollbackAvailable = true;
                await RefreshModsAsync();
            }
        }
        catch (Exception ex) { InvalidateUe4ssInstallPreview(); Ue4ssInstallState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task RollbackUe4ssInstallAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { Ue4ssInstallState = ex.Message; return; }
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.RollbackUe4ssInstallAsync(profile, Session.BearerToken);
            Ue4ssInstallState = result.Message;
            if (result.Success) await RefreshModsAsync();
        }
        catch (Exception ex) { Ue4ssInstallState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // Only clears the token -- callers that need to explain *why* (selection changed vs. a
    // finished install/rollback result) set Ue4ssInstallState themselves, since this is also called
    // after a real result message has already been set and must never clobber it.
    private void InvalidateUe4ssInstallPreview() => Ue4ssInstallToken = string.Empty;

    private async Task VerifyModsAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { ModSummary = ex.Message; return; }
        Session.IsBusy = true;
        ModState = "Verifying…";
        try
        {
            var result = await Session.Api.VerifyModsAsync(profile, Session.BearerToken);
            ModItems.Clear(); foreach (var mod in result.Mods) ModItems.Add(mod);
            SelectedMod = ModItems.FirstOrDefault();
            RaisePropertyChanged(nameof(ModsNeedingAttention)); RaisePropertyChanged(nameof(HasModsNeedingAttention));
            ModHealth = result.OverallHealth; ModInstalledText = result.Installed.ToString();
            ModConfirmedText = result.RuntimeConfirmed.ToString(); ModUnverifiedText = result.ActiveUnverified.ToString();
            ModDisabledText = result.Disabled.ToString(); ModIssuesText = result.Attention.ToString();
            ModState = "Verified"; ModSummary = result.Summary;
            RaisePropertyChanged(nameof(IsModHealthGood)); RaisePropertyChanged(nameof(IsModHealthDegraded));
        }
        catch (Exception ex) { ModState = "Verification failed"; ModSummary = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    public async Task InstallModZipAsync(Stream archive, string suggestedPackage)
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { ModSummary = ex.Message; return; }
        // v0.7.78.0: the manual "Package name (optional)" override box was removed (direct
        // feedback: "i dont think we need a type box here") -- the package name always comes from
        // the ZIP's own filename now, matching how PAK vs. UE4SS type detection already works from
        // the archive's own contents (v0.7.39.0) rather than a manual field.
        var package = Path.GetFileNameWithoutExtension(suggestedPackage);
        Session.IsBusy = true; ModState = "Installing…";
        // v0.7.39.0: the headless service now detects PAK vs. UE4SS from the archive's own
        // contents (HeadlessModManagementService.InstallZipAsync), no longer trusting this value
        // for the actual install -- it stays a plain "PAK" hint purely for CaptureSnapshot's
        // pre-existing-package-of-this-type lookup, unchanged from today's prior default.
        try { var result = await Session.Api.InstallModZipAsync(profile, "PAK", package, archive, Session.BearerToken); ModSummary = result.Message; ApplyModInventory(await Session.Api.GetModsAsync(profile, Session.BearerToken)); }
        catch (Exception ex) { ModState = "Install failed"; ModSummary = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // ---- v0.7.93.0: Nexus Mods catalog -----------------------------------------------------------
    // Browse trending / latest added / latest updated Palworld mods, look one up by id or page URL,
    // list its files, and install one. The user's own API key stays on this machine (memory only
    // unless they explicitly press Save, then DPAPI-encrypted via CredentialStore) and is never sent
    // to the MystTiq management server. Direct download links are Premium-only on Nexus, so free
    // accounts get Open on Nexus plus a paste-an-nxm://-link path. Every download still goes through
    // the existing validated ZIP install (InstallModZipAsync), never straight into the server folder.
    private const string NexusKeyCredentialId = "nexusmods-api-key";

    private const long NexusMaxDownloadBytes = 750L * 1024 * 1024;

    private string _nexusApiKey = string.Empty;

    private string _nexusApiKeyInput = string.Empty;

    private string _nexusAccountText = "Not connected.";

    private bool _nexusIsPremium;

    private string _nexusStatusText = string.Empty;

    private string _nexusRateText = string.Empty;

    private string _nexusNxmLinkText = string.Empty;

    private string _nexusLookupText = string.Empty;

    private NexusModDto? _selectedNexusMod;

    private NexusFileDto? _selectedNexusFile;

    private AsyncCommand[] _nexusCommands = [];

    private RelayCommand<string>? _nexusListCommand;

    public ObservableCollection<NexusModDto> NexusMods { get; } = [];

    public ObservableCollection<NexusFileDto> NexusFiles { get; } = [];

    public string NexusApiKeyInput { get => _nexusApiKeyInput; set => SetField(ref _nexusApiKeyInput, value); }

    public string NexusAccountText { get => _nexusAccountText; private set => SetField(ref _nexusAccountText, value); }

    public bool NexusIsPremium { get => _nexusIsPremium; private set => SetField(ref _nexusIsPremium, value); }

    public string NexusStatusText { get => _nexusStatusText; private set => SetField(ref _nexusStatusText, value); }

    public string NexusRateText { get => _nexusRateText; private set => SetField(ref _nexusRateText, value); }

    public string NexusNxmLinkText { get => _nexusNxmLinkText; set => SetField(ref _nexusNxmLinkText, value); }

    public string NexusLookupText { get => _nexusLookupText; set => SetField(ref _nexusLookupText, value); }

    public NexusModDto? SelectedNexusMod
    {
        get => _selectedNexusMod;
        set { if (SetField(ref _selectedNexusMod, value)) { NexusFiles.Clear(); SelectedNexusFile = null; } }
    }

    public NexusFileDto? SelectedNexusFile
    {
        get => _selectedNexusFile;
        set { if (SetField(ref _selectedNexusFile, value)) RaiseNexusCommandStates(); }
    }

    public ICommand NexusConnectCommand { get; private set; } = null!;

    public ICommand NexusSaveKeyCommand { get; private set; } = null!;

    public ICommand NexusForgetKeyCommand { get; private set; } = null!;

    public ICommand NexusLoadListCommand { get; private set; } = null!;

    public ICommand NexusLookupCommand { get; private set; } = null!;

    public ICommand NexusShowFilesCommand { get; private set; } = null!;

    public ICommand NexusInstallFileCommand { get; private set; } = null!;

    public ICommand NexusInstallFromNxmCommand { get; private set; } = null!;

    public ICommand NexusOpenPageCommand { get; private set; } = null!;

    private void InitializeNexusMods()
    {
        _nexusApiKey = _credentialStore.TryLoad(NexusKeyCredentialId) ?? string.Empty;
        if (_nexusApiKey.Length > 0) NexusAccountText = "A saved key was found. Press Connect to verify it.";

        var connect = new AsyncCommand(NexusConnectAsync, () => !Session.IsBusy);
        var list = new RelayCommand<string>(kind => _ = NexusLoadListAsync(kind), _ => !Session.IsBusy);
        _nexusListCommand = list;
        var lookup = new AsyncCommand(NexusLookupAsync, () => !Session.IsBusy);
        var files = new AsyncCommand(NexusShowFilesAsync, () => !Session.IsBusy && SelectedNexusMod is not null);
        var install = new AsyncCommand(NexusInstallSelectedFileAsync, () => !Session.IsBusy && SelectedNexusMod is not null && SelectedNexusFile is not null);
        var nxm = new AsyncCommand(NexusInstallFromNxmAsync, () => !Session.IsBusy && !string.IsNullOrWhiteSpace(NexusNxmLinkText));
        NexusConnectCommand = connect;
        NexusLoadListCommand = list;
        NexusLookupCommand = lookup;
        NexusShowFilesCommand = files;
        NexusInstallFileCommand = install;
        NexusInstallFromNxmCommand = nxm;
        NexusSaveKeyCommand = new RelayCommand(NexusSaveKey);
        NexusForgetKeyCommand = new RelayCommand(NexusForgetKey);
        NexusOpenPageCommand = new RelayCommand(NexusOpenPage);
        _nexusCommands = [connect, files, install, nxm, lookup];
    }

    internal void RaiseNexusCommandStates()
    {
        foreach (var command in _nexusCommands) command.RaiseCanExecuteChanged();
        _nexusListCommand?.RaiseCanExecuteChanged();
    }

    // Uses the key typed into the box if there is one, otherwise the one already held in memory
    // (loaded from the encrypted store, or verified earlier this session).
    private string CurrentNexusKey() => string.IsNullOrWhiteSpace(NexusApiKeyInput) ? _nexusApiKey : NexusApiKeyInput.Trim();

    private void ReportNexusRate<T>(NexusCallResult<T> result)
    {
        if (result.HourlyRemaining is null && result.DailyRemaining is null) return;
        NexusRateText = $"Nexus requests left: {result.HourlyRemaining?.ToString() ?? "?"} this hour, {result.DailyRemaining?.ToString() ?? "?"} today.";
    }

    private async Task NexusConnectAsync()
    {
        var key = CurrentNexusKey();
        if (string.IsNullOrWhiteSpace(key)) { NexusStatusText = "Paste your Nexus Mods API key first (nexusmods.com > your account > API keys)."; return; }
        Session.IsBusy = true; NexusStatusText = "Checking the key with Nexus Mods…";
        try
        {
            var result = await _nexus.ValidateAsync(key);
            ReportNexusRate(result);
            if (!result.Ok || result.Value is null) { NexusStatusText = result.Error ?? "Nexus did not confirm the key."; return; }
            _nexusApiKey = key;
            NexusApiKeyInput = string.Empty;
            NexusIsPremium = result.Value.IsPremium;
            NexusAccountText = $"Connected as {result.Value.Name}{(result.Value.IsPremium ? " (Premium: direct downloads available)" : " (free account: use Open on Nexus or an nxm:// link to download)")}.";
            NexusStatusText = "Connected. Load a list below, or look up a mod by id or page URL.";
        }
        finally { Session.IsBusy = false; }
    }

    private void NexusSaveKey()
    {
        var key = CurrentNexusKey();
        if (string.IsNullOrWhiteSpace(key)) { NexusStatusText = "There is no key to save yet."; return; }
        if (!OperatingSystem.IsWindows()) { NexusStatusText = "Saving the key is only supported on Windows; it stays in memory for this session."; _nexusApiKey = key; return; }
        _credentialStore.Save(NexusKeyCredentialId, key);
        _nexusApiKey = key;
        NexusApiKeyInput = string.Empty;
        NexusStatusText = "Key saved on this PC, encrypted for your Windows account only. It is never sent to the MystTiq server.";
    }

    private void NexusForgetKey()
    {
        _credentialStore.Delete(NexusKeyCredentialId);
        _nexusApiKey = string.Empty;
        NexusApiKeyInput = string.Empty;
        NexusIsPremium = false;
        NexusAccountText = "Not connected.";
        NexusMods.Clear(); NexusFiles.Clear(); SelectedNexusMod = null;
        NexusStatusText = "The saved key was removed and this session's copy cleared.";
    }

    private async Task NexusLoadListAsync(string? kind)
    {
        var key = CurrentNexusKey();
        if (string.IsNullOrWhiteSpace(key)) { NexusStatusText = "Connect with your API key first."; return; }
        Session.IsBusy = true; NexusStatusText = "Loading from Nexus Mods…";
        try
        {
            var result = await _nexus.GetListAsync(key, kind ?? "trending");
            ReportNexusRate(result);
            if (!result.Ok || result.Value is null) { NexusStatusText = result.Error ?? "Nexus returned nothing."; return; }
            NexusMods.Clear();
            foreach (var mod in result.Value) NexusMods.Add(mod);
            SelectedNexusMod = null;
            NexusStatusText = $"{result.Value.Count} mod(s). Nexus's public API only lists 10 per category and has no catalog search; use the lookup box for any other mod.";
        }
        finally { Session.IsBusy = false; }
    }

    private async Task NexusLookupAsync()
    {
        if (!NexusModsLinks.TryParseModReference(NexusLookupText, out var modId))
        { NexusStatusText = "Enter a mod id or a nexusmods.com/palworld/mods/... page URL."; return; }
        var key = CurrentNexusKey();
        if (string.IsNullOrWhiteSpace(key)) { NexusStatusText = "Connect with your API key first."; return; }
        Session.IsBusy = true; NexusStatusText = $"Looking up mod {modId}…";
        try
        {
            var result = await _nexus.GetModAsync(key, modId);
            ReportNexusRate(result);
            if (!result.Ok || result.Value is null) { NexusStatusText = result.Error ?? "Nexus returned nothing."; return; }
            NexusMods.Clear();
            NexusMods.Add(result.Value);
            SelectedNexusMod = result.Value;
            NexusStatusText = $"Found {result.Value.DisplayName}. Press Show Files to see what can be installed.";
        }
        finally { Session.IsBusy = false; }
    }

    private async Task NexusShowFilesAsync()
    {
        if (SelectedNexusMod is not { } mod) return;
        var key = CurrentNexusKey();
        if (string.IsNullOrWhiteSpace(key)) { NexusStatusText = "Connect with your API key first."; return; }
        Session.IsBusy = true; NexusStatusText = $"Loading files for {mod.DisplayName}…";
        try
        {
            var result = await _nexus.GetFilesAsync(key, mod.ModId);
            ReportNexusRate(result);
            if (!result.Ok || result.Value is null) { NexusStatusText = result.Error ?? "Nexus returned nothing."; return; }
            NexusFiles.Clear();
            foreach (var file in result.Value.Where(f => !string.Equals(f.CategoryName, "OLD_VERSION", StringComparison.OrdinalIgnoreCase)))
                NexusFiles.Add(file);
            SelectedNexusFile = NexusFiles.FirstOrDefault(f => f.IsPrimary) ?? NexusFiles.FirstOrDefault();
            NexusStatusText = NexusFiles.Count == 0 ? "That mod has no current files." : $"{NexusFiles.Count} file(s). Only .zip archives can be installed by MystTiq.";
        }
        finally { Session.IsBusy = false; }
    }

    private Task NexusInstallSelectedFileAsync() =>
        SelectedNexusMod is { } mod && SelectedNexusFile is { } file
            ? NexusDownloadAndInstallAsync(mod.ModId, file.FileId, file.FileName ?? $"nexus-{mod.ModId}-{file.FileId}.zip", null, null)
            : Task.CompletedTask;

    private Task NexusInstallFromNxmAsync()
    {
        if (!NexusModsLinks.TryParseNxm(NexusNxmLinkText, out var link, out var error) || link is null)
        { NexusStatusText = error; return Task.CompletedTask; }
        return NexusDownloadAndInstallAsync(link.ModId, link.FileId, $"nexus-{link.ModId}-{link.FileId}.zip", link.Key, link.Expires);
    }

    private async Task NexusDownloadAndInstallAsync(int modId, int fileId, string fileName, string? nxmKey, long? nxmExpires)
    {
        var key = CurrentNexusKey();
        if (string.IsNullOrWhiteSpace(key)) { NexusStatusText = "Connect with your API key first."; return; }
        var temp = Path.Combine(Path.GetTempPath(), "MystTiq-nexus-" + Guid.NewGuid().ToString("N") + ".download");
        Session.IsBusy = true; NexusStatusText = "Asking Nexus for a download link…";
        try
        {
            var link = await _nexus.GetDownloadLinkAsync(key, modId, fileId, nxmKey, nxmExpires);
            ReportNexusRate(link);
            if (!link.Ok || link.Value is null) { NexusStatusText = link.Error ?? "Nexus gave no download link."; return; }

            var progress = new Progress<string>(text => NexusStatusText = text);
            var failure = await _nexus.DownloadToFileAsync(link.Value, temp, NexusMaxDownloadBytes, progress);
            if (failure is not null) { NexusStatusText = failure; return; }

            var header = new byte[8];
            await using (var probe = File.OpenRead(temp)) { _ = await probe.ReadAsync(header); }
            var kind = NexusModsLinks.DescribeArchiveKind(header);
            if (kind != "zip")
            {
                NexusStatusText = kind is "7z" or "rar"
                    ? $"That file is a .{kind} archive. MystTiq installs .zip only: extract it, re-zip it, then use Install MOD ZIP."
                    : "That download is not a ZIP archive, so MystTiq will not install it.";
                return;
            }

            // v1.0.5.0 (roadmap M-1): the same layout check as the MOD browser, before anything is sent to the server.
            if (ModSourceHttp.PlanZip(temp) is { Installable: false } plan) { NexusStatusText = plan.Summary; return; }

            NexusStatusText = "Downloaded. Installing through the validated ZIP install…";
            Session.IsBusy = false; // InstallModZipAsync manages the busy flag itself
            await using var stream = File.OpenRead(temp);
            await InstallModZipAsync(stream, fileName);
            NexusStatusText = string.IsNullOrWhiteSpace(ModSummary) ? "Install finished." : ModSummary;
        }
        finally
        {
            Session.IsBusy = false;
            try { if (File.Exists(temp)) File.Delete(temp); } catch { /* best-effort temp cleanup */ }
        }
    }

    private void NexusOpenPage()
    {
        if (SelectedNexusMod is not { } mod) { NexusStatusText = "Select a mod first."; return; }
        try { Process.Start(new ProcessStartInfo(NexusModsLinks.ModPageUrl(mod.ModId)) { UseShellExecute = true }); }
        catch (Exception ex) { NexusStatusText = $"Could not open the browser: {ex.Message}"; }
    }

    private async Task DeleteSelectedModAsync()
    {
        if (SelectedMod is null) return; var selected = SelectedMod;
        await RunModMutationAsync((profile) => Session.Api.DeleteModAsync(profile, selected.Type, selected.Package, Session.BearerToken), "Delete failed");
    }

    private async Task RollbackSelectedModAsync()
    {
        if (SelectedMod is null) return; var selected = SelectedMod;
        await RunModMutationAsync((profile) => Session.Api.RollbackModAsync(profile, selected.Type, selected.Package, Session.BearerToken), "Rollback failed");
    }

    // v0.7.77.0: per-MOD "Repair / Re-install" -- direct request. Distinct from Update (only
    // offers when Steam's local copy is newer) and the existing global Repair button (only
    // neutralizes legacy enabled.txt overrides, never touches a MOD's own files). Always available
    // to try for any selected MOD, not just ones already flagged Misconfigured -- the backend
    // itself reports honestly if no local Workshop source is known rather than this trying to
    // pre-guess availability client-side.
    private async Task RepairSelectedModAsync()
    {
        if (SelectedMod is null) return; var selected = SelectedMod;
        await RunModMutationAsync((profile) => Session.Api.RepairModAsync(profile, selected.Type, selected.Package, Session.BearerToken), "Repair failed");
    }

    private async Task SetAllModsEnabledAsync(bool enabled) =>
        await RunModMutationAsync(profile => Session.Api.SetAllModsEnabledAsync(profile, enabled, Session.BearerToken), "Bulk state change failed");

    private async Task RepairModsAsync() =>
        await RunModMutationAsync(profile => Session.Api.RepairModsAsync(profile, Session.BearerToken), "Repair failed");

    internal async Task ScanWorkshopModsAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { WorkshopScanState = ex.Message; return; }
        Session.IsBusy = true;
        WorkshopScanState = "Scanning local Steam Workshop content…";
        try
        {
            var result = await Session.Api.ScanWorkshopModsAsync(profile, Session.BearerToken);
            WorkshopItems.Clear(); foreach (var item in result.Items) WorkshopItems.Add(item);
            SelectedWorkshopItem = WorkshopItems.FirstOrDefault();
            WorkshopScanState = result.Detail;
        }
        catch (Exception ex) { WorkshopScanState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task ImportSelectedWorkshopModAsync()
    {
        if (SelectedWorkshopItem is null) return;
        var selected = SelectedWorkshopItem;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { WorkshopScanState = ex.Message; return; }
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ImportWorkshopModAsync(profile, selected.WorkshopId, Session.BearerToken);
            WorkshopScanState = result.Message;
            ApplyModInventory(await Session.Api.GetModsAsync(profile, Session.BearerToken));
            var rescan = await Session.Api.ScanWorkshopModsAsync(profile, Session.BearerToken);
            WorkshopItems.Clear(); foreach (var item in rescan.Items) WorkshopItems.Add(item);
            SelectedWorkshopItem = WorkshopItems.FirstOrDefault(x => x.WorkshopId == selected.WorkshopId) ?? WorkshopItems.FirstOrDefault();
        }
        catch (Exception ex) { WorkshopScanState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // v0.7.41.0: MOD update detection (item 52). Check calls the headless service's real,
    // locally-verifiable comparison (installed files vs. Steam's local Workshop content cache for
    // a matching item, no network calls, no fabricated "latest version" claim) and remembers the
    // matched WorkshopId so Update can reuse the existing Workshop-import route unchanged.
    private async Task CheckSelectedModUpdateAsync()
    {
        if (SelectedMod is not { } mod) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { SelectedModUpdateText = ex.Message; return; }
        Session.IsBusy = true;
        SelectedModUpdateText = "Checking for an update…";
        try
        {
            var result = await Session.Api.CheckModUpdateAsync(profile, mod.Type, mod.Package, Session.BearerToken);
            SelectedModUpdateText = result.Detail;
            _selectedModUpdateWorkshopId = result.UpdateAvailable ? result.WorkshopId : null;
            (UpdateSelectedModCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
        catch (Exception ex) { SelectedModUpdateText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task UpdateSelectedModAsync()
    {
        if (_selectedModUpdateWorkshopId is not { } workshopId || SelectedMod is not { } mod) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { SelectedModUpdateText = ex.Message; return; }
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ImportWorkshopModAsync(profile, workshopId, Session.BearerToken);
            var inventory = await Session.Api.GetModsAsync(profile, Session.BearerToken);
            ApplyModInventory(inventory);
            // Reselecting runs through SelectedMod's setter, which clears SelectedModUpdateText as
            // stale for the new selection -- set the result message after, not before, reselecting.
            SelectedMod = ModItems.FirstOrDefault(x => string.Equals(x.Package, mod.Package, StringComparison.OrdinalIgnoreCase)) ?? SelectedMod;
            SelectedModUpdateText = result.Message;
        }
        catch (Exception ex) { SelectedModUpdateText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // v0.7.55.0: website-sourced MOD descriptions (item 40's deferred half). Only ever runs from an
    // explicit user click (Fetch/Refresh) -- never automatically on selection change, so switching
    // through the MOD list never triggers a network call on its own. A description already showing
    // for this selection means the click is a Refresh, so it forces a re-fetch past the cache.
    private async Task FetchSelectedModDescriptionAsync()
    {
        if (SelectedMod is not { } mod) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { SelectedModDescription = new ModDescriptionResultDto { Available = false, Detail = ex.Message }; return; }
        Session.IsBusy = true;
        try
        {
            var forceRefresh = SelectedModDescription is not null;
            SelectedModDescription = await Session.Api.GetModDescriptionAsync(profile, mod.Type, mod.Package, forceRefresh, Session.BearerToken);
        }
        catch (Exception ex) { SelectedModDescription = new ModDescriptionResultDto { Available = false, Detail = ex.Message }; }
        finally { Session.IsBusy = false; }
    }

    private async Task SetSelectedModDescriptionSourceAsync()
    {
        if (SelectedMod is not { } mod) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { SelectedModDescription = new ModDescriptionResultDto { Available = false, Detail = ex.Message }; return; }
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.SetModDescriptionSourceAsync(profile, mod.Type, mod.Package, SelectedModDescriptionSourceInput, Session.BearerToken);
            SelectedModDescription = result.Success
                ? await Session.Api.GetModDescriptionAsync(profile, mod.Type, mod.Package, true, Session.BearerToken)
                : new ModDescriptionResultDto { Available = false, Detail = result.Message };
        }
        catch (Exception ex) { SelectedModDescription = new ModDescriptionResultDto { Available = false, Detail = ex.Message }; }
        finally { Session.IsBusy = false; }
    }

    // v0.7.59.0: Safe-Start MOD Diagnostic. Session.IsBusy only wraps the initial Begin POST -- the
    // diagnostic itself runs server-side over many minutes, so a dedicated poll loop (not Session.IsBusy)
    // tracks it independently, matching the "don't hold Session.IsBusy for a genuinely long background
    // operation" discipline this ViewModel already applies to UE4SS install preview/apply.
    internal Task BeginModSafeStartAsync() => BeginModSafeStartAsync(testLoad: false);

    internal async Task BeginModSafeStartAsync(bool testLoad)
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { ModSafeStartStatus = new SafeStartStatusDto { Completed = true, FinalMessage = ex.Message }; return; }
        Session.IsBusy = true;
        try
        {
            var result = testLoad
                ? await Session.Api.BeginModTestLoadAsync(profile, Session.BearerToken)
                : await Session.Api.BeginModSafeStartAsync(profile, Session.BearerToken);
            if (!result.Success)
            {
                ModSafeStartStatus = new SafeStartStatusDto { Completed = true, FinalMessage = result.Message };
                return;
            }
        }
        catch (Exception ex)
        {
            ModSafeStartStatus = new SafeStartStatusDto { Completed = true, FinalMessage = ex.Message };
            return;
        }
        finally { Session.IsBusy = false; }
        StartModSafeStartPolling();
    }

    private void StartModSafeStartPolling()
    {
        _modSafeStartPollTimer?.Stop();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += async (_, _) => await PollModSafeStartStatusAsync();
        _modSafeStartPollTimer = timer;
        timer.Start();
        _ = PollModSafeStartStatusAsync();
    }

    private async Task PollModSafeStartStatusAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch { return; }
        try
        {
            var status = await Session.Api.GetModSafeStartStatusAsync(profile, Session.BearerToken);
            ModSafeStartStatus = status;
            (BeginModSafeStartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (CancelModSafeStartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            if (status is null || status.Completed)
            {
                _modSafeStartPollTimer?.Stop();
                _modSafeStartPollTimer = null;
                if (status is { Completed: true })
                {
                    // The diagnostic may have disabled MODs along the way -- refresh the inventory
                    // so the Installed MODs list reflects the real, current enabled/disabled state.
                    var inventory = await Session.Api.GetModsAsync(profile, Session.BearerToken);
                    ApplyModInventory(inventory);
                }
            }
        }
        catch { /* Transient poll failure -- the timer tries again on its own next tick. */ }
    }

    private async Task CancelModSafeStartAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch { return; }
        try { await Session.Api.CancelModSafeStartAsync(profile, Session.BearerToken); }
        catch { /* Best effort -- the next poll tick will reflect whatever the server actually did. */ }
        await PollModSafeStartStatusAsync();
    }

    private async Task RunModMutationAsync(Func<ConnectionProfile, Task<ModMutationResultDto>> operation, string failureState)
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { ModSummary = ex.Message; return; }
        Session.IsBusy = true;
        try { var result = await operation(profile); ModSummary = result.Message; ApplyModInventory(await Session.Api.GetModsAsync(profile, Session.BearerToken)); }
        catch (Exception ex) { ModState = failureState; ModSummary = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    internal void ApplyModInventory(ModInventoryDto inventory)
    {
        ModItems.Clear(); foreach (var mod in inventory.Mods) ModItems.Add(mod);
        SelectedMod = ModItems.FirstOrDefault();
        RaisePropertyChanged(nameof(ModsNeedingAttention)); RaisePropertyChanged(nameof(HasModsNeedingAttention));
        ModState = "Ready"; ModSummary = inventory.Summary; ModHealth = inventory.OverallHealth;
        ModInstalledText = inventory.Installed.ToString(); ModConfirmedText = inventory.RuntimeConfirmed.ToString();
        ModUnverifiedText = inventory.ActiveUnverified.ToString(); ModDisabledText = inventory.Disabled.ToString();
        ModIssuesText = inventory.ConfirmedIssues.ToString();
        RaisePropertyChanged(nameof(IsModHealthGood)); RaisePropertyChanged(nameof(IsModHealthDegraded));
        Ue4ssHealth = inventory.Ue4ss.HealthState; Ue4ssDetection = inventory.Ue4ss.DetectionMethod;
        Ue4ssActiveRoot = inventory.Ue4ss.ActiveModsRoot; Ue4ssRuntimeRoot = inventory.Ue4ss.RuntimeRootText;
        Ue4ssWarning = inventory.Ue4ss.WarningMessage;
        Ue4ssInstalledVersion = inventory.Ue4ss.InstalledVersion;
    }

    // The UE4SS page with the newest release selected and its install previewed; the user applies it there.
    internal async Task<bool> SelectAndPreviewLatestUe4ssAsync()
    {
        if (Ue4ssPalworldForkReleases.Count == 0)
            await RefreshUe4ssReleaseCatalogAsync();

        var latest = Ue4ssPalworldForkReleases.Where(r => !r.Prerelease).OrderByDescending(r => r.PublishedAt).FirstOrDefault()
            ?? Ue4ssPalworldForkReleases.OrderByDescending(r => r.PublishedAt).FirstOrDefault();
        if (latest is null)
        {
            Ue4ssInstallState = "No UE4SS release catalog data was available to auto-install from.";
            return false;
        }

        SelectedUe4ssFork = "Palworld Fork";
        SelectedUe4ssRelease = latest;
        await PreviewUe4ssInstallAsync();
        return true;
    }

    private async Task SetSelectedModEnabledAsync(bool enabled)
    {
        if (SelectedMod is null) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { ModSummary = ex.Message; return; }
        var selected = SelectedMod;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.SetModEnabledAsync(profile, selected.Type, selected.Package, enabled, Session.BearerToken);
            ModSummary = result.Message;
            ApplyModInventory(await Session.Api.GetModsAsync(profile, Session.BearerToken));
        }
        catch (Exception ex) { ModState = "State change failed"; ModSummary = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // v1.0.5.0 (roadmap M-1; owner decision 2026-10-06): the MOD browser on the MOD Library page. Search a source, pick a MOD
    // and one of its files; Check and Install (MainWindow's Click handler) fetches it from that source's own hosts, shows what
    // the archive holds and whether MystTiq can install it, asks, then installs through the validated ZIP install.
    private const string CurseForgeKeyCredentialId = "curseforge-api-key";
    private static readonly HttpClient ModBrowserHttp = ModSourceHttp.Create();
    private LocalModBrowserPreferencesStore _modBrowserPrefs = new();
    private NxmLinkHandoff _nxmHandoff = new();
    private DispatcherTimer? _nxmTimer;
    private string _curseForgeKey = string.Empty;
    private IModSource[] _modSources = [];
    private CancellationTokenSource? _modFilesLoad;

    public ObservableCollection<ModSourceOption> ModSourceOptions { get; } = [];
    public ObservableCollection<ModListing> ModBrowserResults { get; } = [];
    public ObservableCollection<ModListingFile> ModBrowserFiles { get; } = [];
    public ObservableCollection<string> ModBrowserFolders { get; } = [];
    public ObservableCollection<string> ModBrowserRepositories { get; } = [];

    private ModSourceOption? _selectedModSource;
    public ModSourceOption? SelectedModSource
    {
        get => _selectedModSource;
        set
        {
            if (!SetField(ref _selectedModSource, value)) return;
            ModBrowserResults.Clear(); ModBrowserFiles.Clear(); ModBrowserStatus = string.Empty;
            foreach (var name in new[] { nameof(IsFolderModSource), nameof(IsCurseForgeModSource), nameof(IsGitHubModSource), nameof(IsNexusModSource) }) RaisePropertyChanged(name);
        }
    }
    public bool IsFolderModSource => SelectedModSource?.Id == "folders";
    public bool IsCurseForgeModSource => SelectedModSource?.Id == "curseforge";
    public bool IsGitHubModSource => SelectedModSource?.Id == "github";
    public bool IsNexusModSource => SelectedModSource?.Id == "nexus";

    private ModListing? _selectedModListing;
    public ModListing? SelectedModListing
    {
        get => _selectedModListing;
        set { if (SetField(ref _selectedModListing, value)) _ = LoadModBrowserFilesAsync(value); }
    }
    private ModListingFile? _selectedModFile;
    public ModListingFile? SelectedModFile { get => _selectedModFile; set => SetField(ref _selectedModFile, value); }

    private string _modBrowserQuery = string.Empty;
    public string ModBrowserQuery { get => _modBrowserQuery; set => SetField(ref _modBrowserQuery, value); }
    private string _modBrowserStatus = string.Empty;
    public string ModBrowserStatus { get => _modBrowserStatus; private set => SetField(ref _modBrowserStatus, value); }
    private string? _selectedModBrowserFolder;
    public string? SelectedModBrowserFolder { get => _selectedModBrowserFolder; set => SetField(ref _selectedModBrowserFolder, value); }
    private string? _selectedModBrowserRepository;
    public string? SelectedModBrowserRepository { get => _selectedModBrowserRepository; set => SetField(ref _selectedModBrowserRepository, value); }
    private string _newModBrowserFolder = string.Empty;
    public string NewModBrowserFolder { get => _newModBrowserFolder; set => SetField(ref _newModBrowserFolder, value); }
    private string _newModBrowserRepository = string.Empty;
    public string NewModBrowserRepository { get => _newModBrowserRepository; set => SetField(ref _newModBrowserRepository, value); }
    private string _curseForgeKeyInput = string.Empty;
    public string CurseForgeKeyInput { get => _curseForgeKeyInput; set => SetField(ref _curseForgeKeyInput, value); }
    private string _nxmHandlerText = string.Empty;
    public string NxmHandlerText { get => _nxmHandlerText; private set => SetField(ref _nxmHandlerText, value); }

    public ICommand SearchModBrowserCommand { get; private set; } = null!;
    public ICommand AddModBrowserFolderCommand { get; private set; } = null!;
    public ICommand RemoveModBrowserFolderCommand { get; private set; } = null!;
    public ICommand AddModBrowserRepositoryCommand { get; private set; } = null!;
    public ICommand RemoveModBrowserRepositoryCommand { get; private set; } = null!;
    public ICommand SaveCurseForgeKeyCommand { get; private set; } = null!;
    public ICommand ForgetCurseForgeKeyCommand { get; private set; } = null!;
    public ICommand OpenModBrowserPageCommand { get; private set; } = null!;
    public ICommand StopUsingMystTiqForNxmLinksCommand { get; private set; } = null!;

    private void InitializeModBrowser()
    {
        var prefs = _modBrowserPrefs.Load();
        foreach (var f in prefs.Folders) ModBrowserFolders.Add(f);
        foreach (var r in prefs.GitHubRepositories) ModBrowserRepositories.Add(r);
        _curseForgeKey = _credentialStore.TryLoad(CurseForgeKeyCredentialId) ?? string.Empty;
        _modSources =
        [
            new FolderModSource(() => ModBrowserFolders.ToList()),
            new ThunderstoreModSource(ModBrowserHttp),
            new CurseForgeModSource(ModBrowserHttp, CurrentCurseForgeKey),
            new GitHubReleasesModSource(ModBrowserHttp, () => ModBrowserRepositories.ToList()),
            new NexusModSource(_nexus, CurrentNexusKey),
        ];
        foreach (var s in _modSources) ModSourceOptions.Add(new ModSourceOption { Id = s.Id, Name = s.Name });
        _selectedModSource = ModSourceOptions[0];

        SearchModBrowserCommand = new AsyncCommand(SearchModBrowserAsync, () => !Session.IsBusy && SelectedModSource is not null);
        AddModBrowserFolderCommand = new RelayCommand(AddModBrowserFolder);
        RemoveModBrowserFolderCommand = new RelayCommand(() => { if (SelectedModBrowserFolder is { } f) { ModBrowserFolders.Remove(f); SaveModBrowserPreferences(); } });
        AddModBrowserRepositoryCommand = new RelayCommand(AddModBrowserRepository);
        RemoveModBrowserRepositoryCommand = new RelayCommand(() => { if (SelectedModBrowserRepository is { } r) { ModBrowserRepositories.Remove(r); SaveModBrowserPreferences(); } });
        SaveCurseForgeKeyCommand = new RelayCommand(SaveCurseForgeKey);
        ForgetCurseForgeKeyCommand = new RelayCommand(ForgetCurseForgeKey);
        OpenModBrowserPageCommand = new RelayCommand(OpenModBrowserPage);
        StopUsingMystTiqForNxmLinksCommand = new RelayCommand(StopUsingMystTiqForNxmLinks);
        RefreshNxmHandlerText();

        // Links handed over by a second MystTiq started from the browser (see NxmLinkHandoff).
        if (Avalonia.Application.Current is not null)
        {
            _nxmTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _nxmTimer.Tick += (_, _) => ReceiveNxmLinks();
            _nxmTimer.Start();
        }
    }

    private string CurrentCurseForgeKey() => string.IsNullOrWhiteSpace(CurseForgeKeyInput) ? _curseForgeKey : CurseForgeKeyInput.Trim();
    private IModSource? CurrentModSource => _modSources.FirstOrDefault(s => s.Id == SelectedModSource?.Id);

    public void ReceiveNxmLinks()
    {
        IReadOnlyList<string> links;
        try { links = _nxmHandoff.TakeAll(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
        if (links.Count == 0) return;
        NexusNxmLinkText = links[^1];
        NexusModsLinks.TryParseNxm(links[^1], out var link, out _);
        SelectedModSource = ModSourceOptions.FirstOrDefault(o => o.Id == "nexus");
        ModBrowserStatus = $"A Mod Manager Download link arrived from Nexus (MOD {link?.ModId}, file {link?.FileId}). Press Install from link in the Nexus Mods card to check and install it.";
        Navigate(nameof(NavigationPage.ModLibrary));
    }

    private async Task SearchModBrowserAsync()
    {
        if (CurrentModSource is not { } source) return;
        Session.IsBusy = true; ModBrowserStatus = "Searching…";
        ModBrowserResults.Clear(); ModBrowserFiles.Clear();
        try
        {
            var result = await source.SearchAsync(ModBrowserQuery);
            if (!result.Ok) { ModBrowserStatus = result.Error!; return; }
            foreach (var listing in result.Value!) ModBrowserResults.Add(listing);
            ModBrowserStatus = source is FolderModSource { SkippedOtherArchives: > 0 } folders
                ? $"{ModBrowserResults.Count} found. {folders.SkippedOtherArchives} .7z or .rar archives were skipped: MystTiq installs .zip only."
                : ModBrowserResults.Count == 0 ? "Nothing found." : $"{ModBrowserResults.Count} found.";
        }
        catch (Exception ex) { ModBrowserStatus = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task LoadModBrowserFilesAsync(ModListing? listing)
    {
        _modFilesLoad?.Cancel();
        ModBrowserFiles.Clear(); SelectedModFile = null;
        if (listing is null || CurrentModSource is not { } source) return;
        var cts = _modFilesLoad = new CancellationTokenSource();
        try
        {
            var files = await source.GetFilesAsync(listing, cts.Token);
            if (cts.IsCancellationRequested) return;
            if (!files.Ok) { ModBrowserStatus = files.Error!; return; }
            foreach (var f in files.Value!) ModBrowserFiles.Add(f);
            SelectedModFile = ModBrowserFiles.FirstOrDefault();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ModBrowserStatus = ex.Message; }
    }

    public sealed record PreparedModInstall(string Path, bool IsTemporary, ModArchivePlan Plan, string Package, string SourceName);

    // Fetches the selected file (or uses the one on this PC) and reads what it holds; null with the reason shown otherwise.
    public async Task<PreparedModInstall?> PrepareModBrowserInstallAsync()
    {
        if (CurrentModSource is not { } source || SelectedModListing is not { } listing) { ModBrowserStatus = "Pick a MOD first."; return null; }
        if (SelectedModFile is not { } file) { ModBrowserStatus = "Pick one of its files first."; return null; }
        var package = PackageNameFor(listing.Name, file.Name);
        if (file.LocalPath is { } local)
        {
            var localPlan = ModSourceHttp.PlanZip(local);
            if (localPlan is null) { ModBrowserStatus = "That file is not a ZIP MystTiq can read."; return null; }
            if (!localPlan.Installable) { ModBrowserStatus = localPlan.Summary; return null; }
            return new PreparedModInstall(local, false, localPlan, PackageFor(localPlan, package), source.Name);
        }
        var temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MystTiq-mod-" + Guid.NewGuid().ToString("N") + ".zip");
        Session.IsBusy = true; ModBrowserStatus = "Asking for the download…";
        try
        {
            var where = await source.ResolveDownloadAsync(listing, file);
            if (!where.Ok) { ModBrowserStatus = where.Error!; return null; }
            var failure = await ModSourceHttp.DownloadAsync(ModBrowserHttp, source, where.Value!, temp, new Progress<string>(t => ModBrowserStatus = t));
            if (failure is not null) { ModBrowserStatus = failure; TryDeleteTemp(temp); return null; }
            var plan = ModSourceHttp.PlanZip(temp);
            if (plan is null) { ModBrowserStatus = "The download is not a ZIP (MystTiq installs .zip only)."; TryDeleteTemp(temp); return null; }
            if (!plan.Installable) { ModBrowserStatus = plan.Summary; TryDeleteTemp(temp); return null; }
            ModBrowserStatus = plan.Summary;
            return new PreparedModInstall(temp, true, plan, PackageFor(plan, package), source.Name);
        }
        catch (Exception ex) { ModBrowserStatus = ex.Message; TryDeleteTemp(temp); return null; }
        finally { Session.IsBusy = false; }
    }

    public async Task InstallPreparedModAsync(PreparedModInstall prepared)
    {
        try
        {
            await using (var stream = File.OpenRead(prepared.Path)) await InstallModZipAsync(stream, prepared.Package + ".zip");
            ModBrowserStatus = string.IsNullOrWhiteSpace(ModSummary) ? "Install finished." : ModSummary;
        }
        finally { DiscardPreparedMod(prepared); }
    }

    public void DiscardPreparedMod(PreparedModInstall prepared) { if (prepared.IsTemporary) TryDeleteTemp(prepared.Path); }

    private static void TryDeleteTemp(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

    // A UE4SS MOD keeps the name of its own folder in the archive (its scripts may refer to it, as UE4SS loads it by that
    // name); otherwise the listing's name. Found live on 2026-10-06: a GitHub MOD otherwise took its repository's name.
    private static string PackageFor(ModArchivePlan plan, string fallback) =>
        plan.Ue4ssRoot is { Length: > 0 } root ? PackageNameFor(root.TrimEnd('/').Split('/')[^1], fallback) : fallback;

    // The MOD's own name, made safe as a package name (the service refuses path characters).
    public static string PackageNameFor(string listingName, string fileName)
    {
        var name = string.IsNullOrWhiteSpace(listingName) ? System.IO.Path.GetFileNameWithoutExtension(fileName) : listingName;
        var safe = new string(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_').ToArray()).Trim('_', '.');
        while (safe.Contains("__")) safe = safe.Replace("__", "_");
        return safe.Length == 0 ? "Mod" : safe.Length > 60 ? safe[..60] : safe;
    }

    private void AddModBrowserFolder()
    {
        var folder = NewModBrowserFolder.Trim().Trim('"');
        if (folder.Length == 0 || !Directory.Exists(folder)) { ModBrowserStatus = "That folder does not exist on this PC."; return; }
        if (!ModBrowserFolders.Contains(folder, StringComparer.OrdinalIgnoreCase)) { ModBrowserFolders.Add(folder); SaveModBrowserPreferences(); }
        NewModBrowserFolder = string.Empty;
    }

    private void AddModBrowserRepository()
    {
        if (GitHubReleasesModSource.NormalizeRepository(NewModBrowserRepository) is not { } repo) { ModBrowserStatus = "Enter a GitHub repository as owner/repo or its github.com address."; return; }
        if (!ModBrowserRepositories.Contains(repo, StringComparer.OrdinalIgnoreCase)) { ModBrowserRepositories.Add(repo); SaveModBrowserPreferences(); }
        NewModBrowserRepository = string.Empty;
    }

    private void SaveModBrowserPreferences()
    {
        try { _modBrowserPrefs.Save(new([.. ModBrowserFolders], [.. ModBrowserRepositories])); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { ModBrowserStatus = ex.Message; }
    }

    private void SaveCurseForgeKey()
    {
        var key = CurrentCurseForgeKey();
        if (string.IsNullOrWhiteSpace(key)) { ModBrowserStatus = "There is no key to save yet."; return; }
        _curseForgeKey = key;
        if (!OperatingSystem.IsWindows()) { ModBrowserStatus = "Saving the key is only supported on Windows; it stays in memory for this session."; return; }
        _credentialStore.Save(CurseForgeKeyCredentialId, key);
        CurseForgeKeyInput = string.Empty;
        ModBrowserStatus = "The CurseForge key is saved on this PC, encrypted for you.";
    }

    private void ForgetCurseForgeKey()
    {
        _credentialStore.Delete(CurseForgeKeyCredentialId);
        _curseForgeKey = string.Empty; CurseForgeKeyInput = string.Empty;
        ModBrowserStatus = "The CurseForge key was removed from this PC.";
    }

    private void OpenModBrowserPage()
    {
        if (SelectedModListing?.PageUrl is not { } url || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return;
        try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { ModBrowserStatus = ex.Message; }
    }

    private static string? DesktopExecutable => Environment.ProcessPath is { } p && System.IO.Path.GetFileNameWithoutExtension(p).Equals("MystTiq.Desktop", StringComparison.OrdinalIgnoreCase) ? p : null;

    private void RefreshNxmHandlerText()
    {
        if (!OperatingSystem.IsWindows()) { NxmHandlerText = "Taking Mod Manager Download links is available on Windows."; return; }
        var current = NxmProtocolRegistration.CurrentCommand();
        NxmHandlerText = current is null ? "No program takes Nexus Mod Manager Download links on this PC."
            : DesktopExecutable is { } exe && NxmProtocolRegistration.IsMine(exe) ? "MystTiq takes Nexus Mod Manager Download links on this PC."
            : $"Another program takes Nexus Mod Manager Download links on this PC: {current}";
    }

    // The program that takes nxm:// links now when it is not this MystTiq (MainWindow asks before replacing it); null otherwise.
    public string? OtherNxmHandler =>
        OperatingSystem.IsWindows() && NxmProtocolRegistration.CurrentCommand() is { } current && !(DesktopExecutable is { } exe && NxmProtocolRegistration.IsMine(exe)) ? current : null;

    public void UseMystTiqForNxmLinks()
    {
        if (!OperatingSystem.IsWindows() || DesktopExecutable is not { } exe) { RefreshNxmHandlerText(); return; }
        NxmProtocolRegistration.Register(exe);
        RefreshNxmHandlerText();
    }

    private void StopUsingMystTiqForNxmLinks()
    {
        if (!OperatingSystem.IsWindows() || DesktopExecutable is not { } exe) { RefreshNxmHandlerText(); return; }
        NxmProtocolRegistration.Unregister(exe);
        RefreshNxmHandlerText();
    }
}
