// MystTiq v1.0.8.4: file reviewed for this release (2026-10-12).
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
/// v1.0.8.2 (roadmap C-2): the Players page -- the player directory and its filters, notes and warnings, the moderation
/// actions (kick, ban, unban, promote, whisper, temporary bans, the ban list, the whitelist), the name guard, starter kits,
/// Give Item and its picker, and the selected player's inventory and Pal box -- moved out of MainWindowViewModel unchanged.
/// The window shows it as vm.Players; the map and the Dashboard read the selected and online players through it.
/// </summary>
public sealed class PlayersViewModel : PageViewModel
{
    private string _playerSearchText = string.Empty;
    private string _moderationProviderStatusText = string.Empty;
    public string PlayerSearchText
    {
        get => _playerSearchText;
        set { if (SetField(ref _playerSearchText, value ?? string.Empty)) ApplyPlayerFilters(); }
    }
    public string ModerationProviderStatusText { get => _moderationProviderStatusText; internal set => SetField(ref _moderationProviderStatusText, value); }

    /// <summary>Raised when the page's commands may have changed state (the window's teleport buttons follow it).</summary>
    public event Action? CommandStatesChanged;

    private readonly Func<Task> _refreshActivity;
    private readonly Func<Task> _refreshPage;

    public PlayersViewModel(SessionContext session, Func<Task> refreshActivity, Func<Task> refreshPage) : base(session, NavigationPage.Players)
    {
        _refreshActivity = refreshActivity;
        _refreshPage = refreshPage;
        RefreshWhitelistCommand = new AsyncCommand(RefreshWhitelistAsync, () => !Session.IsBusy);
        SaveWhitelistCommand = new AsyncCommand(SaveWhitelistAsync, () => !Session.IsBusy);
        AddWhitelistEntryCommand = new RelayCommand(AddWhitelistEntry);
        RemoveWhitelistEntryCommand = new RelayCommand(RemoveSelectedWhitelistEntry);
        ToggleNameGuardCommand = new RelayCommand(ToggleNameGuard);
        RefreshNameGuardCommand = new AsyncCommand(RefreshNameGuardAsync, () => !Session.IsBusy);
        SaveNameGuardCommand = new AsyncCommand(SaveNameGuardAsync, () => !Session.IsBusy);
        ReserveNameCommand = new RelayCommand(ReserveName);
        ReleaseNameCommand = new RelayCommand(ReleaseSelectedName);
        LoadPlayerMetadataCommand = new AsyncCommand(LoadSelectedPlayerMetadataAsync, () => !Session.IsBusy && SelectedPlayerRecord is not null);
        SavePlayerNotesCommand = new AsyncCommand(SaveSelectedPlayerNotesAsync, () => !Session.IsBusy && SelectedPlayerRecord is not null);
        AddPlayerWarningCommand = new AsyncCommand(AddSelectedPlayerWarningAsync, () => !Session.IsBusy && SelectedPlayerRecord is not null && !string.IsNullOrWhiteSpace(NewPlayerWarning));
        KickSelectedPlayerCommand = new AsyncCommand(() => RunSelectedPlayerAdminActionAsync("kick"), () => !Session.IsBusy && SelectedPlayerRecord?.Online == true);
        BanSelectedPlayerCommand = new AsyncCommand(() => RunSelectedPlayerAdminActionAsync("ban"), () => !Session.IsBusy && SelectedPlayerRecord?.Online == true);
        UnbanSelectedPlayerCommand = new AsyncCommand(() => RunSelectedPlayerAdminActionAsync("unban", requireOnline: false), () => !Session.IsBusy && SelectedPlayerRecord is not null);
        RefreshBanListCommand = new AsyncCommand(RefreshBanListAsync, () => !Session.IsBusy);
        WhisperSelectedPlayerCommand = new AsyncCommand(() => RunSelectedPlayerAdminActionAsync("whisper"), () => false);
        PromoteSelectedPlayerCommand = new AsyncCommand(() => RunSelectedPlayerAdminActionAsync("promote"), () => false);
        GiveItemSelectedPlayerCommand = new AsyncCommand(GiveItemsToSelectedPlayerAsync, () => !Session.IsBusy && SelectedPlayerRecord?.Online == true && !string.IsNullOrWhiteSpace(GiveItemsText));
        LoadInventoryCommand = new AsyncCommand(LoadInventoryAsync, () => !Session.IsBusy && SelectedPlayerRecord is not null);
        ToggleBanListCommand = new RelayCommand(() =>
        {
            IsBanListExpanded = !IsBanListExpanded;
            if (IsBanListExpanded) Session.RunUi(() => RefreshBanListAsync());
        });
        ToggleWhitelistCommand = new RelayCommand(() =>
        {
            IsWhitelistExpanded = !IsWhitelistExpanded;
            if (IsWhitelistExpanded) Session.RunUi(() => RefreshWhitelistAsync());
        });
        ToggleTemporaryBansCommand = new RelayCommand(() =>
        {
            IsTemporaryBansExpanded = !IsTemporaryBansExpanded;
            if (IsTemporaryBansExpanded) Session.RunUi(() => RefreshTemporaryBansAsync());
        });
        RefreshTemporaryBansCommand = new AsyncCommand(RefreshTemporaryBansAsync, () => !Session.IsBusy);
        CreateTemporaryBanCommand = new AsyncCommand(CreateTemporaryBanForSelectedPlayerAsync, () => !Session.IsBusy && SelectedPlayerRecord?.Online == true);
        CancelTemporaryBanCommand = new RelayCommand<string>(playerId => Session.RunUi(() => CancelTemporaryBanAsync(playerId)));
        InitializePalBox();
        InitializeKits();
        InitializeGameIdPicker();
    }

    private string _playerRecordCountText = "—";

    private PlayerExplorerItemDto? _selectedPlayerRecord;

    private string _selectedPlayerViewFilter = "All Players";

    private string _selectedPlayerAdminFilter = "All Records";

    private bool _hideDuplicatePlayerNames;

    private string _playersPageState = "Not loaded";

    private string _playersPageDetail = "Connect to discover live and saved player records.";

    private string _playerVisibleCountText = "0 visible";

    private string _selectedPlayerNotes = string.Empty;

    private string _newPlayerWarning = string.Empty;

    private string _playerMetadataState = "Select a player to load notes and warnings.";

    private WhitelistConfigDto _whitelistConfig = new();

    private string _whitelistState = "Click Refresh to load the whitelist.";

    private string _newWhitelistPlayerId = string.Empty;

    private string _newWhitelistLabel = string.Empty;

    private WhitelistEntryDto? _selectedWhitelistEntry;

    private string _playerAdminStatusText = "Right-click a player for administration actions.";

    private string _banListText = "Click Refresh to load the RCON ban list.";

    private string _playerRegistrySummaryText = string.Empty;

    private string _playerActionMessage = "Removed by administrator.";

    private string _playerActionItem = string.Empty;

    private bool _isBanListExpanded;

    private bool _isWhitelistExpanded;

    private bool _isTemporaryBansExpanded;

    private string _temporaryBanState = "Click Refresh to load active temporary bans.";

    private double _newTemporaryBanDurationHours = 24;

    // ---- v0.7.94.0: Starter Kits -----------------------------------------------------------------
    // Edit kits locally (name plus one entry per line), one explicit Save persists them all. Delivery
    // needs PalDefender (vanilla Palworld has no give command), so the card shows the provider's real
    // status and a Test button rather than assuming it works.
    private bool _isKitsExpanded;
    private string _kitStatusText = "Open this card to load starter kits.";
    private string _kitProviderText = string.Empty;
    private KitDefinitionDto? _selectedKit;
    private string _kitNameText = string.Empty;
    private string _kitEntriesText = string.Empty;
    private bool _kitAutoGiftEnabled;
    private KitDefinitionDto? _kitAutoGiftKit;
    private string _kitAutoGiftNote = string.Empty;
    private bool _loadingKitEditor;
    private AsyncCommand[] _kitCommands = [];

    public ObservableCollection<KitDefinitionDto> Kits { get; } = [];
    public ObservableCollection<KitClaimDto> KitClaims { get; } = [];
    public bool IsKitsExpanded { get => _isKitsExpanded; private set => SetField(ref _isKitsExpanded, value); }
    public string KitStatusText { get => _kitStatusText; private set => SetField(ref _kitStatusText, value); }
    public string KitProviderText { get => _kitProviderText; private set => SetField(ref _kitProviderText, value); }
    public string KitAutoGiftNote { get => _kitAutoGiftNote; private set => SetField(ref _kitAutoGiftNote, value); }
    public bool KitAutoGiftEnabled { get => _kitAutoGiftEnabled; set => SetField(ref _kitAutoGiftEnabled, value); }
    public KitDefinitionDto? KitAutoGiftKit { get => _kitAutoGiftKit; set => SetField(ref _kitAutoGiftKit, value); }
    public bool HasSelectedKit => _selectedKit is not null;
    public KitDefinitionDto? SelectedKit
    {
        get => _selectedKit;
        set
        {
            if (!SetField(ref _selectedKit, value)) return;
            _loadingKitEditor = true;
            KitNameText = value?.Name ?? string.Empty;
            KitEntriesText = value is null ? string.Empty : KitEntryText.Format(value.Entries.Select(e => new KitTextEntry(e.Type, e.Id, e.Amount)));
            _loadingKitEditor = false;
            RaisePropertyChanged(nameof(HasSelectedKit));
            RaiseKitCommandStates();
        }
    }
    public string KitNameText
    {
        get => _kitNameText;
        set
        {
            if (!SetField(ref _kitNameText, value ?? string.Empty) || _loadingKitEditor || _selectedKit is null) return;
            _selectedKit.Name = _kitNameText;
        }
    }
    // Live-synced into the selected kit whenever the text parses; a bad line just leaves the last good
    // entries in place and says which line to fix, and Save refuses until it parses.
    public string KitEntriesText
    {
        get => _kitEntriesText;
        set
        {
            if (!SetField(ref _kitEntriesText, value ?? string.Empty) || _loadingKitEditor || _selectedKit is null) return;
            if (KitEntryText.TryParse(_kitEntriesText, out var parsed, out var error))
            {
                _selectedKit.Entries = parsed.Select(e => new KitEntryDto { Type = e.Type, Id = e.Id, Amount = e.Amount }).ToList();
                KitStatusText = "Unsaved kit changes. Press Save Kits to keep them.";
            }
            else KitStatusText = error;
        }
    }
    public ICommand ToggleKitsCommand { get; private set; } = null!;
    public ICommand RefreshKitsCommand { get; private set; } = null!;
    public ICommand NewKitCommand { get; private set; } = null!;
    public ICommand DeleteKitCommand { get; private set; } = null!;
    public ICommand SaveKitsCommand { get; private set; } = null!;
    public ICommand TestKitProviderCommand { get; private set; } = null!;
    public ICommand GiveSelectedKitCommand { get; private set; } = null!;
    public ICommand ResetKitClaimCommand { get; private set; } = null!;

    private void InitializeKits()
    {
        var refresh = new AsyncCommand(RefreshKitsAsync, () => !Session.IsBusy);
        var save = new AsyncCommand(SaveKitsAsync, () => !Session.IsBusy);
        var test = new AsyncCommand(TestKitProviderAsync, () => !Session.IsBusy);
        var give = new AsyncCommand(GiveSelectedKitAsync, () => !Session.IsBusy && SelectedKit is not null && SelectedPlayerRecord?.Online == true);
        var reset = new AsyncCommand(ResetKitClaimAsync, () => !Session.IsBusy && SelectedPlayerRecord is not null);
        RefreshKitsCommand = refresh;
        SaveKitsCommand = save;
        TestKitProviderCommand = test;
        GiveSelectedKitCommand = give;
        ResetKitClaimCommand = reset;
        NewKitCommand = new RelayCommand(NewKit);
        DeleteKitCommand = new RelayCommand(DeleteSelectedKit);
        ToggleKitsCommand = new RelayCommand(() =>
        {
            IsKitsExpanded = !IsKitsExpanded;
            if (IsKitsExpanded) Session.RunUi(() => RefreshKitsAsync());
        });
        _kitCommands = [refresh, save, test, give, reset];
    }

    internal void RaiseKitCommandStates()
    {
        foreach (var command in _kitCommands) command.RaiseCanExecuteChanged();
    }

    private void ApplyKitConfig(KitConfigDto config, KitProviderStatusDto? provider, IEnumerable<KitClaimDto>? claims)
    {
        var keepId = SelectedKit?.Id;
        Kits.Clear();
        foreach (var kit in config.Kits) Kits.Add(kit);
        SelectedKit = Kits.FirstOrDefault(k => k.Id == keepId) ?? Kits.FirstOrDefault();
        KitAutoGiftEnabled = config.AutoGiftEnabled;
        KitAutoGiftKit = Kits.FirstOrDefault(k => k.Id.Equals(config.AutoGiftKitId, StringComparison.OrdinalIgnoreCase));
        KitAutoGiftNote = config.AutoGiftEnabled && config.AutoGiftEnabledAtUtc is { } since
            ? $"Auto-gift has been on since {since.ToLocalTime():g}. Only players MystTiq first saw after that are gifted, once each."
            : "Auto-gift is off. When on, only players MystTiq first sees after you turn it on are gifted, once each. Existing players are never gifted.";
        if (provider is not null)
            KitProviderText = (provider.CanDeliver ? "Ready: " : "Cannot deliver: ") + provider.Detail;
        if (claims is not null)
        {
            KitClaims.Clear();
            foreach (var claim in claims) KitClaims.Add(claim);
        }
    }

    private async Task RefreshKitsAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var snapshot = await Session.Api.GetKitsAsync(Session.SelectedProfile, Session.BearerToken);
            ApplyKitConfig(snapshot.Config, snapshot.Provider, snapshot.Claims);
            KitStatusText = $"{Kits.Count} kit(s) loaded, {KitClaims.Count} recent claim(s).";
        }
        catch (Exception ex) { KitStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private void NewKit()
    {
        var kit = new KitDefinitionDto
        {
            Id = "kit-" + Guid.NewGuid().ToString("N")[..8],
            Name = "New kit",
            Entries = [new KitEntryDto { Type = "Item", Id = "PalSphere", Amount = 10 }]
        };
        Kits.Add(kit);
        SelectedKit = kit;
        KitStatusText = "New kit added. Edit it, then press Save Kits.";
    }

    private void DeleteSelectedKit()
    {
        if (SelectedKit is not { } kit) return;
        if (ReferenceEquals(KitAutoGiftKit, kit)) { KitAutoGiftKit = null; KitAutoGiftEnabled = false; }
        Kits.Remove(kit);
        SelectedKit = Kits.FirstOrDefault();
        KitStatusText = "Kit removed. Press Save Kits to keep that.";
    }

    private async Task SaveKitsAsync()
    {
        if (Session.SelectedProfile is null) return;
        if (!KitEntryText.TryParse(KitEntriesText, out _, out var parseError)) { KitStatusText = parseError; return; }
        Session.IsBusy = true;
        try
        {
            var request = new KitConfigDto
            {
                AutoGiftEnabled = KitAutoGiftEnabled && KitAutoGiftKit is not null,
                AutoGiftKitId = KitAutoGiftKit?.Id ?? string.Empty,
                Kits = [.. Kits]
            };
            var result = await Session.Api.SaveKitsAsync(Session.SelectedProfile, request, Session.BearerToken);
            if (!result.Success)
            {
                KitStatusText = result.Errors.Count > 0 ? string.Join(" ", result.Errors) : (string.IsNullOrWhiteSpace(result.Message) ? "The kits were not saved." : result.Message);
                return;
            }

            ApplyKitConfig(result.Config, null, null);
            KitStatusText = "Kits saved.";
        }
        catch (Exception ex) { KitStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task TestKitProviderAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true; KitStatusText = "Asking the server's PalDefender (version command over RCON)…";
        try
        {
            var result = await Session.Api.TestKitProviderAsync(Session.SelectedProfile, Session.BearerToken);
            KitStatusText = result.Success
                ? $"The server answered: {(string.IsNullOrWhiteSpace(result.Response) ? "(no text)" : result.Response)}"
                : result.Message;
        }
        catch (Exception ex) { KitStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // ---- v0.8.3.0: Give Item picker -------------------------------------------------------------------------
    // Search the item and Pal ids this server knows (world save, kits, earlier gives) and add one as a line to the
    // Give box or the selected kit, instead of typing internal ids from memory. Typing by hand still works.
    public const int MaximumShownGameIds = 300;
    internal readonly List<GameIdCatalogEntryDto> _gameIdCatalog = [];
    private string _gameIdFilterText = string.Empty;
    private string _gameIdAmountText = "1";
    private string _gameIdCatalogStatus = "Press Load IDs to list the item and Pal ids this world knows.";
    private string _gameIdCatalogDetail = string.Empty;
    private GameIdCatalogEntryDto? _selectedGameId;
    internal AsyncCommand? _loadGameIdsCommand;
    private RelayCommand? _addGameIdToGiveCommand;
    private RelayCommand? _addGameIdToKitCommand;

    public ObservableCollection<GameIdCatalogEntryDto> FilteredGameIds { get; } = [];
    public string GameIdCatalogStatus { get => _gameIdCatalogStatus; private set => SetField(ref _gameIdCatalogStatus, value); }
    public string GameIdFilterText
    {
        get => _gameIdFilterText;
        set { if (SetField(ref _gameIdFilterText, value ?? string.Empty)) ApplyGameIdFilter(); }
    }
    public string GameIdAmountText { get => _gameIdAmountText; set => SetField(ref _gameIdAmountText, value ?? string.Empty); }
    public string GameIdAmountLabel => SelectedGameId?.IsPal == true ? "Level" : "Amount";
    public GameIdCatalogEntryDto? SelectedGameId
    {
        get => _selectedGameId;
        set
        {
            var wasPal = _selectedGameId?.IsPal == true;
            if (!SetField(ref _selectedGameId, value)) return;
            RaisePropertyChanged(nameof(GameIdAmountLabel));
            // Switching between items (counts) and Pals (levels) resets a value that would not make sense for the other.
            if (value is not null && value.IsPal != wasPal) GameIdAmountText = "1";
            _addGameIdToGiveCommand?.RaiseCanExecuteChanged();
            _addGameIdToKitCommand?.RaiseCanExecuteChanged();
        }
    }
    public ICommand LoadGameIdsCommand { get; private set; } = null!;
    public ICommand AddGameIdToGiveCommand { get; private set; } = null!;
    public ICommand AddGameIdToKitCommand { get; private set; } = null!;

    private void InitializeGameIdPicker()
    {
        _loadGameIdsCommand = new AsyncCommand(LoadGameIdsAsync, () => !Session.IsBusy);
        _addGameIdToGiveCommand = new RelayCommand(() => AddSelectedGameId(toKit: false), () => SelectedGameId is not null);
        _addGameIdToKitCommand = new RelayCommand(() => AddSelectedGameId(toKit: true), () => SelectedGameId is not null);
        LoadGameIdsCommand = _loadGameIdsCommand;
        AddGameIdToGiveCommand = _addGameIdToGiveCommand;
        AddGameIdToKitCommand = _addGameIdToKitCommand;
    }

    internal async Task LoadGameIdsAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true; GameIdCatalogStatus = "Reading the item and Pal ids from the world save…";
        try
        {
            var catalog = await Session.Api.GetGameIdCatalogAsync(Session.SelectedProfile, Session.BearerToken);
            _gameIdCatalog.Clear();
            _gameIdCatalog.AddRange(catalog.Entries);
            ApplyGameIdFilter();
            // v0.8.13.0: say where the names come from, or why there are none.
            _gameIdCatalogDetail = string.IsNullOrWhiteSpace(catalog.NamesDetail) ? catalog.Detail : $"{catalog.Detail} {catalog.NamesDetail}";
            GameIdCatalogStatus = _gameIdCatalogDetail;
        }
        catch (Exception ex) { GameIdCatalogStatus = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // v0.9.2.0: the picker's names come in the display language, so a language change reads a loaded catalog again.
    internal void OnLanguageChanged()
    {
        if (_gameIdCatalog.Count > 0 && Session.SelectedProfile is not null && !Session.IsBusy) _ = LoadGameIdsAsync();
    }

    internal void ClearGameIdCatalog()
    {
        _gameIdCatalog.Clear();
        FilteredGameIds.Clear();
        SelectedGameId = null;
        GameIdCatalogStatus = "Press Load IDs to list the item and Pal ids this world knows.";
    }

    private void ApplyGameIdFilter()
    {
        var keep = SelectedGameId;
        // v0.8.13.0: the display name is searched too ("pal sphere", "lamball").
        var matches = _gameIdCatalog.Where(e => GameIdSearch.Matches(e.Id, e.Name, GameIdFilterText)).ToList();
        FilteredGameIds.Clear();
        foreach (var entry in matches.Take(MaximumShownGameIds)) FilteredGameIds.Add(entry);
        SelectedGameId = keep is not null && FilteredGameIds.Contains(keep) ? keep : null;
        // v0.8.13.0: back to the catalogue's own line once the search is narrow enough (it used to keep the "first 300" note).
        if (_gameIdCatalog.Count > 0)
            GameIdCatalogStatus = matches.Count > MaximumShownGameIds
                ? $"Showing the first {MaximumShownGameIds} of {matches.Count} matches. Type more of the name to narrow it down."
                : _gameIdCatalogDetail;
    }

    private void AddSelectedGameId(bool toKit)
    {
        if (SelectedGameId is not { } entry) return;
        var type = entry.IsPal ? "Pal" : "Item";
        if (!KitEntryText.TryParseAmount(type, GameIdAmountText, out var amount, out var error)) { GameIdCatalogStatus = error; return; }
        var line = new KitTextEntry(type, entry.Id, amount);
        if (toKit)
        {
            if (SelectedKit is null) { GameIdCatalogStatus = "Select or create a kit under Starter Kits first."; return; }
            KitEntriesText = KitEntryText.Append(KitEntriesText, line);
            GameIdCatalogStatus = $"Added {KitEntryText.Format([line])} to kit '{SelectedKit.Name}'. Press Save Kits to keep it.";
        }
        else
        {
            GiveItemsText = KitEntryText.Append(GiveItemsText, line);
            GameIdCatalogStatus = $"Added {KitEntryText.Format([line])} to the Give box.";
        }
    }

    // v0.7.112.0: items/Pals to give the selected online player, in the kit editor's line format.
    private string _giveItemsText = string.Empty;
    public string GiveItemsText
    {
        get => _giveItemsText;
        set { if (SetField(ref _giveItemsText, value ?? string.Empty)) (GiveItemSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }

    private async Task GiveItemsToSelectedPlayerAsync()
    {
        if (Session.SelectedProfile is null || SelectedPlayerRecord is not { Online: true } player) return;
        if (!KitEntryText.TryParse(GiveItemsText, out var parsed, out var error)) { PlayerAdminStatusText = error; return; }
        if (parsed.Count == 0) { PlayerAdminStatusText = "Enter at least one item or Pal to give."; return; }
        Session.IsBusy = true; PlayerAdminStatusText = $"Giving {parsed.Count} entr{(parsed.Count == 1 ? "y" : "ies")} to {player.PlayerName}…";
        try
        {
            var entries = parsed.Select(e => new KitEntryDto { Type = e.Type, Id = e.Id, Amount = e.Amount }).ToList();
            var result = await Session.Api.GiveItemsAsync(Session.SelectedProfile, player.PlayerId, entries, Session.BearerToken);
            PlayerAdminStatusText = result.Success
                ? $"{result.Message}{(string.IsNullOrWhiteSpace(result.Response) ? string.Empty : " Server: " + result.Response)}"
                : result.Message;
        }
        catch (Exception ex) { PlayerAdminStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task GiveSelectedKitAsync()
    {
        if (Session.SelectedProfile is null || SelectedKit is not { } kit || SelectedPlayerRecord is not { } player) return;
        Session.IsBusy = true; KitStatusText = $"Giving '{kit.Name}' to {player.PlayerName}…";
        try
        {
            var result = await Session.Api.GiveKitAsync(Session.SelectedProfile, kit.Id, player.PlayerId, Session.BearerToken);
            KitStatusText = result.Success
                ? $"{result.Message}{(string.IsNullOrWhiteSpace(result.Response) ? string.Empty : " Server: " + result.Response)}"
                : result.Message + (result.Message.Contains("no longer exists", StringComparison.OrdinalIgnoreCase) ? " Press Save Kits first, then try again." : string.Empty);
        }
        catch (Exception ex) { KitStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task ResetKitClaimAsync()
    {
        if (Session.SelectedProfile is null || SelectedPlayerRecord is not { } player) return;
        Session.IsBusy = true;
        try
        {
            await Session.Api.ForgetKitClaimsAsync(Session.SelectedProfile, player.PlayerId, Session.BearerToken);
            KitStatusText = $"Cleared {player.PlayerName}'s kit claims, so auto-gift can give them the kit again if they qualify.";
            var snapshot = await Session.Api.GetKitsAsync(Session.SelectedProfile, Session.BearerToken);
            KitClaims.Clear();
            foreach (var claim in snapshot.Claims) KitClaims.Add(claim);
        }
        catch (Exception ex) { KitStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    public PlayerExplorerItemDto? SelectedPlayerRecord
    {
        get => _selectedPlayerRecord;
        set
        {
            if (!SetField(ref _selectedPlayerRecord, value)) return;
            SelectedPlayerWarnings.Clear();
            SelectedPlayerNotes = string.Empty;
            PlayerMetadataState = value is null ? "Select a player to load notes and warnings." : "Metadata not loaded.";
            RaisePlayerCommandStates();
            RaiseKitCommandStates();
        }
    }

    public string SelectedPlayerViewFilter
    {
        get => _selectedPlayerViewFilter;
        set { if (SetField(ref _selectedPlayerViewFilter, value ?? "All Players")) ApplyPlayerFilters(); }
    }

    public string SelectedPlayerAdminFilter
    {
        get => _selectedPlayerAdminFilter;
        set { if (SetField(ref _selectedPlayerAdminFilter, value ?? "All Records")) ApplyPlayerFilters(); }
    }

    // v0.7.18.0: requested directly -- a selectable way to declutter the Directory list when the
    // same physical player shows up multiple times under the same display name (a rejoin under a
    // different platform ID, a stale record from an old save, etc.). Purely a view-side filter --
    // it hides rows, it never deletes or merges the underlying player records those extra PlayerIds
    // still legitimately identify, so switching it off always brings every record straight back.
    public bool HideDuplicatePlayerNames
    {
        get => _hideDuplicatePlayerNames;
        set { if (SetField(ref _hideDuplicatePlayerNames, value)) ApplyPlayerFilters(); }
    }

    public string NewPlayerWarning
    {
        get => _newPlayerWarning;
        set
        {
            if (!SetField(ref _newPlayerWarning, value ?? string.Empty)) return;
            (AddPlayerWarningCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ObservableCollection<PlayerExplorerItemDto> PlayerRecords { get; } = [];

    public ObservableCollection<PlayerExplorerItemDto> FilteredPlayerRecords { get; } = [];

    public ObservableCollection<PlayerWarningDto> SelectedPlayerWarnings { get; } = [];

    public IReadOnlyList<string> PlayerViewFilters { get; } = ["All Players", "Online", "Known Saves", "Missing Saves"];

    public IReadOnlyList<string> PlayerAdminFilters { get; } = ["All Records", "Actionable Online", "Needs Review"];

    public string PlayersPageState { get => _playersPageState; internal set => SetField(ref _playersPageState, value); }

    public string PlayersPageDetail { get => _playersPageDetail; internal set => SetField(ref _playersPageDetail, value); }

    public string PlayerVisibleCountText { get => _playerVisibleCountText; private set => SetField(ref _playerVisibleCountText, value); }

    public string SelectedPlayerNotes { get => _selectedPlayerNotes; set => SetField(ref _selectedPlayerNotes, value ?? string.Empty); }

    public string PlayerMetadataState { get => _playerMetadataState; internal set => SetField(ref _playerMetadataState, value); }

    public string PlayerAdminStatusText { get => _playerAdminStatusText; internal set => SetField(ref _playerAdminStatusText, value); }

    public string BanListText { get => _banListText; internal set => SetField(ref _banListText, value); }

    public string PlayerRegistrySummaryText { get => _playerRegistrySummaryText; private set => SetField(ref _playerRegistrySummaryText, value); }

    public string PlayerActionMessage { get => _playerActionMessage; set => SetField(ref _playerActionMessage, value); }

    public string PlayerActionItem { get => _playerActionItem; set => SetField(ref _playerActionItem, value); }

    public string PlayerRecordCountText { get => _playerRecordCountText; internal set => SetField(ref _playerRecordCountText, value); }

    // v0.7.10.0: whitelist config is read/replaced as a whole, mirroring the Discord Bot config
    // pattern immediately above (add/remove entries locally, one explicit Save persists the list).
    public string WhitelistState { get => _whitelistState; private set => SetField(ref _whitelistState, value); }

    public WhitelistConfigDto WhitelistConfig { get => _whitelistConfig; set => SetField(ref _whitelistConfig, value ?? new()); }

    public ObservableCollection<WhitelistEntryDto> WhitelistEntries { get; } = [];

    public string NewWhitelistPlayerId { get => _newWhitelistPlayerId; set => SetField(ref _newWhitelistPlayerId, value ?? string.Empty); }

    public string NewWhitelistLabel { get => _newWhitelistLabel; set => SetField(ref _newWhitelistLabel, value ?? string.Empty); }

    public WhitelistEntryDto? SelectedWhitelistEntry { get => _selectedWhitelistEntry; set => SetField(ref _selectedWhitelistEntry, value); }

    // v0.7.15.0: temporary ban -- bans the currently selected player (same selection/online gating
    // as Kick/Ban above) for a chosen duration, then the headless service auto-unbans once it
    // elapses. This card only ever displays server-reported state (GetTemporaryBansAsync); there is
    // no local add/remove list to edit before saving, unlike Whitelist.
    public string TemporaryBanState { get => _temporaryBanState; private set => SetField(ref _temporaryBanState, value); }

    public ObservableCollection<TemporaryBanEntryDto> TemporaryBans { get; } = [];

    public double NewTemporaryBanDurationHours { get => _newTemporaryBanDurationHours; set => SetField(ref _newTemporaryBanDurationHours, value <= 0 ? 1 : value); }

    public bool IsBanListExpanded { get => _isBanListExpanded; private set => SetField(ref _isBanListExpanded, value); }

    public bool IsWhitelistExpanded { get => _isWhitelistExpanded; private set => SetField(ref _isWhitelistExpanded, value); }

    public bool IsTemporaryBansExpanded { get => _isTemporaryBansExpanded; private set => SetField(ref _isTemporaryBansExpanded, value); }

    public ICommand AddPlayerWarningCommand { get; }

    public ICommand AddWhitelistEntryCommand { get; }

    public ICommand BanSelectedPlayerCommand { get; }

    public ICommand CancelTemporaryBanCommand { get; }

    public ICommand CreateTemporaryBanCommand { get; }

    public ICommand GiveItemSelectedPlayerCommand { get; }

    public ICommand KickSelectedPlayerCommand { get; }

    public ICommand LoadPlayerMetadataCommand { get; }

    public ICommand PromoteSelectedPlayerCommand { get; }

    public ICommand RefreshBanListCommand { get; }

    public ICommand RefreshTemporaryBansCommand { get; }

    public ICommand RefreshWhitelistCommand { get; }

    public ICommand RemoveWhitelistEntryCommand { get; }

    public ICommand SavePlayerNotesCommand { get; }

    public ICommand SaveWhitelistCommand { get; }

    public ICommand ToggleBanListCommand { get; }

    public ICommand ToggleTemporaryBansCommand { get; }

    public ICommand ToggleWhitelistCommand { get; }

    public ICommand UnbanSelectedPlayerCommand { get; }

    public ICommand WhisperSelectedPlayerCommand { get; }

    internal void ApplyPlayerFilters()
    {
        var selectedId = SelectedPlayerRecord?.PlayerId;
        var search = PlayerSearchText.Trim();
        var filtered = PlayerRecords.Where(player =>
        {
            if (!string.IsNullOrWhiteSpace(search) &&
                !player.PlayerId.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !player.PlayerName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !player.SteamId.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !player.UserId.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !player.GuildName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !player.Platform.Contains(search, StringComparison.OrdinalIgnoreCase))
                return false;

            if (SelectedPlayerViewFilter == "Online" && !player.Online) return false;
            if (SelectedPlayerViewFilter == "Known Saves" && !player.SaveExists) return false;
            if (SelectedPlayerViewFilter == "Missing Saves" && player.SaveExists) return false;
            if (SelectedPlayerAdminFilter == "Actionable Online" && !player.Online) return false;
            if (SelectedPlayerAdminFilter == "Needs Review" && player.SaveExists) return false;
            return true;
        }).ToArray();

        var duplicatesHidden = 0;
        if (HideDuplicatePlayerNames)
        {
            var beforeCount = filtered.Length;
            // One row per distinct PlayerName (case-insensitive; blank names are left ungrouped --
            // multiple genuinely-nameless records shouldn't collapse into one). Preference order for
            // which PlayerId "wins" a name: currently online, then has a save at all, then most
            // recently written -- the record an operator is actually looking for, not an arbitrary one.
            filtered = filtered
                .GroupBy(p => string.IsNullOrWhiteSpace(p.PlayerName) ? $"\0{p.PlayerId}" : p.PlayerName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g
                    .OrderByDescending(p => p.Online)
                    .ThenByDescending(p => p.SaveExists)
                    .ThenByDescending(p => p.SaveLastWriteUtc ?? DateTimeOffset.MinValue)
                    .First())
                .ToArray();
            duplicatesHidden = beforeCount - filtered.Length;
        }

        FilteredPlayerRecords.Clear();
        foreach (var player in filtered) FilteredPlayerRecords.Add(player);
        PlayerVisibleCountText = duplicatesHidden > 0
            ? $"{filtered.Length} visible / {PlayerRecords.Count} known ({duplicatesHidden} duplicate name(s) hidden)"
            : $"{filtered.Length} visible / {PlayerRecords.Count} known";

        if (!string.IsNullOrWhiteSpace(selectedId))
            SelectedPlayerRecord = filtered.FirstOrDefault(x => string.Equals(x.PlayerId, selectedId, StringComparison.OrdinalIgnoreCase));
        SelectedPlayerRecord ??= filtered.FirstOrDefault();
    }

    internal async Task LoadSelectedPlayerMetadataAsync()
    {
        if (SelectedPlayerRecord is null) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { PlayerMetadataState = ex.Message; return; }

        Session.IsBusy = true;
        try { await LoadSelectedPlayerMetadataCoreAsync(profile, SelectedPlayerRecord.PlayerId); }
        catch (Exception ex) { PlayerMetadataState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    internal async Task LoadSelectedPlayerMetadataCoreAsync(ConnectionProfile profile, string playerId)
    {
        var metadata = await Session.Api.GetPlayerMetadataAsync(profile, playerId, Session.BearerToken);
        if (!string.Equals(SelectedPlayerRecord?.PlayerId, playerId, StringComparison.OrdinalIgnoreCase)) return;
        ApplyPlayerMetadata(metadata, "Metadata loaded");

        try
        {
            var registry = await Session.Api.GetPlayerRegistryAsync(profile, Session.BearerToken);
            if (!string.Equals(SelectedPlayerRecord?.PlayerId, playerId, StringComparison.OrdinalIgnoreCase)) return;
            var record = registry.FirstOrDefault(r => string.Equals(r.PlayerId, playerId, StringComparison.OrdinalIgnoreCase));
            PlayerRegistrySummaryText = record is null
                ? "No registry history yet — first observed on the next status poll."
                : $"First seen {record.FirstSeenUtc.ToLocalTime():yyyy-MM-dd} · Last seen {record.LastSeenUtc.ToLocalTime():yyyy-MM-dd HH:mm} · {record.TotalSessions} session(s) · {record.TotalPlaytimeMinutes / 60:F1}h tracked playtime";
        }
        catch { PlayerRegistrySummaryText = string.Empty; }
    }

    private async Task SaveSelectedPlayerNotesAsync()
    {
        if (SelectedPlayerRecord is null) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { PlayerMetadataState = ex.Message; return; }

        Session.IsBusy = true;
        PlayerMetadataState = "Saving notes…";
        try
        {
            var metadata = await Session.Api.SavePlayerNotesAsync(profile, SelectedPlayerRecord.PlayerId, SelectedPlayerNotes, Session.BearerToken);
            ApplyPlayerMetadata(metadata, "Notes saved and audited by the headless service");
        }
        catch (Exception ex) { PlayerMetadataState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task AddSelectedPlayerWarningAsync()
    {
        if (SelectedPlayerRecord is null || string.IsNullOrWhiteSpace(NewPlayerWarning)) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { PlayerMetadataState = ex.Message; return; }

        Session.IsBusy = true;
        PlayerMetadataState = "Adding warning…";
        try
        {
            var metadata = await Session.Api.AddPlayerWarningAsync(profile, SelectedPlayerRecord.PlayerId, NewPlayerWarning, Session.BearerToken);
            NewPlayerWarning = string.Empty;
            ApplyPlayerMetadata(metadata, "Warning added and audited by the headless service");
        }
        catch (Exception ex) { PlayerMetadataState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private void ApplyPlayerMetadata(PlayerMetadataDto metadata, string state)
    {
        SelectedPlayerNotes = metadata.Notes;
        SelectedPlayerWarnings.Clear();
        foreach (var warning in metadata.Warnings.OrderByDescending(x => x.CreatedAt))
            SelectedPlayerWarnings.Add(warning);
        PlayerMetadataState = $"{state} · {metadata.Warnings.Count} warning(s) · {metadata.UpdatedAt.ToLocalTime():yyyy-MM-dd HH:mm}";
    }

    public string ExportVisiblePlayersCsv()
    {
        static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
        var builder = new StringBuilder();
        builder.AppendLine("PlayerId,PlayerName,SteamId,UserId,Online,Platform,Ping,GuildId,GuildName,Role,SaveExists,SaveSizeBytes,SaveLastWriteUtc,Evidence");
        foreach (var player in FilteredPlayerRecords)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                Csv(player.PlayerId), Csv(player.PlayerName), Csv(player.SteamId), Csv(player.UserId), Csv(player.Online.ToString()), Csv(player.Platform), Csv(player.Ping),
                Csv(player.GuildId), Csv(player.GuildName), Csv(player.Role), Csv(player.SaveExists.ToString()),
                Csv(player.SaveSizeBytes.ToString()), Csv(player.SaveLastWriteUtc?.ToString("O")), Csv(player.Evidence)
            }));
        }
        return builder.ToString();
    }

    private void RaisePlayerCommandStates()
    {
        (KickSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (BanSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (CreateTemporaryBanCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (UnbanSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        // The teleport buttons (the window's map code) follow the selected player too.
        CommandStatesChanged?.Invoke();
        (WhisperSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (PromoteSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (GiveItemSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (LoadPlayerMetadataCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (SavePlayerNotesCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (AddPlayerWarningCommand as AsyncCommand)?.RaiseCanExecuteChanged();
    }

    // v0.7.8.0: requireOnline defaults to true (kick/ban's original behavior) but is false for
    // unban -- a banned player is, by definition, never online, so gating it the same way as
    // kick/ban would make the action permanently unusable.
    private async Task RunSelectedPlayerAdminActionAsync(string action, bool requireOnline = true)
    {
        if (SelectedPlayerRecord is null)
        {
            PlayerAdminStatusText = "Select a player first.";
            return;
        }
        if (requireOnline && !SelectedPlayerRecord.Online)
        {
            PlayerAdminStatusText = "This action requires a currently online player.";
            return;
        }
        var playerId = SelectedPlayerRecord.PlayerId;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            PlayerAdminStatusText = "The selected player does not have a usable server-side identity.";
            return;
        }
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { PlayerAdminStatusText = ex.Message; return; }

        Session.IsBusy = true;
        PlayerAdminStatusText = $"Sending {action} for {SelectedPlayerRecord.PlayerName}…";
        try
        {
            var result = await Session.Api.RunPlayerAdminActionAsync(profile, playerId, action, PlayerActionMessage, PlayerActionItem, Session.BearerToken);
            PlayerAdminStatusText = result.Message;
            await _refreshActivity();
            if (action is "kick" or "ban" or "unban") await _refreshPage();
        }
        catch (Exception ex) { PlayerAdminStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // v0.7.75.0: "Delete Player Completely" / "Copy Player" -- code-behind (MainWindow.axaml.cs)
    // owns the dialog interaction (this ViewModel has no Window reference), these methods own the
    // actual API calls, matching how every other dialog-driven action in this codebase splits that
    // responsibility (e.g. CloseTabButton_OnClick / StopTabServerAsync).
    public async Task<PlayerDeletionPreviewDto?> PreviewDeleteSelectedPlayerAsync()
    {
        if (SelectedPlayerRecord is null) { PlayerAdminStatusText = "Select a player first."; return null; }
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { PlayerAdminStatusText = ex.Message; return null; }

        Session.IsBusy = true;
        try
        {
            var preview = await Session.Api.PreviewPlayerDeletionAsync(profile, SelectedPlayerRecord.PlayerId, Session.BearerToken);
            if (!preview.CanApply) PlayerAdminStatusText = string.Join(" ", preview.Findings);
            return preview;
        }
        catch (Exception ex) { PlayerAdminStatusText = ex.Message; return null; }
        finally { Session.IsBusy = false; }
    }

    public async Task<bool> ApplyDeleteSelectedPlayerAsync(string previewToken)
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { PlayerAdminStatusText = ex.Message; return false; }

        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ApplyPlayerDeletionAsync(profile, previewToken, true, Session.BearerToken);
            PlayerAdminStatusText = result.Message;
            await _refreshPage();
            await _refreshActivity();
            return result.Success;
        }
        catch (Exception ex) { PlayerAdminStatusText = ex.Message; return false; }
        finally { Session.IsBusy = false; }
    }

    private async Task RefreshBanListAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { BanListText = ex.Message; return; }

        Session.IsBusy = true;
        BanListText = "Loading ban list…";
        try
        {
            var result = await Session.Api.GetBanListAsync(profile, Session.BearerToken);
            BanListText = string.IsNullOrWhiteSpace(result.Response) ? "(empty -- no banned players, or the server returned no text)" : result.Response;
        }
        catch (Exception ex) { BanListText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task RefreshTemporaryBansAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { TemporaryBanState = ex.Message; return; }

        Session.IsBusy = true;
        try
        {
            var config = await Session.Api.GetTemporaryBansAsync(profile, Session.BearerToken);
            TemporaryBans.Clear();
            foreach (var entry in config.Entries) TemporaryBans.Add(entry);
            TemporaryBanState = TemporaryBans.Count == 0
                ? "No active temporary bans."
                : $"{TemporaryBans.Count} active temporary ban(s).";
        }
        catch (Exception ex) { TemporaryBanState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // v0.7.15.0: bans the currently selected online player (same selection this Kick/Ban card
    // already uses) for NewTemporaryBanDurationHours, reusing the reason text from
    // PlayerActionMessage. The headless service applies the ban immediately via the existing
    // ban path and auto-unbans once the duration elapses (HeadlessTemporaryBanService.EnforceAsync,
    // polled the same way HeadlessWhitelistService already is).
    private async Task CreateTemporaryBanForSelectedPlayerAsync()
    {
        if (SelectedPlayerRecord is null) { PlayerAdminStatusText = "Select a player first."; return; }
        if (!SelectedPlayerRecord.Online) { PlayerAdminStatusText = "Temporary ban requires a currently online player."; return; }
        var playerId = SelectedPlayerRecord.PlayerId;
        if (string.IsNullOrWhiteSpace(playerId)) { PlayerAdminStatusText = "The selected player does not have a usable server-side identity."; return; }

        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { PlayerAdminStatusText = ex.Message; return; }

        Session.IsBusy = true;
        PlayerAdminStatusText = $"Applying a {NewTemporaryBanDurationHours:F1}-hour temporary ban to {SelectedPlayerRecord.PlayerName}…";
        try
        {
            var result = await Session.Api.CreateTemporaryBanAsync(profile, playerId, SelectedPlayerRecord.PlayerName, PlayerActionMessage, NewTemporaryBanDurationHours, Session.BearerToken);
            PlayerAdminStatusText = result.Message;
            await _refreshActivity();
            await _refreshPage();
            if (IsTemporaryBansExpanded) await RefreshTemporaryBansAsync();
        }
        catch (Exception ex) { PlayerAdminStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // v0.7.15.0: early-lifts one active temporary ban from the list card below, via the same
    // unban action the standalone Unban button uses -- the headless service clears its own tracked
    // expiry for this player as soon as that unban succeeds (HeadlessTemporaryBanService.ForgetIfPresent),
    // so its poll sweep won't attempt a second, redundant unban later.
    private async Task CancelTemporaryBanAsync(string? playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { TemporaryBanState = ex.Message; return; }

        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.RunPlayerAdminActionAsync(profile, playerId, "unban", "Temporary ban cancelled by administrator.", null, Session.BearerToken);
            TemporaryBanState = result.Message;
            await RefreshTemporaryBansAsync();
        }
        catch (Exception ex) { TemporaryBanState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task RefreshWhitelistAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var config = await Session.Api.GetWhitelistAsync(Session.SelectedProfile, Session.BearerToken);
            WhitelistConfig = config;
            WhitelistEntries.Clear();
            foreach (var entry in config.Entries) WhitelistEntries.Add(entry);
            WhitelistState = config.Enabled
                ? $"Whitelist enabled -- {config.Entries.Count} player(s) allowed; anyone else is auto-kicked."
                : $"Whitelist disabled -- {config.Entries.Count} player(s) saved but not enforced.";
        }
        catch (Exception ex) { WhitelistState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task SaveWhitelistAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            WhitelistConfig.Entries = [.. WhitelistEntries];
            var saved = await Session.Api.SaveWhitelistAsync(Session.SelectedProfile, WhitelistConfig, Session.BearerToken);
            WhitelistConfig = saved;
            WhitelistEntries.Clear();
            foreach (var entry in saved.Entries) WhitelistEntries.Add(entry);
            WhitelistState = "Whitelist saved.";
        }
        catch (Exception ex) { WhitelistState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private void AddWhitelistEntry()
    {
        if (string.IsNullOrWhiteSpace(NewWhitelistPlayerId)) return;
        WhitelistEntries.Add(new WhitelistEntryDto { PlayerId = NewWhitelistPlayerId.Trim(), Label = NewWhitelistLabel.Trim() });
        NewWhitelistPlayerId = string.Empty;
        NewWhitelistLabel = string.Empty;
    }

    private void RemoveSelectedWhitelistEntry()
    {
        if (SelectedWhitelistEntry is null) return;
        WhitelistEntries.Remove(SelectedWhitelistEntry);
        SelectedWhitelistEntry = null;
    }

    // v1.0.0.2 (asked 2026-10-04): the Players page's Unique player names card. Each name belongs to the first account seen
    // with it, or to the account it is reserved for; the service turns away a player using another account's name.
    private NameGuardConfigDto _nameGuardConfig = new();
    private string _nameGuardState = string.Empty;
    private string _newReservedName = string.Empty;
    private string _newReservedOwner = string.Empty;
    private NameClaimDto? _selectedNameClaim;
    private bool _isNameGuardExpanded;

    public NameGuardConfigDto NameGuardConfig { get => _nameGuardConfig; set => SetField(ref _nameGuardConfig, value ?? new()); }
    public ObservableCollection<NameClaimDto> NameClaims { get; } = [];
    public ObservableCollection<NameGuardEventDto> NameGuardEvents { get; } = [];
    public bool HasNameGuardEvents => NameGuardEvents.Count > 0;
    public string NameGuardState { get => _nameGuardState; private set => SetField(ref _nameGuardState, value); }
    public string NewReservedName { get => _newReservedName; set => SetField(ref _newReservedName, value ?? string.Empty); }
    public string NewReservedOwner { get => _newReservedOwner; set => SetField(ref _newReservedOwner, value ?? string.Empty); }
    public NameClaimDto? SelectedNameClaim { get => _selectedNameClaim; set => SetField(ref _selectedNameClaim, value); }
    public bool IsNameGuardExpanded { get => _isNameGuardExpanded; private set => SetField(ref _isNameGuardExpanded, value); }

    public ICommand ToggleNameGuardCommand { get; }
    public ICommand RefreshNameGuardCommand { get; }
    public ICommand SaveNameGuardCommand { get; }
    public ICommand ReserveNameCommand { get; }
    public ICommand ReleaseNameCommand { get; }

    private void ToggleNameGuard()
    {
        IsNameGuardExpanded = !IsNameGuardExpanded;
        if (IsNameGuardExpanded) Session.RunUi(() => RefreshNameGuardAsync());
    }

    private async Task RefreshNameGuardAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var snapshot = await Session.Api.GetNameGuardAsync(Session.SelectedProfile, Session.BearerToken);
            ShowNameGuard(snapshot.Config);
            NameGuardEvents.Clear();
            foreach (var entry in snapshot.Events.OrderByDescending(e => e.At).Take(20)) NameGuardEvents.Add(entry);
            RaisePropertyChanged(nameof(HasNameGuardEvents));
            NameGuardState = NameGuardText.Describe(snapshot.Config);
        }
        catch (Exception ex) { NameGuardState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task SaveNameGuardAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            NameGuardConfig.Claims = [.. NameClaims];
            var saved = await Session.Api.SaveNameGuardAsync(Session.SelectedProfile, NameGuardConfig, Session.BearerToken);
            ShowNameGuard(saved);
            NameGuardState = NameGuardText.Describe(saved);
        }
        catch (Exception ex) { NameGuardState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private void ShowNameGuard(NameGuardConfigDto config)
    {
        NameGuardConfig = config;
        NameClaims.Clear();
        foreach (var claim in config.Claims.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)) NameClaims.Add(claim);
    }

    // Reserving a name already listed gives it to the new owner (or blocks it); the change is kept when Save is clicked.
    private void ReserveName()
    {
        var (claims, message) = NameGuardText.Reserve(NameClaims, NewReservedName, NewReservedOwner, DateTimeOffset.UtcNow);
        NameGuardState = message;
        if (claims is null) return;
        NameClaims.Clear();
        foreach (var claim in claims) NameClaims.Add(claim);
        NewReservedName = string.Empty;
        NewReservedOwner = string.Empty;
    }

    private void ReleaseSelectedName()
    {
        if (SelectedNameClaim is null) return;
        NameClaims.Remove(SelectedNameClaim);
        SelectedNameClaim = null;
        NameGuardState = "Released once you save. The next player to use the name will own it.";
    }

    // v1.0.6.0 (roadmap S-3; owner decisions D-2 guarded edits only, D-7 add and remove a Pal): the selected player's Pal box
    // from the world save, one Pal added (a copy of one of that species already in a Pal box) or removed, each previewed,
    // confirmed (MainWindow's Click handlers) and applied with a fresh, checked safety backup while the server is stopped.
    public ObservableCollection<PalBoxPalDto> PalBoxPals { get; } = [];
    public ObservableCollection<string> PalBoxSpecies { get; } = [];
    private PalBoxPalDto? _selectedPalBoxPal;
    public PalBoxPalDto? SelectedPalBoxPal { get => _selectedPalBoxPal; set => SetField(ref _selectedPalBoxPal, value); }
    private string? _selectedPalBoxSpecies;
    public string? SelectedPalBoxSpecies { get => _selectedPalBoxSpecies; set => SetField(ref _selectedPalBoxSpecies, value); }
    private string _palBoxStatus = string.Empty;
    public string PalBoxStatus { get => _palBoxStatus; private set => SetField(ref _palBoxStatus, value); }
    public ICommand LoadPalBoxCommand { get; private set; } = null!;

    private void InitializePalBox() => LoadPalBoxCommand = new AsyncCommand(LoadPalBoxAsync, () => !Session.IsBusy && SelectedPlayerRecord is not null);

    private async Task LoadPalBoxAsync()
    {
        if (Session.SelectedProfile is null || SelectedPlayerRecord?.PlayerId is not { Length: > 0 } playerId) { PalBoxStatus = "Select a player first."; return; }
        Session.IsBusy = true;
        PalBoxStatus = "Reading the world save…";
        try
        {
            var view = await Session.Api.GetPalBoxAsync(Session.SelectedProfile, playerId, Session.BearerToken);
            PalBoxPals.Clear();
            foreach (var pal in view.Pals) PalBoxPals.Add(pal);
            var keep = SelectedPalBoxSpecies;
            PalBoxSpecies.Clear();
            foreach (var species in view.AddableSpecies) PalBoxSpecies.Add(species);
            SelectedPalBoxSpecies = keep is not null && PalBoxSpecies.Contains(keep) ? keep : PalBoxSpecies.FirstOrDefault();
            PalBoxStatus = view.Detail;
        }
        catch (Exception ex) { PalBoxStatus = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    public async Task<PalBoxEditPreviewDto?> PreviewPalBoxEditAsync(string action)
    {
        if (Session.SelectedProfile is null || SelectedPlayerRecord?.PlayerId is not { Length: > 0 } playerId) { PalBoxStatus = "Select a player first."; return null; }
        var request = new PalBoxEditRequestDto { PlayerId = playerId, Action = action };
        if (action == "remove")
        {
            if (SelectedPalBoxPal is not { } pal) { PalBoxStatus = "Select a Pal in the box first."; return null; }
            request.InstanceId = pal.InstanceId;
        }
        else
        {
            if (SelectedPalBoxSpecies is not { Length: > 0 } species) { PalBoxStatus = "Pick the Pal to add first."; return null; }
            request.Species = species;
        }
        Session.IsBusy = true;
        try
        {
            var preview = await Session.Api.PreviewPalBoxEditAsync(Session.SelectedProfile, request, Session.BearerToken);
            if (!preview.CanApply) PalBoxStatus = string.Join(" ", preview.Findings);
            return preview;
        }
        catch (Exception ex) { PalBoxStatus = ex.Message; return null; }
        finally { Session.IsBusy = false; }
    }

    public async Task ApplyPalBoxEditAsync(string token)
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ApplyPalBoxEditAsync(Session.SelectedProfile, token, Session.BearerToken);
            PalBoxStatus = result.Message;
        }
        catch (Exception ex) { PalBoxStatus = ex.Message; }
        finally { Session.IsBusy = false; }
        await LoadPalBoxAsync();
    }

    // v1.0.4.0 (roadmap S-1, S-2; owner decision D-2: guarded edits only): the selected player's main inventory from the world
    // save, one stack removed or one added, each previewed, confirmed (MainWindow's Click handlers) and applied with a fresh,
    // checked safety backup while the server is stopped.
    public ObservableCollection<InventorySlotDto> InventorySlots { get; } = [];
    private InventorySlotDto? _selectedInventorySlot;
    public InventorySlotDto? SelectedInventorySlot { get => _selectedInventorySlot; set => SetField(ref _selectedInventorySlot, value); }
    private string _inventoryStatus = string.Empty;
    public string InventoryStatus { get => _inventoryStatus; private set => SetField(ref _inventoryStatus, value); }
    public ICommand LoadInventoryCommand { get; }

    private async Task LoadInventoryAsync()
    {
        if (Session.SelectedProfile is null || SelectedPlayerRecord?.PlayerId is not { Length: > 0 } playerId) { InventoryStatus = "Select a player first."; return; }
        Session.IsBusy = true;
        InventoryStatus = "Reading the world save…";
        try
        {
            var view = await Session.Api.GetInventoryAsync(Session.SelectedProfile, playerId, Session.BearerToken);
            InventorySlots.Clear();
            foreach (var slot in view.Slots) InventorySlots.Add(slot);
            InventoryStatus = view.Detail;
        }
        catch (Exception ex) { InventoryStatus = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    public async Task<InventoryEditPreviewDto?> PreviewInventoryEditAsync(string action)
    {
        if (Session.SelectedProfile is null || SelectedPlayerRecord?.PlayerId is not { Length: > 0 } playerId) { InventoryStatus = "Select a player first."; return null; }
        var request = new InventoryEditRequestDto { PlayerId = playerId, Action = action };
        if (action == "remove")
        {
            if (SelectedInventorySlot is not { } slot) { InventoryStatus = "Select a stack in the inventory first."; return null; }
            request.ItemId = slot.ItemId; request.SlotIndex = slot.SlotIndex; request.Count = slot.Count;
        }
        else
        {
            if (SelectedGameId?.Id is not { Length: > 0 } id) { InventoryStatus = "Pick an item in the list above first."; return null; }
            request.ItemId = id;
            request.Count = int.TryParse(GameIdAmountText, out var n) && n > 0 ? n : 1;
        }
        Session.IsBusy = true;
        try
        {
            var preview = await Session.Api.PreviewInventoryEditAsync(Session.SelectedProfile, request, Session.BearerToken);
            if (!preview.CanApply) InventoryStatus = string.Join(" ", preview.Findings);
            return preview;
        }
        catch (Exception ex) { InventoryStatus = ex.Message; return null; }
        finally { Session.IsBusy = false; }
    }

    public async Task ApplyInventoryEditAsync(string token)
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ApplyInventoryEditAsync(Session.SelectedProfile, token, Session.BearerToken);
            InventoryStatus = result.Message;
        }
        catch (Exception ex) { InventoryStatus = ex.Message; }
        finally { Session.IsBusy = false; }
        await LoadInventoryAsync();
    }
}
