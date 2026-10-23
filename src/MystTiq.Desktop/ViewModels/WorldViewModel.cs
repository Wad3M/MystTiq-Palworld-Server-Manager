// MystTiq v1.1.5.0: file reviewed for this release (2026-10-23).
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
/// v1.0.8.3 (roadmap C-3): the world pages -- the Inspector (players, guilds, bases and Pals from the world save, the
/// Pal editor), the Bases and Guilds pages and their operations, character migration and world transactions -- moved
/// out of MainWindowViewModel unchanged. The window shows them as vm.World; zooming to a base goes through vm.Map.
/// </summary>
public sealed class WorldViewModel : PageViewModel
{
    private MapViewModel Map { get; }
    private PlayersViewModel Players { get; }
    private Func<Task> RefreshOperationsAsync { get; }
    private Func<Task> ValidateActiveWorldAsync { get; }
    private Func<Task> RefreshPalsAsync { get; }
    private Func<Task> RefreshPlayersPageAsync { get; }
    private Func<Task> RefreshWorldExplorerAsync { get; }
    private Func<Task> RefreshPlayerGuildExplorerAsync { get; }
    private Func<Task> OpenSelectedGuildLeaderAsync { get; }

    public WorldViewModel(SessionContext session, MapViewModel map, PlayersViewModel players, Func<Task> refreshOperations, Func<Task> validateActiveWorld, Func<Task> refreshPals, Func<Task> refreshPlayersPage, Func<Task> refreshWorldExplorer, Func<Task> refreshPlayerGuildExplorer, Func<Task> openSelectedGuildLeader) : base(session, NavigationPage.Inspector)
    {
        Map = map;
        Players = players;
        RefreshOperationsAsync = refreshOperations;
        ValidateActiveWorldAsync = validateActiveWorld;
        RefreshPalsAsync = refreshPals;
        RefreshPlayersPageAsync = refreshPlayersPage;
        RefreshWorldExplorerAsync = refreshWorldExplorer;
        RefreshPlayerGuildExplorerAsync = refreshPlayerGuildExplorer;
        OpenSelectedGuildLeaderAsync = openSelectedGuildLeader;
        PreviewCharacterMigrationCommand = new AsyncCommand(PreviewCharacterMigrationAsync, () => !Session.IsBusy && MigrationSourcePlayer is not null && MigrationDestinationPlayer is not null);
        ApplyCharacterMigrationCommand = new AsyncCommand(ApplyCharacterMigrationAsync, () => !Session.IsBusy && MigrationPreview is { CanApply: true });
        ApplyWorldTransactionCommand = new AsyncCommand(ApplyWorldTransactionAsync, () => !Session.IsBusy && WorldTransactionConfirmed && !string.IsNullOrWhiteSpace(WorldPreviewToken));
        OpenSelectedGuildLeaderCommand = new AsyncCommand(OpenSelectedGuildLeaderAsync, () => !Session.IsBusy && SelectedExplorerGuild is not null && !string.IsNullOrWhiteSpace(SelectedExplorerGuild.LeaderPlayerId));
        PreviewGuildOwnershipCommand = new AsyncCommand(PreviewGuildOwnershipAsync, () => !Session.IsBusy && SelectedExplorerGuild is not null && !string.IsNullOrWhiteSpace(GuildOperationPlayerId));
        ApplyGuildOwnershipCommand = new AsyncCommand(ApplyGuildOwnershipAsync, () => !Session.IsBusy && GuildOperationConfirmed && !string.IsNullOrWhiteSpace(GuildOperationPreviewToken));
        PreviewPalEditCommand = new AsyncCommand(PreviewPalEditAsync, () => !Session.IsBusy && SelectedExplorerPal is not null);
        ApplyPalEditCommand = new AsyncCommand(ApplyPalEditAsync, () => !Session.IsBusy && PalEditConfirmed && !string.IsNullOrWhiteSpace(PalEditPreviewToken));
        PreviewBaseTransferCommand = new AsyncCommand(PreviewBaseTransferAsync, () => !Session.IsBusy && SelectedExplorerBase is not null && !string.IsNullOrWhiteSpace(BaseTransferTargetGuildId));
        ApplyBaseTransferCommand = new AsyncCommand(ApplyBaseTransferAsync, () => !Session.IsBusy && BaseTransferConfirmed && !string.IsNullOrWhiteSpace(BaseTransferPreviewToken));
        PreviewBaseRecoveryCommand = new AsyncCommand(PreviewBaseRecoveryAsync, () => !Session.IsBusy && SelectedExplorerBase is not null);
        ApplyBaseRecoveryCommand = new AsyncCommand(ApplyBaseRecoveryAsync, () => !Session.IsBusy && BaseRecoveryConfirmed && !string.IsNullOrWhiteSpace(BaseRecoveryPreviewToken));
        ZoomToBaseCommand = new RelayCommand<string>(ZoomToBase);
        TogglePalEditorCommand = new RelayCommand(() => IsPalEditorExpanded = !IsPalEditorExpanded);
    }

    private string _worldExplorerState = "Not loaded";

    private string _worldExplorerDetail = "Connect to inspect Palworld save data.";

    private string _worldInspectorPlayerCountText = "—";

    private string _worldInspectorGuildCountText = "—";

    private string _worldInspectorBaseCountText = "—";

    private string _worldTransactionState = "Analyze an archive to create a reviewable plan.";

    private string _worldTransactionMode = "world-import";

    private bool _worldTransactionConfirmed;

    private string _playerGuildState = "Not loaded";

    private string _playerGuildDetail = "Connect to explore player and guild identities.";

    private string _guildRecordCountText = "—";

    private PlayerExplorerItemDto? _selectedExplorerPlayer;

    private PlayerExplorerItemDto? _migrationSourcePlayer;

    private PlayerExplorerItemDto? _migrationDestinationPlayer;

    private string _migrationState = "Pick a source and destination player, then Preview.";

    private CharacterMigrationPreviewDto? _migrationPreview;

    private string _migrationDisposition = "Keep";

    private GuildExplorerItemDto? _selectedExplorerGuild;

    private BaseExplorerItemDto? _selectedExplorerBase;

    private string _guildSearchText = string.Empty;

    private string _selectedGuildStatusFilter = "All Guilds";

    private string _baseSearchText = string.Empty;

    private string _selectedBaseStatusFilter = "All Bases";

    private string _guildVisibleCountText = "0 visible";

    private string _baseVisibleCountText = "0 visible";

    private string _guildOperationType = "Claim Orphaned Guild";

    private string _guildOperationPlayerId = string.Empty;

    private string _guildOperationPreviewToken = string.Empty;

    private bool _guildOperationConfirmed;

    private string _guildOperationStatusText = "Select a guild, choose an operation, and enter the target player's ID.";

    private PalInstanceDto? _selectedExplorerPal;

    private string _palEditNickName = string.Empty;

    private int _palEditLevel;

    private int _palEditRank;

    private int _palEditTalentHp;

    private int _palEditTalentShot;

    private int _palEditTalentDefense;

    private string _palEditGender = "Male";

    private bool _palEditIsRarePal;

    private string _palEditPreviewToken = string.Empty;

    private bool _palEditConfirmed;

    private string _palEditStatusText = "Refresh Pals, select one, and adjust its fields below.";

    private string _baseTransferTargetGuildId = string.Empty;

    private string _baseTransferPreviewToken = string.Empty;

    private bool _baseTransferConfirmed;

    private string _baseTransferStatusText = "Select a base above and enter the target guild's ID.";

    private string _baseRecoveryPreviewToken = string.Empty;

    private bool _baseRecoveryConfirmed;

    private string _baseRecoveryStatusText = "Select a base above, then preview to see how many owned records will be removed.";

    // v0.7.96.0: open by default. It was collapsed in v0.7.5.0 to save vertical space, but a
    // collapsed card is exactly why "where are the players and bases" read as missing.
    private bool _isPalEditorExpanded;

    public ObservableCollection<string> WorldTransactionPlanSteps { get; } = [];

    public ObservableCollection<WorldTransactionJournalDto> WorldTransactionHistory { get; } = [];

    public ObservableCollection<PlayerExplorerItemDto> ExplorerPlayers { get; } = [];

    public ObservableCollection<GuildExplorerItemDto> ExplorerGuilds { get; } = [];

    public ObservableCollection<GuildExplorerItemDto> FilteredExplorerGuilds { get; } = [];

    public ObservableCollection<BaseExplorerItemDto> ExplorerBases { get; } = [];

    public ObservableCollection<BaseExplorerItemDto> FilteredExplorerBases { get; } = [];

    public ObservableCollection<string> PlayerGuildWarnings { get; } = [];

    public IReadOnlyList<string> GuildStatusFilters { get; } = ["All Guilds", "Healthy", "Needs Review"];

    public IReadOnlyList<string> BaseStatusFilters { get; } = ["All Bases", "Healthy Owner", "Needs Review"];

    public PlayerExplorerItemDto? MigrationSourcePlayer { get => _migrationSourcePlayer; set { if (SetField(ref _migrationSourcePlayer, value)) (PreviewCharacterMigrationCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }

    public PlayerExplorerItemDto? MigrationDestinationPlayer { get => _migrationDestinationPlayer; set { if (SetField(ref _migrationDestinationPlayer, value)) (PreviewCharacterMigrationCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }

    public string MigrationState { get => _migrationState; private set => SetField(ref _migrationState, value); }

    public CharacterMigrationPreviewDto? MigrationPreview { get => _migrationPreview; private set { if (SetField(ref _migrationPreview, value)) (ApplyCharacterMigrationCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }

    public string MigrationDisposition { get => _migrationDisposition; set => SetField(ref _migrationDisposition, value); }

    public string WorldExplorerState { get => _worldExplorerState; internal set => SetField(ref _worldExplorerState, value); }

    public string WorldExplorerDetail { get => _worldExplorerDetail; internal set => SetField(ref _worldExplorerDetail, value); }

    public string WorldInspectorPlayerCountText { get => _worldInspectorPlayerCountText; internal set => SetField(ref _worldInspectorPlayerCountText, value); }

    public string WorldInspectorGuildCountText { get => _worldInspectorGuildCountText; internal set => SetField(ref _worldInspectorGuildCountText, value); }

    public string WorldInspectorBaseCountText { get => _worldInspectorBaseCountText; internal set => SetField(ref _worldInspectorBaseCountText, value); }

    public string WorldTransactionState { get => _worldTransactionState; internal set => SetField(ref _worldTransactionState, value); }

    public IReadOnlyList<string> WorldTransactionModes { get; } = ["world-import", "player-recovery"];

    public string WorldTransactionMode { get => _worldTransactionMode; set => SetField(ref _worldTransactionMode, value ?? "world-import"); }

    public bool WorldTransactionConfirmed { get => _worldTransactionConfirmed; set { if (SetField(ref _worldTransactionConfirmed, value)) (ApplyWorldTransactionCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }

    public string PlayerGuildState { get => _playerGuildState; internal set => SetField(ref _playerGuildState, value); }

    public string PlayerGuildDetail { get => _playerGuildDetail; internal set => SetField(ref _playerGuildDetail, value); }

    public string GuildRecordCountText { get => _guildRecordCountText; internal set => SetField(ref _guildRecordCountText, value); }

    public PlayerExplorerItemDto? SelectedExplorerPlayer
    {
        get => _selectedExplorerPlayer;
        set
        {
            if (!SetField(ref _selectedExplorerPlayer, value)) return;
            (Players.KickSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.BanSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.WhisperSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.PromoteSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (Players.GiveItemSelectedPlayerCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    public GuildExplorerItemDto? SelectedExplorerGuild
    {
        get => _selectedExplorerGuild;
        set
        {
            if (!SetField(ref _selectedExplorerGuild, value)) return;
            (OpenSelectedGuildLeaderCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            (PreviewGuildOwnershipCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            GuildOperationPreviewToken = string.Empty;
            GuildOperationConfirmed = false;
            GuildOperationStatusText = "Select a guild, choose an operation, and enter the target player's ID.";
        }
    }

    public PalInstanceDto? SelectedExplorerPal
    {
        get => _selectedExplorerPal;
        set
        {
            if (!SetField(ref _selectedExplorerPal, value)) return;
            (PreviewPalEditCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            PalEditPreviewToken = string.Empty;
            PalEditConfirmed = false;
            if (value is not null)
            {
                PalEditNickName = value.NickName;
                PalEditLevel = value.Level;
                PalEditRank = value.Rank;
                PalEditTalentHp = value.TalentHp;
                PalEditTalentShot = value.TalentShot;
                PalEditTalentDefense = value.TalentDefense;
                PalEditGender = value.Gender is "Male" or "Female" ? value.Gender : "Male";
                PalEditIsRarePal = value.IsRarePal;
                PalEditStatusText = $"Editing {value.DisplayName} (owned by {value.OwnerDisplay}). Adjust fields, then Preview.";
            }
            else
            {
                PalEditStatusText = "Refresh Pals, select one, and adjust its fields below.";
            }
        }
    }

    public BaseExplorerItemDto? SelectedExplorerBase
    {
        get => _selectedExplorerBase;
        set
        {
            if (!SetField(ref _selectedExplorerBase, value)) return;
            (PreviewBaseTransferCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            BaseTransferPreviewToken = string.Empty;
            BaseTransferConfirmed = false;
            BaseTransferStatusText = "Select a base above and enter the target guild's ID.";
            (PreviewBaseRecoveryCommand as AsyncCommand)?.RaiseCanExecuteChanged();
            BaseRecoveryPreviewToken = string.Empty;
            BaseRecoveryConfirmed = false;
            BaseRecoveryStatusText = "Select a base above, then preview to see how many owned records will be removed.";
        }
    }

    public string GuildSearchText { get => _guildSearchText; set { if (SetField(ref _guildSearchText, value ?? string.Empty)) ApplyGuildFilters(); } }

    public string SelectedGuildStatusFilter { get => _selectedGuildStatusFilter; set { if (SetField(ref _selectedGuildStatusFilter, value ?? "All Guilds")) ApplyGuildFilters(); } }

    public string BaseSearchText { get => _baseSearchText; set { if (SetField(ref _baseSearchText, value ?? string.Empty)) ApplyBaseFilters(); } }

    public string SelectedBaseStatusFilter { get => _selectedBaseStatusFilter; set { if (SetField(ref _selectedBaseStatusFilter, value ?? "All Bases")) ApplyBaseFilters(); } }

    public string GuildVisibleCountText { get => _guildVisibleCountText; private set => SetField(ref _guildVisibleCountText, value); }

    public string BaseVisibleCountText { get => _baseVisibleCountText; private set => SetField(ref _baseVisibleCountText, value); }

    public IReadOnlyList<string> GuildOperationTypes { get; } = ["Claim Orphaned Guild", "Transfer Leadership", "Add Player to Guild", "Remove Broken Member"];

    public string GuildOperationType
    {
        get => _guildOperationType;
        set
        {
            if (!SetField(ref _guildOperationType, value ?? "Claim Orphaned Guild")) return;
            GuildOperationPreviewToken = string.Empty;
            GuildOperationConfirmed = false;
            GuildOperationStatusText = "Select a guild, choose an operation, and enter the target player's ID.";
        }
    }

    public string GuildOperationPlayerId
    {
        get => _guildOperationPlayerId;
        set { if (SetField(ref _guildOperationPlayerId, value ?? string.Empty)) (PreviewGuildOwnershipCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }

    public string GuildOperationPreviewToken
    {
        get => _guildOperationPreviewToken;
        private set { if (SetField(ref _guildOperationPreviewToken, value)) (ApplyGuildOwnershipCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }

    public bool GuildOperationConfirmed
    {
        get => _guildOperationConfirmed;
        set { if (SetField(ref _guildOperationConfirmed, value)) (ApplyGuildOwnershipCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }

    public string GuildOperationStatusText { get => _guildOperationStatusText; private set => SetField(ref _guildOperationStatusText, value); }

    private static string GuildOperationApiName(string displayName) => displayName switch
    {
        "Transfer Leadership" => "transfer-leadership",
        "Add Player to Guild" => "add-player",
        "Remove Broken Member" => "remove-broken-member",
        _ => "claim"
    };

    public ObservableCollection<PalInstanceDto> ExplorerPals { get; } = [];

    public string PalEditNickName { get => _palEditNickName; set => SetField(ref _palEditNickName, value ?? string.Empty); }

    public int PalEditLevel { get => _palEditLevel; set => SetField(ref _palEditLevel, value); }

    public int PalEditRank { get => _palEditRank; set => SetField(ref _palEditRank, value); }

    public int PalEditTalentHp { get => _palEditTalentHp; set => SetField(ref _palEditTalentHp, value); }

    public int PalEditTalentShot { get => _palEditTalentShot; set => SetField(ref _palEditTalentShot, value); }

    public int PalEditTalentDefense { get => _palEditTalentDefense; set => SetField(ref _palEditTalentDefense, value); }

    public string PalEditGender { get => _palEditGender; set => SetField(ref _palEditGender, value ?? "Male"); }

    public bool PalEditIsRarePal { get => _palEditIsRarePal; set => SetField(ref _palEditIsRarePal, value); }

    public string PalEditPreviewToken
    {
        get => _palEditPreviewToken;
        private set { if (SetField(ref _palEditPreviewToken, value)) (ApplyPalEditCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }

    public bool PalEditConfirmed
    {
        get => _palEditConfirmed;
        set { if (SetField(ref _palEditConfirmed, value)) (ApplyPalEditCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }

    public string PalEditStatusText { get => _palEditStatusText; internal set => SetField(ref _palEditStatusText, value); }

    public string BaseTransferTargetGuildId
    {
        get => _baseTransferTargetGuildId;
        set { if (SetField(ref _baseTransferTargetGuildId, value ?? string.Empty)) (PreviewBaseTransferCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }

    public string BaseTransferPreviewToken
    {
        get => _baseTransferPreviewToken;
        private set { if (SetField(ref _baseTransferPreviewToken, value)) (ApplyBaseTransferCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }

    public bool BaseTransferConfirmed
    {
        get => _baseTransferConfirmed;
        set { if (SetField(ref _baseTransferConfirmed, value)) (ApplyBaseTransferCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }

    public string BaseTransferStatusText { get => _baseTransferStatusText; private set => SetField(ref _baseTransferStatusText, value); }

    public string BaseRecoveryPreviewToken
    {
        get => _baseRecoveryPreviewToken;
        private set { if (SetField(ref _baseRecoveryPreviewToken, value)) (ApplyBaseRecoveryCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }

    public bool BaseRecoveryConfirmed
    {
        get => _baseRecoveryConfirmed;
        set { if (SetField(ref _baseRecoveryConfirmed, value)) (ApplyBaseRecoveryCommand as AsyncCommand)?.RaiseCanExecuteChanged(); }
    }

    public string BaseRecoveryStatusText { get => _baseRecoveryStatusText; private set => SetField(ref _baseRecoveryStatusText, value); }

    public ICommand PreviewCharacterMigrationCommand { get; }

    public ICommand ApplyCharacterMigrationCommand { get; }

    public ICommand ApplyWorldTransactionCommand { get; }

    public ICommand PreviewGuildOwnershipCommand { get; }

    public ICommand ApplyGuildOwnershipCommand { get; }

    public ICommand PreviewPalEditCommand { get; }

    public ICommand ApplyPalEditCommand { get; }

    public ICommand PreviewBaseTransferCommand { get; }

    public ICommand ApplyBaseTransferCommand { get; }

    public ICommand PreviewBaseRecoveryCommand { get; }

    public ICommand ApplyBaseRecoveryCommand { get; }

    public ICommand TogglePalEditorCommand { get; }

    public bool IsPalEditorExpanded { get => _isPalEditorExpanded; private set => SetField(ref _isPalEditorExpanded, value); }

    private async Task PreviewCharacterMigrationAsync()
    {
        if (MigrationSourcePlayer is null || MigrationDestinationPlayer is null) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { MigrationState = ex.Message; return; }

        Session.IsBusy = true;
        MigrationState = "Previewing…";
        try
        {
            var preview = await Session.Api.PreviewCharacterMigrationAsync(profile,
                new CharacterMigrationPreviewRequestDto(MigrationSourcePlayer.PlayerId, MigrationDestinationPlayer.PlayerId), Session.BearerToken);
            MigrationPreview = preview;
            MigrationState = string.Join(" ", preview.Findings);
        }
        catch (Exception ex) { MigrationState = ex.Message; MigrationPreview = null; }
        finally { Session.IsBusy = false; }
    }

    private async Task ApplyCharacterMigrationAsync()
    {
        if (MigrationPreview is not { CanApply: true }) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { MigrationState = ex.Message; return; }

        Session.IsBusy = true;
        MigrationState = "Applying migration…";
        try
        {
            var result = await Session.Api.ApplyCharacterMigrationAsync(profile,
                new CharacterMigrationApplyRequestDto(MigrationPreview.PreviewToken, true), Session.BearerToken);
            MigrationState = result.Message;
            if (result.Success && result.SourcePlayerId is not null)
            {
                // Reset is intentionally still selectable (not decorative-disabled) so the server's
                // own real "not yet supported" rejection is what the operator sees, rather than a
                // client-side substitution silently changing their choice.
                var dispositionText = MigrationDisposition.Split(' ')[0];
                var dispositionResult = await Session.Api.DisposeSourceCharacterAsync(profile, result.SourcePlayerId,
                    new CharacterDispositionRequestDto(dispositionText), Session.BearerToken);
                MigrationState = $"{result.Message} {dispositionResult.Message}";
            }
            MigrationPreview = null;
            await RefreshPlayersPageAsync();
        }
        catch (Exception ex) { MigrationState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    public ICommand ZoomToBaseCommand { get; private set; } = null!;

    internal void ZoomToBase(string? baseId)
    {
        var point = Map.BaseMapPoints.FirstOrDefault(b => string.Equals(b.BaseId, baseId, StringComparison.OrdinalIgnoreCase));
        if (point is not null) Map.ZoomToMapPoint(point.UnzoomedX, point.UnzoomedY);
    }

    // v0.7.76.0: right-click quick actions for Bases/Guilds -- these call the exact same, already-
    // shipped and already-working API endpoints the existing in-page Base Ownership Transfer/Base
    // Recovery/Guild Ownership Operations cards use (see MainWindow.axaml's own "Applies to the
    // selected base/guild above" cards), just with their own populated dropdown pickers instead of
    // hand-typed IDs, and driven by code-behind's dialog sequence instead of the in-page form.
    // Deliberately parallel methods rather than reusing the cards' own bound properties (e.g.
    // BaseTransferTargetGuildId), to avoid coupling this flow's state to whatever the user might
    // currently have typed into that card -- each writes its own result into the same status-text
    // properties those cards already display, so both stay in sync regardless of which path ran.
    public async Task<BaseOwnershipPreviewDto?> PreviewTransferBaseAsync(string baseId, string targetGuildId)
    {
        if (Session.SelectedProfile is null) return null;
        Session.IsBusy = true;
        BaseTransferStatusText = "Previewing…";
        try
        {
            var preview = await Session.Api.PreviewBaseOwnershipTransferAsync(Session.SelectedProfile, baseId, targetGuildId, Session.BearerToken);
            if (!preview.CanApply) BaseTransferStatusText = string.Join(" ", preview.Findings);
            return preview;
        }
        catch (Exception ex) { BaseTransferStatusText = ex.Message; return null; }
        finally { Session.IsBusy = false; }
    }

    public async Task<bool> ApplyTransferBaseAsync(string previewToken)
    {
        if (Session.SelectedProfile is null) return false;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ApplyBaseOwnershipTransferAsync(Session.SelectedProfile, previewToken, true, Session.BearerToken);
            BaseTransferStatusText = result.Message + (string.IsNullOrWhiteSpace(result.SafetyBackup) ? string.Empty : $" Safety backup: {result.SafetyBackup}");
            if (result.Success) await RefreshPlayerGuildExplorerAsync();
            await RefreshOperationsAsync();
            return result.Success;
        }
        catch (Exception ex) { BaseTransferStatusText = ex.Message; return false; }
        finally { Session.IsBusy = false; }
    }

    public async Task<BaseRecoveryPreviewDto?> PreviewWipeBaseAsync(string baseId)
    {
        if (Session.SelectedProfile is null) return null;
        Session.IsBusy = true;
        BaseRecoveryStatusText = "Previewing…";
        try
        {
            var preview = await Session.Api.PreviewBaseRecoveryAsync(Session.SelectedProfile, baseId, Session.BearerToken);
            if (!preview.CanApply) BaseRecoveryStatusText = string.Join(" ", preview.Findings);
            return preview;
        }
        catch (Exception ex) { BaseRecoveryStatusText = ex.Message; return null; }
        finally { Session.IsBusy = false; }
    }

    public async Task<bool> ApplyWipeBaseAsync(string previewToken)
    {
        if (Session.SelectedProfile is null) return false;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ApplyBaseRecoveryAsync(Session.SelectedProfile, previewToken, true, Session.BearerToken);
            BaseRecoveryStatusText = result.Message + (string.IsNullOrWhiteSpace(result.SafetyBackup) ? string.Empty : $" Safety backup: {result.SafetyBackup}");
            if (result.Success) await RefreshPlayerGuildExplorerAsync();
            await RefreshOperationsAsync();
            return result.Success;
        }
        catch (Exception ex) { BaseRecoveryStatusText = ex.Message; return false; }
        finally { Session.IsBusy = false; }
    }

    public async Task<GuildOwnershipPreviewDto?> PreviewGuildOperationForRowAsync(string operationApiName, string guildId, string playerId)
    {
        if (Session.SelectedProfile is null) return null;
        Session.IsBusy = true;
        GuildOperationStatusText = "Previewing…";
        try
        {
            var preview = await Session.Api.PreviewGuildOwnershipAsync(Session.SelectedProfile, operationApiName, guildId, playerId, Session.BearerToken);
            if (!preview.CanApply) GuildOperationStatusText = string.Join(" ", preview.Findings);
            return preview;
        }
        catch (Exception ex) { GuildOperationStatusText = ex.Message; return null; }
        finally { Session.IsBusy = false; }
    }

    public async Task<bool> ApplyGuildOperationForRowAsync(string previewToken)
    {
        if (Session.SelectedProfile is null) return false;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ApplyGuildOwnershipAsync(Session.SelectedProfile, previewToken, true, Session.BearerToken);
            GuildOperationStatusText = result.Message + (string.IsNullOrWhiteSpace(result.SafetyBackup) ? string.Empty : $" Safety backup: {result.SafetyBackup}");
            if (result.Success) await RefreshPlayerGuildExplorerAsync();
            await RefreshOperationsAsync();
            return result.Success;
        }
        catch (Exception ex) { GuildOperationStatusText = ex.Message; return false; }
        finally { Session.IsBusy = false; }
    }

    internal async Task RefreshWorldTransactionsAsync()
    {
        await ValidateActiveWorldAsync();
        if (Session.SelectedProfile is null) return;
        try
        {
            var history = await Session.Api.GetWorldTransactionsAsync(Session.SelectedProfile, Session.BearerToken);
            WorldTransactionHistory.Clear();
            foreach (var item in history) WorldTransactionHistory.Add(item);
        }
        catch (Exception ex) { WorldTransactionState = "Transaction history: " + ex.Message; }
        await RefreshOperationsAsync();
    }

    internal async Task ApplyWorldTransactionAsync()
    {
        if (string.IsNullOrWhiteSpace(WorldPreviewToken)) return;
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { WorldTransactionState = ex.Message; return; }
        Session.BusyReason = "Applying world transaction…";
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ApplyWorldTransactionAsync(profile, WorldPreviewToken, WorldTransactionConfirmed, Session.BearerToken);
            WorldTransactionState = result.Message + (string.IsNullOrWhiteSpace(result.SafetyBackup) ? string.Empty : $" Safety backup: {result.SafetyBackup}");
            WorldPreviewToken = string.Empty;
            WorldTransactionConfirmed = false;
            WorldTransactionPlanSteps.Clear();
            await RefreshWorldExplorerAsync();
            if (Session.SelectedProfile is not null)
            {
                var history = await Session.Api.GetWorldTransactionsAsync(Session.SelectedProfile, Session.BearerToken);
                WorldTransactionHistory.Clear(); foreach (var item in history) WorldTransactionHistory.Add(item);
                await RefreshOperationsAsync();
            }
        }
        catch (Exception ex) { WorldTransactionState = ex.Message; }
        finally { Session.IsBusy = false; Session.BusyReason = null; }
    }

    private async Task PreviewGuildOwnershipAsync()
    {
        if (Session.SelectedProfile is null || SelectedExplorerGuild is null || string.IsNullOrWhiteSpace(GuildOperationPlayerId)) return;
        Session.IsBusy = true;
        GuildOperationStatusText = "Previewing…";
        try
        {
            var preview = await Session.Api.PreviewGuildOwnershipAsync(Session.SelectedProfile, GuildOperationApiName(GuildOperationType), SelectedExplorerGuild.GuildId, GuildOperationPlayerId, Session.BearerToken);
            GuildOperationPreviewToken = preview.CanApply ? preview.PreviewToken : string.Empty;
            GuildOperationConfirmed = false;
            GuildOperationStatusText = string.Join(" ", preview.Findings) +
                (preview.CanApply ? $" Preview expires {preview.ExpiresUtc.ToLocalTime():t}." : string.Empty);
        }
        catch (Exception ex) { GuildOperationPreviewToken = string.Empty; GuildOperationStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task ApplyGuildOwnershipAsync()
    {
        if (Session.SelectedProfile is null || string.IsNullOrWhiteSpace(GuildOperationPreviewToken)) return;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ApplyGuildOwnershipAsync(Session.SelectedProfile, GuildOperationPreviewToken, GuildOperationConfirmed, Session.BearerToken);
            GuildOperationStatusText = result.Message + (string.IsNullOrWhiteSpace(result.SafetyBackup) ? string.Empty : $" Safety backup: {result.SafetyBackup}");
            GuildOperationPreviewToken = string.Empty;
            GuildOperationConfirmed = false;
            GuildOperationPlayerId = string.Empty;
            if (result.Success) await RefreshPlayerGuildExplorerAsync();
            await RefreshOperationsAsync();
        }
        catch (Exception ex) { GuildOperationStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task PreviewPalEditAsync()
    {
        if (Session.SelectedProfile is null || SelectedExplorerPal is null) return;
        Session.IsBusy = true;
        PalEditStatusText = "Previewing…";
        try
        {
            var changes = new PalEditFieldChangesDto(PalEditNickName, PalEditLevel, PalEditRank, PalEditTalentHp, PalEditTalentShot, PalEditTalentDefense, PalEditGender, PalEditIsRarePal);
            var preview = await Session.Api.PreviewPalEditAsync(Session.SelectedProfile, SelectedExplorerPal.InstanceId, changes, Session.BearerToken);
            PalEditPreviewToken = preview.CanApply ? preview.PreviewToken : string.Empty;
            PalEditConfirmed = false;
            PalEditStatusText = string.Join(" ", preview.Findings) +
                (preview.CanApply ? $" Preview expires {preview.ExpiresUtc.ToLocalTime():t}." : string.Empty);
        }
        catch (Exception ex) { PalEditPreviewToken = string.Empty; PalEditStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task ApplyPalEditAsync()
    {
        if (Session.SelectedProfile is null || string.IsNullOrWhiteSpace(PalEditPreviewToken)) return;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ApplyPalEditAsync(Session.SelectedProfile, PalEditPreviewToken, PalEditConfirmed, Session.BearerToken);
            PalEditStatusText = result.Message + (string.IsNullOrWhiteSpace(result.SafetyBackup) ? string.Empty : $" Safety backup: {result.SafetyBackup}");
            PalEditPreviewToken = string.Empty;
            PalEditConfirmed = false;
            if (result.Success) await RefreshPalsAsync();
            await RefreshOperationsAsync();
        }
        catch (Exception ex) { PalEditStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task PreviewBaseTransferAsync()
    {
        if (Session.SelectedProfile is null || SelectedExplorerBase is null || string.IsNullOrWhiteSpace(BaseTransferTargetGuildId)) return;
        Session.IsBusy = true;
        BaseTransferStatusText = "Previewing…";
        try
        {
            var preview = await Session.Api.PreviewBaseOwnershipTransferAsync(Session.SelectedProfile, SelectedExplorerBase.BaseId, BaseTransferTargetGuildId, Session.BearerToken);
            BaseTransferPreviewToken = preview.CanApply ? preview.PreviewToken : string.Empty;
            BaseTransferConfirmed = false;
            BaseTransferStatusText = string.Join(" ", preview.Findings) +
                (preview.CanApply ? $" Preview expires {preview.ExpiresUtc.ToLocalTime():t}." : string.Empty);
        }
        catch (Exception ex) { BaseTransferPreviewToken = string.Empty; BaseTransferStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task ApplyBaseTransferAsync()
    {
        if (Session.SelectedProfile is null || string.IsNullOrWhiteSpace(BaseTransferPreviewToken)) return;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ApplyBaseOwnershipTransferAsync(Session.SelectedProfile, BaseTransferPreviewToken, BaseTransferConfirmed, Session.BearerToken);
            BaseTransferStatusText = result.Message + (string.IsNullOrWhiteSpace(result.SafetyBackup) ? string.Empty : $" Safety backup: {result.SafetyBackup}");
            BaseTransferPreviewToken = string.Empty;
            BaseTransferConfirmed = false;
            BaseTransferTargetGuildId = string.Empty;
            if (result.Success) await RefreshPlayerGuildExplorerAsync();
            await RefreshOperationsAsync();
        }
        catch (Exception ex) { BaseTransferStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task PreviewBaseRecoveryAsync()
    {
        if (Session.SelectedProfile is null || SelectedExplorerBase is null) return;
        Session.IsBusy = true;
        BaseRecoveryStatusText = "Previewing…";
        try
        {
            var preview = await Session.Api.PreviewBaseRecoveryAsync(Session.SelectedProfile, SelectedExplorerBase.BaseId, Session.BearerToken);
            BaseRecoveryPreviewToken = preview.CanApply ? preview.PreviewToken : string.Empty;
            BaseRecoveryConfirmed = false;
            BaseRecoveryStatusText = string.Join(" ", preview.Findings) +
                (preview.CanApply ? $" Preview expires {preview.ExpiresUtc.ToLocalTime():t}." : string.Empty);
        }
        catch (Exception ex) { BaseRecoveryPreviewToken = string.Empty; BaseRecoveryStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task ApplyBaseRecoveryAsync()
    {
        if (Session.SelectedProfile is null || string.IsNullOrWhiteSpace(BaseRecoveryPreviewToken)) return;
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.ApplyBaseRecoveryAsync(Session.SelectedProfile, BaseRecoveryPreviewToken, BaseRecoveryConfirmed, Session.BearerToken);
            BaseRecoveryStatusText = result.Message + (string.IsNullOrWhiteSpace(result.SafetyBackup) ? string.Empty : $" Safety backup: {result.SafetyBackup}");
            BaseRecoveryPreviewToken = string.Empty;
            BaseRecoveryConfirmed = false;
            if (result.Success) await RefreshPlayerGuildExplorerAsync();
            await RefreshOperationsAsync();
        }
        catch (Exception ex) { BaseRecoveryStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    internal void ApplyGuildFilters()
    {
        var selectedId = SelectedExplorerGuild?.GuildId;
        var search = GuildSearchText.Trim();
        var filtered = ExplorerGuilds.Where(guild =>
        {
            if (!string.IsNullOrWhiteSpace(search) &&
                !guild.GuildId.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !guild.GuildName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !guild.LeaderPlayerId.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !guild.LeaderName.Contains(search, StringComparison.OrdinalIgnoreCase)) return false;
            if (SelectedGuildStatusFilter == "Healthy" && !guild.Health.Equals("Healthy", StringComparison.OrdinalIgnoreCase)) return false;
            if (SelectedGuildStatusFilter == "Needs Review" && guild.Health.Equals("Healthy", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }).ToArray();
        FilteredExplorerGuilds.Clear();
        foreach (var guild in filtered) FilteredExplorerGuilds.Add(guild);
        GuildVisibleCountText = $"{filtered.Length} visible / {ExplorerGuilds.Count} guilds";
        if (!string.IsNullOrWhiteSpace(selectedId))
            SelectedExplorerGuild = filtered.FirstOrDefault(x => string.Equals(x.GuildId, selectedId, StringComparison.OrdinalIgnoreCase));
        SelectedExplorerGuild ??= filtered.FirstOrDefault();
    }

    internal void ApplyBaseFilters()
    {
        var selectedId = SelectedExplorerBase?.BaseId;
        var search = BaseSearchText.Trim();
        var filtered = ExplorerBases.Where(item =>
        {
            if (!string.IsNullOrWhiteSpace(search) &&
                !item.BaseId.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !item.GuildId.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !item.GuildName.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !item.LeaderName.Contains(search, StringComparison.OrdinalIgnoreCase)) return false;
            if (SelectedBaseStatusFilter == "Healthy Owner" && !item.OwnerHealth.Equals("Healthy", StringComparison.OrdinalIgnoreCase)) return false;
            if (SelectedBaseStatusFilter == "Needs Review" && item.OwnerHealth.Equals("Healthy", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }).ToArray();
        FilteredExplorerBases.Clear();
        foreach (var item in filtered) FilteredExplorerBases.Add(item);
        BaseVisibleCountText = $"{filtered.Length} visible / {ExplorerBases.Count} bases";
        if (!string.IsNullOrWhiteSpace(selectedId))
            SelectedExplorerBase = filtered.FirstOrDefault(x => string.Equals(x.BaseId, selectedId, StringComparison.OrdinalIgnoreCase));
        SelectedExplorerBase ??= filtered.FirstOrDefault();
    }

    public string ExportVisibleGuildsCsv()
    {
        static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
        var builder = new StringBuilder("GuildId,GuildName,Health,LeaderPlayerId,LeaderName,MemberCount,BaseCount,MemberPlayerIds,BaseIds\r\n");
        foreach (var guild in FilteredExplorerGuilds)
            builder.AppendLine(string.Join(',', Csv(guild.GuildId), Csv(guild.GuildName), Csv(guild.Health), Csv(guild.LeaderPlayerId), Csv(guild.LeaderName), Csv(guild.MemberCount.ToString()), Csv(guild.BaseCount.ToString()), Csv(string.Join(';', guild.MemberPlayerIds)), Csv(string.Join(';', guild.BaseIds))));
        return builder.ToString();
    }

    public string ExportVisibleBasesCsv()
    {
        static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
        var builder = new StringBuilder("BaseId,GuildId,GuildName,LeaderPlayerId,LeaderName,OwnerHealth,Evidence\r\n");
        foreach (var item in FilteredExplorerBases)
            builder.AppendLine(string.Join(',', Csv(item.BaseId), Csv(item.GuildId), Csv(item.GuildName), Csv(item.LeaderPlayerId), Csv(item.LeaderName), Csv(item.OwnerHealth), Csv(item.Evidence)));
        return builder.ToString();
    }

    public string WorldPreviewToken { get => _worldPreviewToken; internal set { if (SetField(ref _worldPreviewToken, value)) (ApplyWorldTransactionCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }

    private string _worldPreviewToken = string.Empty;

    public ICommand OpenSelectedGuildLeaderCommand { get; }
}
