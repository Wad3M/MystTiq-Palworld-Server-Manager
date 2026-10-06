// MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Threading;
using MystTiq.Core.Services;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v1.0.5.0 (roadmap M-1; owner decision 2026-10-06): the MOD browser on the MOD Library page. Search a source, pick a MOD
// and one of its files; Check and Install (MainWindow's Click handler) fetches it from that source's own hosts, shows what
// the archive holds and whether MystTiq can install it, asks, then installs through the validated ZIP install.
public sealed partial class MainWindowViewModel
{
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

        SearchModBrowserCommand = new AsyncCommand(SearchModBrowserAsync, () => !IsBusy && SelectedModSource is not null);
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
        IsBusy = true; ModBrowserStatus = "Searching…";
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
        finally { IsBusy = false; }
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
        IsBusy = true; ModBrowserStatus = "Asking for the download…";
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
        finally { IsBusy = false; }
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
