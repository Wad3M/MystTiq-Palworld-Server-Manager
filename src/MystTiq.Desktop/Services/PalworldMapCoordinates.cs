namespace MystTiq.Desktop.Services;

// v0.7.21.0: converts Palworld's raw world coordinates (as reported by the REST API's
// LocationX/LocationY, the same fields RebuildPlayerMapPoints already parses) into a position on
// the Palpagos map image, using the formula the open-source palworld-coord project
// (github.com/palworldlol/palworld-coord) reverse-engineered from the game's own .sav format and
// WorldMapUIData/DT_WorldMapUIData.json bounds.
//
// Verified before shipping: the constants below reproduce that project's own published worked
// example exactly -- sav_to_map(-167230, 96430) returns (-134, -94) in its README, and
// ToMapUnits(-167230, 96430) below returns the same (-134, -94) after rounding. The scale (459)
// and translation constants also reconcile precisely with the documented .sav coordinate bounds
// ((-582888,-301000) to (335112,617000)): both axes span exactly 459000 units, i.e. exactly
// 1000*459, landing exactly on the map's own -1000..1000 range with no residual error.
//
// NOT verified: which corner of the Palpagos map IMAGE this coordinate space's origin corresponds
// to, or whether map-Y needs inverting for screen rendering (Y-down canvas vs. this coordinate
// space's own orientation). Palworld's own REST API documents world coordinates as Y-increasing-
// northward (the same convention RebuildPlayerMapPoints' existing auto-fit path already inverts
// for its canvas), but this reverse-engineered "map" space is a separate, UI-facing coordinate
// system whose own orientation isn't independently confirmed here. ToCanvasPosition's current
// choice (center-origin, no additional Y-flip) is a reasoned best guess, not a confirmed one --
// see docs/architecture/v0.7.21.0-*.md. This is why the feature this powers ships as an explicit,
// off-by-default "Experimental" toggle rather than silently replacing the existing auto-fit view.
public static class PalworldMapCoordinates
{
    private const double TranslateX = 123888;
    private const double TranslateY = 158000;
    private const double Scale = 459;
    private const double MapExtent = 1000;

    public static (double MapX, double MapY) ToMapUnits(double worldX, double worldY)
    {
        var mapX = Math.Round((worldY - TranslateY) / Scale);
        var mapY = Math.Round((worldX + TranslateX) / Scale);
        return (mapX, mapY);
    }

    // canvasSize assumes a square canvas matching the World Map card's fixed 480x480 Border.
    public static (double X, double Y) ToCanvasPosition(double worldX, double worldY, double canvasSize)
    {
        var (mapX, mapY) = ToMapUnits(worldX, worldY);
        var fractionX = Math.Clamp((mapX + MapExtent) / (MapExtent * 2), 0, 1);
        var fractionY = Math.Clamp((mapY + MapExtent) / (MapExtent * 2), 0, 1);
        return (fractionX * canvasSize, fractionY * canvasSize);
    }
}
