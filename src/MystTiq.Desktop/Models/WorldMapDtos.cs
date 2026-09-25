using Avalonia;

namespace MystTiq.Desktop.Models;

// v0.6.16.0: screen-ready map point, computed once in the ViewModel (auto-fit scaling over the
// currently-online players' real location_x/location_y), not a server contract -- matches the
// existing convention MetricHistoryPoint.CpuGraphHeight already establishes for this codebase
// (compute screen-ready values where the data is assembled, not via an XAML value converter).
// v0.7.92.0: carries PlayerId so clicking a dot can select that player for the existing
// teleport/admin commands.
// v0.7.99.0: CoordinateText is the in-game map coordinate the game's own map shows, e.g.
// "(248, -495)". Only set in the calibrated real-world mode, where it means something.
// v0.7.100.0: CanvasX/CanvasY are now where the marker is on screen at the current zoom, and
// UnzoomedX/UnzoomedY are where it sits on the whole map (what a click zooms to). IsOnline says
// whether the position is live (online, from the REST API) or the last known one from the save.
public sealed record PlayerMapPointDto(
    string PlayerId, string Name, double CanvasX, double CanvasY, string CoordinateText = "",
    double UnzoomedX = 0, double UnzoomedY = 0, bool IsOnline = true, string LastSeenText = "",
    double LabelOffsetY = 0, double MarkerOffsetX = 0, double MarkerOffsetY = 0, double LabelOffsetX = 0)
{
    // Places the marker's centre on its point, fanned out by MarkerOffsetX/Y when another marker sits at
    // (or extremely near) the exact same position (v0.7.109.0, see MapLabelLayout.ComputeMarkerOffsets) --
    // zero for anything not coincident, so a marker with real separation is never nudged off its true spot.
    // Markers sit in a Grid layer and are positioned by margin; Canvas.Left and Canvas.Top did not position
    // them (see the Map page in MainWindow.axaml).
    public Thickness MarkerMargin => new(CanvasX + MarkerOffsetX - 5, CanvasY + MarkerOffsetY - 5, 0, 0);
    // v0.7.115.0: labels are drawn in their own layer at exactly the rectangle MapLabelLayout placed (left edge
    // of the dot, just below it, moved by LabelOffsetX/Y; see MapLabelLayout). Inside the marker Button they were subject to the
    // app-wide Button style (MinHeight 31, centred content, ClipToBounds), which moved them off the computed
    // position and, for players, stopped them drawing at all.
    public Thickness LabelPosition => new(CanvasX + MarkerOffsetX - 5 + LabelOffsetX, CanvasY + MarkerOffsetY + 5 + LabelOffsetY, 0, 0);
    public bool IsOffline => !IsOnline;
    public string StateText => IsOnline ? "online now" : string.IsNullOrEmpty(LastSeenText) ? "offline, last known position" : $"offline, last known position ({LastSeenText})";
    public string ListText => string.IsNullOrEmpty(CoordinateText)
        ? $"{(IsOnline ? "●" : "○")} {Name}"
        : $"{(IsOnline ? "●" : "○")} {Name}   {CoordinateText}   {(IsOnline ? "online" : "offline")}";
    public string Tooltip => string.IsNullOrEmpty(CoordinateText)
        ? $"{Name}: {StateText}. Click to zoom in and select."
        : $"{Name} {CoordinateText}: {StateText}. Click to zoom in and select.";
}

// v0.8.11.0: screen-ready Pals marker: one Pal, or several standing within PalMapLayout.ClusterRadius pixels of each
// other at the current zoom (Count > 1 shows the count inside the marker). Same position convention as the others.
public sealed record PalMapPointDto(
    string Key, string Title, int Count, double CanvasX, double CanvasY, double UnzoomedX, double UnzoomedY, string Tooltip)
{
    public const double MarkerSize = 14;
    public Thickness MarkerMargin => new(CanvasX - MarkerSize / 2, CanvasY - MarkerSize / 2, 0, 0);
    public string CountText => Count > 1 ? Count.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
    public string ListText => Title;
}

// v0.7.92.0: screen-ready guild-base marker, same convention as PlayerMapPointDto above.
public sealed record BaseMapPointDto(
    string BaseId, string GuildName, double CanvasX, double CanvasY, string CoordinateText = "",
    double UnzoomedX = 0, double UnzoomedY = 0, double LabelOffsetY = 0, double MarkerOffsetX = 0, double MarkerOffsetY = 0, double LabelOffsetX = 0)
{
    // v0.7.109.0: see PlayerMapPointDto.MarkerMargin.
    public Thickness MarkerMargin => new(CanvasX + MarkerOffsetX - 5, CanvasY + MarkerOffsetY - 5, 0, 0);
    // v0.7.115.0: see PlayerMapPointDto.LabelPosition.
    public Thickness LabelPosition => new(CanvasX + MarkerOffsetX - 5 + LabelOffsetX, CanvasY + MarkerOffsetY + 5 + LabelOffsetY, 0, 0);
    public string ListText => string.IsNullOrEmpty(CoordinateText) ? GuildName : $"{GuildName}   {CoordinateText}";
    public string Tooltip => string.IsNullOrEmpty(CoordinateText) ? $"{GuildName}. Click to zoom in." : $"{GuildName} base {CoordinateText}. Click to zoom in.";
}
