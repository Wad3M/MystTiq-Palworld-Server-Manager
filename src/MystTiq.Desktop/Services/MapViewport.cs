namespace MystTiq.Desktop.Services;

// v0.7.100.0: the zoom and pan state of the Map page, with no UI dependency so the logic harness can
// test it. Positions are in "canvas units": the map is a square of `Size` units (480), and a marker's
// unzoomed position is where it sits at scale 1. On screen a point appears at
//     view = canvas * Scale + Offset
// and the offset is clamped so the map never slides away from the frame (at scale 1 it is always 0).
public sealed class MapViewport
{
    public const double MinScale = 1;
    public const double MaxScale = 10;
    // How far each wheel notch zooms. 1.25 per notch reaches MaxScale in about ten notches.
    public const double WheelStep = 1.25;
    // The scale a marker click zooms to, unless the view is already closer than this.
    public const double MarkerZoomScale = 4;

    public MapViewport(double size) => Size = size;

    public double Size { get; }
    public double Scale { get; private set; } = 1;
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }
    public bool IsZoomed => Scale > MinScale + 1e-9;

    public (double X, double Y) ToView(double canvasX, double canvasY) =>
        (canvasX * Scale + OffsetX, canvasY * Scale + OffsetY);

    public (double X, double Y) FromView(double viewX, double viewY) =>
        ((viewX - OffsetX) / Scale, (viewY - OffsetY) / Scale);

    // Zooms to `newScale` while the canvas point currently under (viewX, viewY) stays under it: the
    // usual "zoom toward the cursor" behaviour.
    public void ZoomAt(double viewX, double viewY, double newScale)
    {
        newScale = Math.Clamp(newScale, MinScale, MaxScale);
        var (canvasX, canvasY) = FromView(viewX, viewY);
        Scale = newScale;
        OffsetX = viewX - canvasX * newScale;
        OffsetY = viewY - canvasY * newScale;
        Clamp();
    }

    // A wheel notch away from the user (positive delta) zooms in; toward the user zooms out.
    public void ZoomByWheel(double viewX, double viewY, double wheelDelta) =>
        ZoomAt(viewX, viewY, Scale * Math.Pow(WheelStep, wheelDelta));

    public void PanBy(double viewDx, double viewDy)
    {
        OffsetX += viewDx;
        OffsetY += viewDy;
        Clamp();
    }

    // Puts the canvas point (canvasX, canvasY) at the centre of the frame at the given scale (or the
    // current scale if that is already closer). Near an edge the clamp keeps the map in frame, so the
    // point may sit off-centre rather than showing empty space.
    public void CenterOn(double canvasX, double canvasY, double scale)
    {
        Scale = Math.Clamp(Math.Max(scale, Scale), MinScale, MaxScale);
        OffsetX = Size / 2 - canvasX * Scale;
        OffsetY = Size / 2 - canvasY * Scale;
        Clamp();
    }

    public void Reset()
    {
        Scale = 1;
        OffsetX = 0;
        OffsetY = 0;
    }

    private void Clamp()
    {
        var min = Size * (1 - Scale);
        OffsetX = Math.Clamp(OffsetX, min, 0);
        OffsetY = Math.Clamp(OffsetY, min, 0);
    }
}
