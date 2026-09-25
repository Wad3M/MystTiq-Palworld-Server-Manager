namespace MystTiq.Desktop.Services;

// Converts Palworld's raw world coordinates (the REST API's LocationX/LocationY, and the same
// numbers stored in the world save) into a position on the bundled Palpagos map image.
//
// Two stages:
//
// 1. World to in-game map units (unchanged since v0.7.21.0). Formula from the open-source
//    palworld-coord project; it reproduces that project's published example exactly, and it is
//    still what the game's own map shows: a real world's first base converted to (248, -495) while
//    the game's default starting location, the Plateau of Beginnings, is documented at (240, -513).
//    Map X grows to the east, map Y grows to the north.
//
// 2. In-game map units to a pixel on the image (v0.7.96.0). The bundled image is the expanded map
//    including Feybreak, roughly 3.08 map units per pixel on its 1024x1024 frame with the map
//    origin (0, 0) near pixel (625, 335), north up. The old code assumed a +/-1000 frame centred on
//    the image, which predates the expansion and put everything in the wrong place.
//
// How stage 2 was calibrated (no fudge, checked several independent ways):
//  - Scale: Feybreak's documented extent (map X -1456..-608, Y -1743..-798) spans about 276 x 307
//    image pixels, i.e. about 3.08 units per pixel, matching the expanded landscape bounds.
//  - Origin: searched a small window around the Feybreak-derived value for the placement where a
//    live world's 53 real character positions all land on land and its bases sit inland.
//  - Overlay check: the game's start location falls on the same peninsula as that world's first
//    base ("right by the default starting location"), and the recorded travel trail runs from
//    there toward the Small Settlement ruins.
// Accuracy is a few pixels of the 1024 frame (a few hundred metres in-world): good for "where on
// the map is this player or base", not survey grade. If a later game update moves the map, only
// the constants below need to change.
public static class PalworldMapCoordinates
{
    private const double TranslateX = 123888;
    private const double TranslateY = 158000;
    private const double Scale = 459;

    private const double ImagePixels = 1024;
    private const double UnitsPerPixel = 3.08;
    private const double OriginPixelX = 625;
    private const double OriginPixelY = 335;

    public static (double MapX, double MapY) ToMapUnits(double worldX, double worldY)
    {
        var mapX = Math.Round((worldY - TranslateY) / Scale);
        var mapY = Math.Round((worldX + TranslateX) / Scale);
        return (mapX, mapY);
    }

    // The coordinate the game's own map shows for a world position, e.g. "(248, -495)".
    public static string Describe(double worldX, double worldY)
    {
        var (mapX, mapY) = ToMapUnits(worldX, worldY);
        // Adding 0.0 turns a negative zero into a plain zero, which would otherwise print as "-0".
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"({mapX + 0.0:0}, {mapY + 0.0:0})");
    }

    // Position as a fraction (0..1) of the image, left-to-right and top-to-bottom.
    public static (double FractionX, double FractionY) ToImageFraction(double worldX, double worldY)
    {
        var (mapX, mapY) = ToMapUnits(worldX, worldY);
        var pixelX = OriginPixelX + mapX / UnitsPerPixel;
        var pixelY = OriginPixelY - mapY / UnitsPerPixel; // north is up, image Y grows downward
        return (Math.Clamp(pixelX / ImagePixels, 0, 1), Math.Clamp(pixelY / ImagePixels, 0, 1));
    }

    // canvasSize assumes a square canvas matching the World Map card's fixed 480x480 Border, which
    // shows the square image with UniformToFill, so image fractions map straight onto it.
    public static (double X, double Y) ToCanvasPosition(double worldX, double worldY, double canvasSize)
    {
        var (fractionX, fractionY) = ToImageFraction(worldX, worldY);
        return (fractionX * canvasSize, fractionY * canvasSize);
    }
}
