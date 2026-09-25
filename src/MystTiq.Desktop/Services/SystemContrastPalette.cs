using System.Runtime.InteropServices;
using Avalonia.Media;

namespace MystTiq.Desktop.Services;

// v0.8.25.0: Windows' own "Contrast themes" (Aquatic, Desert, Dusk, Night sky, or one the user made). When one is on,
// MystTiq's High contrast mode uses its colours instead of MystTiq's own black-and-white palette, and "Follow the
// system" switches to High contrast. Read from Windows itself: SystemParametersInfo(SPI_GETHIGHCONTRAST) says whether a
// contrast theme is on, and GetSysColor gives its colours (the same ones Windows' own apps use). Elsewhere, or when no
// contrast theme is on, there is none and MystTiq's own palette applies.
public sealed record SystemContrastPalette(
    Color Window, Color WindowText, Color Highlight, Color HighlightText, Color GrayText, Color Hotlight, Color ButtonFace, Color ButtonText)
{
    // Desert is a light contrast theme; the others are dark.
    public bool IsLight => DecorativePalette.Luminance(Window) > 0.5;

    // Structural colours in ThemeCatalog's keys.
    public IReadOnlyDictionary<string, Color> Structural => new Dictionary<string, Color>
    {
        ["Bg0"] = Window, ["Bg1"] = Window, ["Bg2"] = Window, ["Card"] = Window, ["CardStrong"] = Window,
        ["Border"] = WindowText, ["BorderSoft"] = GrayText, ["Muted"] = GrayText, ["Text"] = WindowText,
        ["InputFieldBg"] = Window, ["ScrollTrackBg"] = Window, ["ListRowAltBg"] = Window,
        ["ListRowHoverBg"] = ThemeColorMath.Blend(Window, Highlight, 0.35), ["ListRowSelectedBg"] = Highlight,
        ["Bg0Scrim"] = ThemeColorMath.WithAlpha(Window, 0xF5),
    };

    // A step from the window colour toward its text, for hover and pressed surfaces.
    public Color Shade(double amount) => ThemeColorMath.Blend(Window, WindowText, amount);

    public static SystemContrastPalette? ReadFromSystem()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try
        {
            var info = new HighContrastInfo { Size = (uint)Marshal.SizeOf<HighContrastInfo>() };
            if (!SystemParametersInfo(SpiGetHighContrast, info.Size, ref info, 0) || (info.Flags & HighContrastOn) == 0) return null;
            static Color Sys(int index) { var v = GetSysColor(index); return Color.FromRgb((byte)(v & 0xFF), (byte)((v >> 8) & 0xFF), (byte)((v >> 16) & 0xFF)); }
            return new SystemContrastPalette(Sys(5), Sys(8), Sys(13), Sys(14), Sys(17), Sys(26), Sys(15), Sys(18));
        }
        catch { return null; }
    }

    private const uint SpiGetHighContrast = 0x0042;
    private const uint HighContrastOn = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct HighContrastInfo
    {
        public uint Size;
        public uint Flags;
        public IntPtr DefaultScheme;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint action, uint param, ref HighContrastInfo info, uint winIni);

    [DllImport("user32.dll")]
    private static extern uint GetSysColor(int index);
}
