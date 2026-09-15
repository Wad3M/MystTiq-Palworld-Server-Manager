using Avalonia.Media;

namespace MystTiq.Desktop.Services;

// v0.7.55.0: small color-blend helpers ThemeApplier uses to COMPUTE derived theme resources
// (page-accent borders/glows/card-gradients) from ThemeCatalog's existing base colors, instead of
// needing every combination hand-authored. Deliberately simple linear RGB blending, not perceptual
// color space math -- consistent and predictable across ~300 derived values matters far more here
// than perceptual accuracy for a handful of them, and this app's whole existing palette (including
// every value ThemeCatalog already hand-authors) is itself simple hex literals with no evidence of
// perceptual-space tuning either.
public static class ThemeColorMath
{
    public static Color Blend(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromArgb(
            LerpByte(a.A, b.A, t),
            LerpByte(a.R, b.R, t),
            LerpByte(a.G, b.G, t),
            LerpByte(a.B, b.B, t));
    }

    public static Color Lighten(Color color, double amount) => Blend(color, Colors.White, amount);
    public static Color Darken(Color color, double amount) => Blend(color, Colors.Black, amount);

    public static Color WithAlpha(Color color, byte alpha) => Color.FromArgb(alpha, color.R, color.G, color.B);

    private static byte LerpByte(byte from, byte to, double t) =>
        (byte)Math.Round(from + (to - from) * t, MidpointRounding.AwayFromZero);
}
