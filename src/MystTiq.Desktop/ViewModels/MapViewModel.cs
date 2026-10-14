// MystTiq v1.1.0.3: file reviewed for this release (2026-10-13).
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
/// v1.0.8.3 (roadmap C-3): the Map page -- the background and its presets, the viewport (zoom and pan), the player,
/// base and Pal markers and their labels, teleport points and the teleport-to-player commands -- moved out of
/// MainWindowViewModel unchanged. The window shows it as vm.Map and feeds it the readings its status refresh gets.
/// </summary>
public sealed class MapViewModel : PageViewModel
{
    private PlayersViewModel Players { get; }

    public MapViewModel(SessionContext session, PlayersViewModel players) : base(session, NavigationPage.Map)
    {
        Players = players;
        TeleportPlayerToMeCommand = new AsyncCommand(() => RunTeleportAsync(toMe: true), () => !Session.IsBusy && Players.SelectedPlayerRecord?.Online == true);
        TeleportToPlayerCommand = new AsyncCommand(() => RunTeleportAsync(toMe: false), () => !Session.IsBusy && Players.SelectedPlayerRecord?.Online == true);
        ClearMapBackgroundCommand = new RelayCommand(ClearMapBackground);
        SetMapPresetCommand = new RelayCommand<MapPreset>(SetMapPreset);
        RefreshMapBasesCommand = new AsyncCommand(RefreshMapBasesAsync, () => !Session.IsBusy);
        SelectMapPlayerCommand = new RelayCommand<string>(SelectMapPlayer);
        ResetMapViewCommand = new RelayCommand(ResetMapView);
        ZoomToPalGroupCommand = new RelayCommand<string>(ZoomToPalGroup);
        InitializeTeleportCommands();
    }

    private bool _useCalibratedWorldPositions = true;

    private Bitmap? _mapBackgroundBitmap;

    private string _mapBackgroundStatusText = "Background: plain coordinate grid.";

    private bool _isWorldMapExpanded = true;

    private Avalonia.Media.ITransform _mapImageTransform = new Avalonia.Media.MatrixTransform(Avalonia.Matrix.Identity);

    private bool _showPalsOnMap = true;

    private string _mapPalsStatusText = string.Empty;

    private bool _showOfflinePlayersOnMap = true;

    private bool _showBasesOnMap = true;

    private string _mapBasesStatusText = string.Empty;

    private string _mapPlayersStatusText = string.Empty;

    // ---- v0.7.113.0: Teleport Points (Map page) ----------------------------------------------------------
    // Points are edited as text, one per line ("spawn -358 270", optional height), the same way kits are.
    private bool _teleportEnabled;
    private string _teleportCommandPrefix = "!tp";
    private decimal _teleportCooldownSeconds = 60;
    private string _teleportPointsText = string.Empty;
    private string _teleportStatusText = "Teleport points not loaded yet.";
    private string _teleportProviderText = string.Empty;
    private string _teleportChatSourceText = string.Empty;
    private string _teleportSendPointName = string.Empty;
    public bool TeleportEnabled { get => _teleportEnabled; set => SetField(ref _teleportEnabled, value); }
    public string TeleportCommandPrefix { get => _teleportCommandPrefix; set => SetField(ref _teleportCommandPrefix, value ?? string.Empty); }
    public decimal TeleportCooldownSeconds { get => _teleportCooldownSeconds; set => SetField(ref _teleportCooldownSeconds, value); }
    public string TeleportPointsText { get => _teleportPointsText; set => SetField(ref _teleportPointsText, value ?? string.Empty); }
    public string TeleportStatusText { get => _teleportStatusText; set => SetField(ref _teleportStatusText, value ?? string.Empty); }
    public string TeleportProviderText { get => _teleportProviderText; set => SetField(ref _teleportProviderText, value ?? string.Empty); }
    public string TeleportChatSourceText { get => _teleportChatSourceText; set => SetField(ref _teleportChatSourceText, value ?? string.Empty); }
    public string TeleportSendPointName { get => _teleportSendPointName; set => SetField(ref _teleportSendPointName, value ?? string.Empty); }
    public ObservableCollection<TeleportUseDto> TeleportRecentUses { get; } = [];
    public ICommand RefreshTeleportCommand { get; private set; } = null!;
    public ICommand SaveTeleportCommand { get; private set; } = null!;
    public ICommand CaptureTeleportPositionCommand { get; private set; } = null!;
    public ICommand SendPlayerToTeleportPointCommand { get; private set; } = null!;

    private void InitializeTeleportCommands()
    {
        RefreshTeleportCommand = new AsyncCommand(RefreshTeleportAsync, () => !Session.IsBusy);
        SaveTeleportCommand = new AsyncCommand(SaveTeleportAsync, () => !Session.IsBusy);
        CaptureTeleportPositionCommand = new AsyncCommand(CaptureTeleportPositionAsync, () => !Session.IsBusy);
        SendPlayerToTeleportPointCommand = new AsyncCommand(SendPlayerToTeleportPointAsync, () => !Session.IsBusy);
    }

    private void ApplyTeleportSnapshot(TeleportSnapshotDto snapshot)
    {
        TeleportEnabled = snapshot.Config.Enabled;
        TeleportCommandPrefix = snapshot.Config.CommandPrefix;
        TeleportCooldownSeconds = snapshot.Config.CooldownSeconds;
        TeleportPointsText = TeleportPointText.Format(snapshot.Config.Points.Select(p => new TeleportPointEntry(p.Name, p.X, p.Y, p.Z)));
        TeleportProviderText = snapshot.Provider.Detail;
        TeleportChatSourceText = snapshot.ChatSource;
        TeleportRecentUses.Clear();
        foreach (var use in snapshot.RecentUses.Take(20)) TeleportRecentUses.Add(use);
    }

    internal async Task RefreshTeleportAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            ApplyTeleportSnapshot(await Session.Api.GetTeleportAsync(Session.SelectedProfile, Session.BearerToken));
            TeleportStatusText = TeleportEnabled ? $"Players can type {TeleportCommandPrefix} <name> in chat." : "Teleporting by chat command is off.";
        }
        catch (Exception ex) { TeleportStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task SaveTeleportAsync()
    {
        if (Session.SelectedProfile is null) return;
        if (!TeleportPointText.TryParse(TeleportPointsText, out var parsed, out var error)) { TeleportStatusText = error; return; }
        Session.IsBusy = true;
        try
        {
            var config = new TeleportConfigDto
            {
                Enabled = TeleportEnabled,
                CommandPrefix = TeleportCommandPrefix.Trim(),
                CooldownSeconds = (int)TeleportCooldownSeconds,
                Points = parsed.Select(p => new TeleportPointDto { Name = p.Name, X = p.X, Y = p.Y, Z = p.Z }).ToList()
            };
            var result = await Session.Api.SaveTeleportAsync(Session.SelectedProfile, config, Session.BearerToken);
            TeleportStatusText = result.Success ? result.Message : string.Join(" ", result.Errors.DefaultIfEmpty(result.Message));
            if (result.Success) ApplyTeleportSnapshot(await Session.Api.GetTeleportAsync(Session.SelectedProfile, Session.BearerToken));
        }
        catch (Exception ex) { TeleportStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // Asks PalDefender where the selected online player is and adds that spot as a new line to name and save.
    private async Task CaptureTeleportPositionAsync()
    {
        if (Session.SelectedProfile is null) return;
        if (Players.SelectedPlayerRecord is not { Online: true } player) { TeleportStatusText = "Select an online player first (click a green dot or a name). Their current position becomes the point."; return; }
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.CaptureTeleportPositionAsync(Session.SelectedProfile, player.PlayerId, Session.BearerToken);
            if (!result.Success || result.X is not { } x || result.Y is not { } y) { TeleportStatusText = result.Message; return; }
            var line = TeleportPointText.Format([new TeleportPointEntry("newpoint", x, y, result.Z)]);
            TeleportPointsText = string.IsNullOrWhiteSpace(TeleportPointsText) ? line : TeleportPointsText.TrimEnd() + "\n" + line;
            TeleportStatusText = $"{result.Message} Added as \"newpoint\": rename it, then Save.";
        }
        catch (Exception ex) { TeleportStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task SendPlayerToTeleportPointAsync()
    {
        if (Session.SelectedProfile is null) return;
        if (Players.SelectedPlayerRecord is not { Online: true } player) { TeleportStatusText = "Select an online player first."; return; }
        if (string.IsNullOrWhiteSpace(TeleportSendPointName)) { TeleportStatusText = "Type the name of a saved point to send them to."; return; }
        Session.IsBusy = true;
        try
        {
            var result = await Session.Api.SendToTeleportPointAsync(Session.SelectedProfile, TeleportSendPointName.Trim(), player.PlayerId, Session.BearerToken);
            TeleportStatusText = result.Message;
        }
        catch (Exception ex) { TeleportStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    public ObservableCollection<PlayerMapPointDto> PlayerMapPoints { get; } = [];

    public Bitmap? MapBackgroundBitmap { get => _mapBackgroundBitmap; private set => SetField(ref _mapBackgroundBitmap, value); }

    public string MapBackgroundStatusText { get => _mapBackgroundStatusText; private set => SetField(ref _mapBackgroundStatusText, value); }

    public ICommand ClearMapBackgroundCommand { get; }

    // v0.7.20.0: bundled presets (Palpagos/World Tree), listed for the picker in MainWindow.axaml.
    public IReadOnlyList<MapPreset> MapPresets => MapPresetService.Presets;

    public ICommand SetMapPresetCommand { get; }

    // v0.7.21.0: real-position calibration only exists (and is only verified against) the
    // Palpagos preset -- derived purely from which real file path is currently loaded, rather than
    // a separately-tracked "active preset" field that could drift out of sync with it.
    public bool IsPalpagosMapActive => _mapPresets.TryGetPresetForPath(_mapBackgroundPath)?.Key == "palpagos";

    // Base coordinates come from a full decoded-world parse server-side, so they are fetched once
    // when the markers first become visible (map expanded in calibrated mode) and then only on the
    // explicit Refresh Bases button, never on the every-few-seconds status poll.
    private void LoadMapBasesIfNeeded()
    {
        if (IsBaseMarkersActive && IsWorldMapExpanded && _lastBaseLocations.Count == 0 && Session.IsConnected)
            _ = RefreshMapBasesAsync();
    }

    // v0.6.16.0: the map background image is a Desktop-local preference (which machine's chosen
    // image to show, if any), never a server-side setting -- see LocalMapPreferencesStore. Never
    // leaves the view in a broken-image state: a missing/deleted/unreadable file just falls back
    // to the plain coordinate grid the map canvas already draws underneath.
    public void SetMapBackgroundImagePath(string path)
    {
        try
        {
            MapBackgroundBitmap = new Bitmap(path);
            _mapPreferences.SaveBackgroundImagePath(path);
            MapBackgroundStatusText = $"Background: {Path.GetFileName(path)}";
            _mapBackgroundPath = path;
            RaisePropertyChanged(nameof(IsPalpagosMapActive));
            RaisePropertyChanged(nameof(IsBaseMarkersActive));
            RebuildPlayerMapPoints(_lastPlayersForMap);
            LoadMapBasesIfNeeded();
        }
        catch (Exception ex)
        {
            MapBackgroundStatusText = $"Unable to load that image: {ex.Message}";
        }
    }

    private void ClearMapBackground()
    {
        MapBackgroundBitmap = null;
        _mapPreferences.SaveBackgroundImagePath(null);
        MapBackgroundStatusText = "Background: plain coordinate grid.";
        _mapBackgroundPath = null;
        RaisePropertyChanged(nameof(IsPalpagosMapActive));
        RaisePropertyChanged(nameof(IsBaseMarkersActive));
        RebuildPlayerMapPoints(_lastPlayersForMap);
    }

    // v0.7.20.0: extracts the bundled preset (once, cached on disk) and hands the resulting real
    // file path to the exact same SetMapBackgroundImagePath path a browsed file already uses --
    // no parallel loading/persistence logic for presets.
    private void SetMapPreset(MapPreset? preset)
    {
        if (preset is null) return;
        try
        {
            var path = _mapPresets.GetOrExtractPresetPath(preset);
            SetMapBackgroundImagePath(path);
        }
        catch (Exception ex)
        {
            MapBackgroundStatusText = $"Unable to load the {preset.DisplayName} preset: {ex.Message}";
        }
    }

    internal void LoadMapBackgroundPreference()
    {
        var path = _mapPreferences.LoadBackgroundImagePath();
        // v0.7.96.0: a first run (no saved preference at all) starts on the bundled Palpagos map
        // instead of a blank grid. Someone who pressed Clear Background has a saved preference
        // (an empty one), so that choice is still respected.
        if (!_mapPreferences.HasSavedPreference)
        {
            var palpagos = MapPresetService.Presets.FirstOrDefault(p => p.Key == "palpagos");
            if (palpagos is not null)
            {
                SetMapPreset(palpagos);
                if (_mapBackgroundPath is not null) return;
            }
        }
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MapBackgroundStatusText = "Background: plain coordinate grid.";
            return;
        }
        try
        {
            MapBackgroundBitmap = new Bitmap(path);
            MapBackgroundStatusText = $"Background: {Path.GetFileName(path)}";
            _mapBackgroundPath = path;
        }
        catch
        {
            MapBackgroundStatusText = "Background: plain coordinate grid (the saved image could not be loaded).";
        }
    }

    // v0.6.16.0: auto-fit the currently-online players with real, parseable location_x/location_y
    // into a fixed canvas, rather than hardcoding Palworld's absolute world-coordinate range (not
    // reliably documented, and would make the view fragile). A player with a missing/unparseable
    // coordinate (offline snapshot gaps, or a REST response that omitted the field) is excluded
    // from the map entirely rather than plotted at a wrong default position.
    //
    // v0.7.0.2: looked into replacing this with Palworld's actual fixed world bounds (a community
    // wiki documents a DataX/DataY -> MapX/MapY conversion). Not shipped -- the fetched formula
    // didn't reconcile with the wiki's own worked example (off by several orders of magnitude,
    // meaning a scale/division step was lost converting the page's math markup to text), and a
    // coordinate transform that can't be verified numerically correct is worse than the existing,
    // already-correct auto-fit behavior. Revisit if a reliable source for the exact formula turns up.
    //
    // v0.7.21.0: revisited with a numerically-verified formula this time (PalworldMapCoordinates --
    // reproduces its source project's own published worked example exactly). It was opt-in until
    // v0.7.96.0 calibrated the world-to-image mapping to the expanded map. It applies only when
    // UseCalibratedWorldPositions is on AND the active background is specifically the Palpagos
    // preset -- every other case (World Tree, a browsed image, no background) keeps this exact
    // auto-fit path.
    private const double MapCanvasSize = 480;

    private const double MapPadding = 24;

    public Avalonia.Media.ITransform MapImageTransform => _mapImageTransform;

    public string MapZoomText => _mapViewport.IsZoomed ? $"Zoom {_mapViewport.Scale:0.0}×" : "Zoom 1.0× (whole map)";

    public bool IsMapZoomed => _mapViewport.IsZoomed;

    public ICommand ResetMapViewCommand { get; private set; } = null!;

    public bool ShowPalsOnMap
    {
        get => _showPalsOnMap;
        set { if (SetField(ref _showPalsOnMap, value)) RebuildPlayerMapPoints(_lastPlayersForMap); }
    }

    public ObservableCollection<PalMapPointDto> PalMapPoints { get; } = [];

    public string MapPalsStatusText { get => _mapPalsStatusText; internal set => SetField(ref _mapPalsStatusText, value); }

    public bool ShowOfflinePlayersOnMap
    {
        get => _showOfflinePlayersOnMap;
        set { if (SetField(ref _showOfflinePlayersOnMap, value)) RebuildPlayerMapPoints(_lastPlayersForMap); }
    }

    // Called by the Map page's pointer handlers. Positions are in map canvas units (0..480).
    public void ZoomMapAt(double viewX, double viewY, double wheelDelta)
    {
        _mapViewport.ZoomByWheel(viewX, viewY, wheelDelta);
        OnMapViewportChanged();
    }

    public void PanMapBy(double viewDx, double viewDy)
    {
        if (!_mapViewport.IsZoomed) return;
        _mapViewport.PanBy(viewDx, viewDy);
        OnMapViewportChanged();
    }

    private void ResetMapView()
    {
        _mapViewport.Reset();
        OnMapViewportChanged();
    }

    internal void ZoomToMapPoint(double flatX, double flatY)
    {
        _mapViewport.CenterOn(flatX, flatY, MapViewport.MarkerZoomScale);
        OnMapViewportChanged();
    }

    // v0.8.11.0: the Pals layer, clustered at the current zoom (PalMapLayout). Drawn under bases and players, and only
    // in the calibrated Palpagos view, like bases, since both come from save coordinates.
    private void RebuildPalMapPoints()
    {
        PalMapPoints.Clear();
        var workers = _lastPalLocations.Count(p => p.Placement == PalMapLayout.BaseWorker);
        var party = _lastPalLocations.Count(p => p.Placement == PalMapLayout.Party);
        MapPalsStatusText = IsBaseMarkersActive
            ? PalMapLayout.Status(workers, party, _lastPalSummary?.InPalbox ?? 0, _lastPalSummary?.WithoutPosition ?? 0, _lastPalsSemanticAvailable,
                namesAvailable: _lastPalLocations.Count > 0 && _lastPalLocations.All(p => !string.IsNullOrWhiteSpace(p.SpeciesName)))
            : string.Empty;
        if (!IsBaseMarkersActive || !ShowPalsOnMap) return;

        var points = _lastPalLocations.Select(pal =>
        {
            var (flatX, flatY) = PalworldMapCoordinates.ToCanvasPosition(pal.X, pal.Y, MapCanvasSize);
            var (viewX, viewY) = _mapViewport.ToView(flatX, flatY);
            var guild = string.Equals(pal.GuildName, "Unowned", StringComparison.Ordinal) ? string.Empty : pal.GuildName;
            return (viewX, viewY, flatX, flatY, new PalMapEntry(pal.Species, pal.IsAlpha, pal.Level, pal.NickName, pal.OwnerName, pal.Placement, guild, pal.X, pal.Y, pal.SpeciesName));
        }).ToList();
        var clusters = PalMapLayout.Cluster(points);
        var baseMarkers = ShowBasesOnMap ? BaseMapPoints.Select(b => (b.CanvasX, b.CanvasY)).ToArray() : [];
        for (var i = 0; i < clusters.Count; i++)
        {
            var cluster = clusters[i];
            var where = PalworldMapCoordinates.Describe(cluster.Members.Average(m => m.WorldX), cluster.Members.Average(m => m.WorldY));
            var badge = PalMapLayout.BadgePosition(cluster.ViewX, cluster.ViewY, baseMarkers);
            var (x, y) = badge ?? (cluster.ViewX, cluster.ViewY);
            PalMapPoints.Add(new PalMapPointDto($"pals-{i}", PalMapLayout.Title(cluster), cluster.Members.Count,
                x, y, cluster.FlatX, cluster.FlatY, PalMapLayout.Tooltip(cluster, where, badge is not null)));
        }
    }

    // v0.7.92.0, v0.8.11.0, v0.7.100.0: base, player and Pal positions are per-server world data, so a tab switch clears
    // them (a different server's markers are never drawn on this tab's map), and a new server starts on the whole map.
    internal void ResetForNewServer()
    {
        _lastBaseLocations = [];
        _lastPlayerLocations = [];
        _lastExplorerPlayers = [];
        _lastPalLocations = [];
        _lastPalSummary = null;
        _lastPalsSemanticAvailable = false;
        PalMapPoints.Clear();
        MapPalsStatusText = string.Empty;
        BaseMapPoints.Clear();
        MapBasesStatusText = string.Empty;
        _mapViewport.Reset();
        OnMapViewportChanged();
    }

    internal void OnMapViewportChanged()
    {
        var s = _mapViewport.Scale;
        _mapImageTransform = new Avalonia.Media.MatrixTransform(new Avalonia.Matrix(s, 0, 0, s, _mapViewport.OffsetX, _mapViewport.OffsetY));
        RaisePropertyChanged(nameof(MapImageTransform));
        RaisePropertyChanged(nameof(MapZoomText));
        RaisePropertyChanged(nameof(IsMapZoomed));
        RebuildPlayerMapPoints(_lastPlayersForMap);
    }

    internal void RebuildPlayerMapPoints(IReadOnlyList<PlayerSnapshotDto> players)
    {
        _lastPlayersForMap = players;
        PlayerMapPoints.Clear();
        BaseMapPoints.Clear();

        // v0.7.92.0: guild-base markers. Only ever drawn in the calibrated real-world-position mode:
        // bases come from decoded save coordinates and players from the REST API's location fields,
        // and only the calibrated formula treats both as the same coordinate space. The default
        // auto-fit view spreads dots relative to whoever is online, which has no meaning for a
        // saved base, so mixing them there would be a misleading picture.
        if (IsBaseMarkersActive && ShowBasesOnMap)
        {
            foreach (var location in _lastBaseLocations)
            {
                var (flatX, flatY) = PalworldMapCoordinates.ToCanvasPosition(location.X, location.Y, MapCanvasSize);
                var (viewX, viewY) = _mapViewport.ToView(flatX, flatY);
                BaseMapPoints.Add(new BaseMapPointDto(location.BaseId, location.GuildName, viewX, viewY, PalworldMapCoordinates.Describe(location.X, location.Y), flatX, flatY));
            }
        }

        RebuildPalMapPoints();

        var located = players
            .Select(p => (Player: p, X: TryParseCoordinate(p.LocationX), Y: TryParseCoordinate(p.LocationY)))
            .Where(t => t.X.HasValue && t.Y.HasValue)
            .Select(t => (t.Player, X: t.X!.Value, Y: t.Y!.Value))
            .ToList();

        var calibrated = UseCalibratedWorldPositions && IsPalpagosMapActive;

        // v0.7.100.0: players who are not online are drawn at the last position the save recorded for
        // them, as hollow markers, before the online dots so an online dot is never hidden under one.
        // Anyone the server currently lists as online is skipped even if it gave no position yet.
        var offlineCount = 0;
        if (calibrated && ShowOfflinePlayersOnMap)
        {
            var onlineKeys = players.Select(p => NormalizePlayerKey(p.PlayerId)).ToHashSet(StringComparer.Ordinal);
            foreach (var saved in _lastPlayerLocations)
            {
                var key = NormalizePlayerKey(saved.PlayerId);
                if (onlineKeys.Contains(key)) continue;
                var record = _lastExplorerPlayers.FirstOrDefault(p => NormalizePlayerKey(p.PlayerId) == key);
                if (record?.Online == true) continue;
                var name = !string.IsNullOrWhiteSpace(record?.PlayerName) ? record!.PlayerName
                    : !string.IsNullOrWhiteSpace(saved.Name) ? saved.Name : "Unknown player";
                var (flatX, flatY) = PalworldMapCoordinates.ToCanvasPosition(saved.X, saved.Y, MapCanvasSize);
                var (viewX, viewY) = _mapViewport.ToView(flatX, flatY);
                var lastSeen = record?.SaveLastWriteUtc is { } written ? $"save written {written.ToLocalTime():yyyy-MM-dd HH:mm}" : string.Empty;
                PlayerMapPoints.Add(new PlayerMapPointDto(record?.PlayerId ?? saved.PlayerId, name, viewX, viewY,
                    PalworldMapCoordinates.Describe(saved.X, saved.Y), flatX, flatY, IsOnline: false, LastSeenText: lastSeen));
                offlineCount++;
            }
        }

        MapPlayersStatusText = MapContentsText.Describe(players.Count, located.Count, BaseMapPoints.Count, offlineCount);
        if (located.Count > 0)
        {
            if (calibrated)
            {
                foreach (var (player, x, y) in located)
                {
                    var (flatX, flatY) = PalworldMapCoordinates.ToCanvasPosition(x, y, MapCanvasSize);
                    var (viewX, viewY) = _mapViewport.ToView(flatX, flatY);
                    PlayerMapPoints.Add(new PlayerMapPointDto(player.PlayerId, player.DisplayName, viewX, viewY, PalworldMapCoordinates.Describe(x, y), flatX, flatY));
                }
            }
            else
            {
                var minX = located.Min(t => t.X); var maxX = located.Max(t => t.X);
                var minY = located.Min(t => t.Y); var maxY = located.Max(t => t.Y);
                var spanX = Math.Max(maxX - minX, 1d);
                var spanY = Math.Max(maxY - minY, 1d);
                var usable = MapCanvasSize - MapPadding * 2;

                foreach (var (player, x, y) in located)
                {
                    var canvasX = MapPadding + (x - minX) / spanX * usable;
                    // World Y increases northward in Palworld's coordinate system; canvas Y increases
                    // downward, so this is inverted to keep "up" on the map meaning "north" in-world.
                    var canvasY = MapPadding + (1 - (y - minY) / spanY) * usable;
                    var (viewX, viewY) = _mapViewport.ToView(canvasX, canvasY);
                    PlayerMapPoints.Add(new PlayerMapPointDto(player.PlayerId, player.DisplayName, viewX, viewY, string.Empty, canvasX, canvasY));
                }
            }
        }

        ApplyMapLabelDeoverlap();
    }

    // v0.7.106.0: markers standing close together had their name labels drawn directly on top of each
    // other. Runs after every rebuild (including every zoom and pan, since RebuildPlayerMapPoints is
    // called again on each), over the current SCREEN position of every base and player marker together --
    // MapLabelLayout is the pure pass that decides how far each label is pushed down. Bases are listed
    // first, matching their draw order, so a base keeps its label at the dot and a player standing on it
    // is the one that staggers.
    //
    // v0.7.109.0: two markers at (or extremely near) the exact same pixel used to render as one visible
    // icon -- named as a residual gap in v0.7.106.0's own doc, since that version only ever staggered
    // labels, never dots. ComputeMarkerOffsets runs first, fanning a truly coincident marker out a few
    // pixels from the shared point (the first in the cluster stays exactly on the real spot); the label
    // pass then runs against those FANNED-OUT positions, so a spread-apart marker's label reacts to where
    // it now actually sits rather than where it started.
    private void ApplyMapLabelDeoverlap()
    {
        var rawPoints = new List<(double X, double Y)>(BaseMapPoints.Count + PlayerMapPoints.Count);
        foreach (var b in BaseMapPoints) rawPoints.Add((b.CanvasX, b.CanvasY));
        foreach (var p in PlayerMapPoints) rawPoints.Add((p.CanvasX, p.CanvasY));
        var markerOffsets = MapLabelLayout.ComputeMarkerOffsets(rawPoints);
        var effectivePoints = rawPoints.Select((pt, i) => (X: pt.X + markerOffsets[i].X, Y: pt.Y + markerOffsets[i].Y)).ToList();
        // v0.7.115.0: labels are laid out as real text rectangles. Widths match the XAML fonts: base names are
        // FontSize 9, player names FontSize 10.
        var labels = new List<MapLabelLayout.MapLabel>(effectivePoints.Count);
        for (var i = 0; i < effectivePoints.Count; i++)
        {
            var width = i < BaseMapPoints.Count
                ? MapLabelLayout.EstimateLabelWidth(BaseMapPoints[i].GuildName, 9)
                : MapLabelLayout.EstimateLabelWidth(PlayerMapPoints[i - BaseMapPoints.Count].Name, 10);
            labels.Add(new(effectivePoints[i].X, effectivePoints[i].Y, width));
        }
        // v0.8.11.0: name labels step around the Pals markers, so a badge's count is never written over.
        var palBoxes = PalMapPoints.Select(p => new MapLabelLayout.Box(p.CanvasX - PalMapPointDto.MarkerSize / 2, p.CanvasY - PalMapPointDto.MarkerSize / 2,
            PalMapPointDto.MarkerSize, PalMapPointDto.MarkerSize)).ToArray();
        var labelOffsets = MapLabelLayout.ComputeLabelOffsets(labels, palBoxes);

        for (var i = 0; i < BaseMapPoints.Count; i++)
        {
            var (mx, my) = markerOffsets[i];
            var (lx, ly) = labelOffsets[i];
            if (mx != 0 || my != 0 || lx != 0 || ly != 0)
                BaseMapPoints[i] = BaseMapPoints[i] with { LabelOffsetX = lx, LabelOffsetY = ly, MarkerOffsetX = mx, MarkerOffsetY = my };
        }

        var baseCount = BaseMapPoints.Count;
        for (var i = 0; i < PlayerMapPoints.Count; i++)
        {
            var (mx, my) = markerOffsets[baseCount + i];
            var (lx, ly) = labelOffsets[baseCount + i];
            if (mx != 0 || my != 0 || lx != 0 || ly != 0)
                PlayerMapPoints[i] = PlayerMapPoints[i] with { LabelOffsetX = lx, LabelOffsetY = ly, MarkerOffsetX = mx, MarkerOffsetY = my };
        }
    }

    public ObservableCollection<BaseMapPointDto> BaseMapPoints { get; } = [];

    public bool ShowBasesOnMap
    {
        get => _showBasesOnMap;
        set { if (SetField(ref _showBasesOnMap, value)) RebuildPlayerMapPoints(_lastPlayersForMap); }
    }

    public string MapBasesStatusText { get => _mapBasesStatusText; internal set => SetField(ref _mapBasesStatusText, value); }

    public string MapPlayersStatusText { get => _mapPlayersStatusText; private set => SetField(ref _mapPlayersStatusText, value); }

    public ICommand RefreshMapBasesCommand { get; private set; } = null!;

    public ICommand SelectMapPlayerCommand { get; private set; } = null!;

    private async Task RefreshMapBasesAsync()
    {
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { MapBasesStatusText = ex.Message; return; }

        MapBasesStatusText = "Reading base locations from the decoded world save…";
        try
        {
            var snapshot = await Session.Api.GetPlayerGuildExplorerAsync(profile, Session.BearerToken);
            ApplyBaseLocations(snapshot);
        }
        catch (Exception ex) { MapBasesStatusText = $"Base locations unavailable: {ex.Message}"; }
    }

    // Clicking a player dot selects that player, so the existing Teleport To Me / Teleport To Player
    // commands (which act on Players.SelectedPlayerRecord) work straight from the map. Palworld's vanilla
    // RCON has no "teleport to coordinates" command, so map clicks can only target a player.
    private void SelectMapPlayer(string? playerId)
    {
        if (string.IsNullOrWhiteSpace(playerId)) return;
        static string Norm(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        var wanted = Norm(playerId);
        // v0.7.100.0: clicking a marker (or a name in the list) also zooms in on it.
        var marker = PlayerMapPoints.FirstOrDefault(p => Norm(p.PlayerId) == wanted);
        if (marker is not null) ZoomToMapPoint(marker.UnzoomedX, marker.UnzoomedY);
        var match = Players.PlayerRecords.FirstOrDefault(p => Norm(p.PlayerId) == wanted);
        if (match is not null) Players.SelectedPlayerRecord = match;
        else MapBasesStatusText = "That player is not in the loaded player list yet. Refresh Players, then click again.";
    }

    public ICommand TeleportPlayerToMeCommand { get; }

    public ICommand TeleportToPlayerCommand { get; }

    // v0.7.8.0: toMe=true summons the selected online player to the admin's own in-game character
    // (RCON TeleportToMe); toMe=false moves the admin's character to the player instead (RCON
    // TeleportToPlayer). Both require the admin to actually have a character present in the world
    // -- this is a headless dedicated server, so there is no guarantee of that, and Palworld's RCON
    // gives no distinct error for "no admin character" versus "player not found"; the raw RCON
    // response text is surfaced as-is so the operator can tell the two apart themselves.
    private async Task RunTeleportAsync(bool toMe)
    {
        if (Players.SelectedPlayerRecord is null) { Players.PlayerAdminStatusText = "Select a player first."; return; }
        if (!Players.SelectedPlayerRecord.Online) { Players.PlayerAdminStatusText = "Teleport requires a currently online player."; return; }
        var playerId = Players.SelectedPlayerRecord.PlayerId;
        if (string.IsNullOrWhiteSpace(playerId)) { Players.PlayerAdminStatusText = "The selected player does not have a usable server-side identity."; return; }
        ConnectionProfile profile;
        try { profile = Session.BuildProfile(); }
        catch (Exception ex) { Players.PlayerAdminStatusText = ex.Message; return; }

        Session.IsBusy = true;
        Players.PlayerAdminStatusText = toMe ? $"Summoning {Players.SelectedPlayerRecord.PlayerName} to you…" : $"Teleporting to {Players.SelectedPlayerRecord.PlayerName}…";
        try
        {
            var result = toMe
                ? await Session.Api.TeleportToMeAsync(profile, playerId, Session.BearerToken)
                : await Session.Api.TeleportToPlayerAsync(profile, playerId, Session.BearerToken);
            Players.PlayerAdminStatusText = result.Message;
        }
        catch (Exception ex) { Players.PlayerAdminStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // On by default since v0.7.96.0 (it was an off-by-default experiment from v0.7.21.0 until the
    // image mapping was calibrated). See PalworldMapCoordinates' header comment for how. Turning
    // it off falls back to the relative-spread view, which cannot show bases.
    public bool UseCalibratedWorldPositions
    {
        get => _useCalibratedWorldPositions;
        set
        {
            if (!SetField(ref _useCalibratedWorldPositions, value)) return;
            RaisePropertyChanged(nameof(IsBaseMarkersActive));
            RebuildPlayerMapPoints(_lastPlayersForMap);
            LoadMapBasesIfNeeded();
        }
    }

    public bool IsBaseMarkersActive => UseCalibratedWorldPositions && IsPalpagosMapActive;

    public bool IsWorldMapExpanded { get => _isWorldMapExpanded; internal set => SetField(ref _isWorldMapExpanded, value); }


    // v0.7.92.0: guild-base markers and click-to-select on the live map.
    private IReadOnlyList<BaseLocationDto> _lastBaseLocations = [];

    private IReadOnlyList<PlayerSnapshotDto> _lastPlayersForMap = [];

    // v0.7.100.0: the map's zoom and pan. Markers are recomputed at the current zoom (their shapes stay
    // the same size); only the background image is scaled, through MapImageTransform.
    private readonly MapViewport _mapViewport = new(MapCanvasSize);

    // v0.8.11.0: owned Pals out in the world (base workers and party members), from the same explorer read as bases.
    private IReadOnlyList<PalLocationDto> _lastPalLocations = [];

    private PalSummaryDto? _lastPalSummary;

    private bool _lastPalsSemanticAvailable;

    private IReadOnlyList<PlayerLocationDto> _lastPlayerLocations = [];

    private IReadOnlyList<PlayerExplorerItemDto> _lastExplorerPlayers = [];

    private readonly LocalMapPreferencesStore _mapPreferences = new();

    private readonly MapPresetService _mapPresets = new();

    private string? _mapBackgroundPath;

    // Base coordinates arrive with the Players page's own explorer read, so the map does not need
    // a second request; the Refresh Bases button still re-reads on demand.
    internal void ApplyBaseLocations(PlayerGuildSnapshotDto snapshot)
    {
        _lastBaseLocations = snapshot.BaseLocations;
        // v0.7.100.0: the same explorer read carries where every player last was and who is online.
        _lastPlayerLocations = snapshot.PlayerLocations;
        _lastExplorerPlayers = snapshot.Players;
        _lastPalLocations = snapshot.PalLocations;
        _lastPalSummary = snapshot.PalSummary;
        _lastPalsSemanticAvailable = snapshot.SemanticAvailable;
        MapBasesStatusText = snapshot.BaseLocations.Count > 0
            ? $"{snapshot.BaseLocations.Count} base(s) located from decoded Level.sav.json."
            : snapshot.SemanticAvailable
                ? "The decoded world save lists no base coordinates."
                : "No base coordinates: guild and base data need decoded Level.sav.json (Palworld Save Tools).";
        RebuildPlayerMapPoints(_lastPlayersForMap);
    }

    private static double? TryParseCoordinate(string value) =>
        double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static string NormalizePlayerKey(string? value) =>
        new((value ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private void ZoomToPalGroup(string? key)
    {
        var point = PalMapPoints.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.Ordinal));
        if (point is null) return;
        // All the way in: base workers stand within a few dozen metres of each other.
        _mapViewport.CenterOn(point.UnzoomedX, point.UnzoomedY, MapViewport.MaxScale);
        OnMapViewportChanged();
    }

    public ICommand ZoomToPalGroupCommand { get; private set; } = null!;
}
