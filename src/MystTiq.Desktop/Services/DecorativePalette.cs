using Avalonia.Controls;
using Avalonia.Media;

namespace MystTiq.Desktop.Services;

// v0.8.25.0: the decorative colours that used to be literals in the styles (glows, BoxShadow shorthands, fixed borders
// and a few surfaces) -- the "~240 outside the catalogue" v0.8.16.0 left. Each is now a resource, written by
// ThemeApplier for the current mode. Dark keeps the exact value it was tuned with; the other modes derive from it by the
// colour's role, so a new mode needs no per-colour tuning:
//   - Light: dark surfaces become light ones tinted by their hue, bright borders and text darken, glows soften.
//   - High contrast: black surfaces, white borders and text, no glows.
//   - Midnight: dark surfaces go deeper; the rest as Dark.
public enum DecorativeRole { Border, Background, Foreground, GlowColor }

public sealed record DecorativeColor(DecorativeRole Role, string Dark);

public static class DecorativePalette
{
    public static readonly IReadOnlyDictionary<string, DecorativeColor> Colors = new Dictionary<string, DecorativeColor>
    {
        // The history charts (Controls/ResourceHistoryChart.cs, HostHistoryChart.cs) draw their own background and grid.
        ["DecoBackground_08111A"] = new(DecorativeRole.Background, "#08111A"),
        ["DecoBorder_183047"] = new(DecorativeRole.Border, "#183047"),
        ["DecoBorder_5F7F98"] = new(DecorativeRole.Border, "#5F7F98"),
        ["DecoBorder_5AAED4"] = new(DecorativeRole.Border, "#5AAED4"),
        ["DecoBorder_7DD9FF"] = new(DecorativeRole.Border, "#7DD9FF"),
        ["DecoBorder_C4F1FF"] = new(DecorativeRole.Border, "#C4F1FF"),
        ["DecoBorder_FF858C"] = new(DecorativeRole.Border, "#FF858C"),
        ["DecoBorder_FFC0C4"] = new(DecorativeRole.Border, "#FFC0C4"),
        ["DecoBorder_5EE0AA"] = new(DecorativeRole.Border, "#5EE0AA"),
        ["DecoBorder_9AF0C8"] = new(DecorativeRole.Border, "#9AF0C8"),
        ["DecoBorder_2E9E6C"] = new(DecorativeRole.Border, "#2E9E6C"),
        ["DecoBorder_FFDE8A"] = new(DecorativeRole.Border, "#FFDE8A"),
        ["DecoForeground_2A1B04"] = new(DecorativeRole.Foreground, "#2A1B04"),
        ["DecoBorder_FFEBB0"] = new(DecorativeRole.Border, "#FFEBB0"),
        ["DecoBorder_C4841A"] = new(DecorativeRole.Border, "#C4841A"),
        ["DecoBorder_6FE8F5"] = new(DecorativeRole.Border, "#6FE8F5"),
        ["DecoForeground_EAFEFF"] = new(DecorativeRole.Foreground, "#EAFEFF"),
        ["DecoBorder_B0F7FF"] = new(DecorativeRole.Border, "#B0F7FF"),
        ["DecoBorder_1A7488"] = new(DecorativeRole.Border, "#1A7488"),
        ["DecoBorder_D4B3FF"] = new(DecorativeRole.Border, "#D4B3FF"),
        ["DecoForeground_F5ECFF"] = new(DecorativeRole.Foreground, "#F5ECFF"),
        ["DecoBorder_E9D6FF"] = new(DecorativeRole.Border, "#E9D6FF"),
        ["DecoBorder_653C9E"] = new(DecorativeRole.Border, "#653C9E"),
        ["DecoBackground_25334C61"] = new(DecorativeRole.Background, "#25334C61"),
        ["DecoBorder_466987"] = new(DecorativeRole.Border, "#466987"),
        ["DecoBorder_385B73"] = new(DecorativeRole.Border, "#385B73"),
        ["DecoBorder_E39399"] = new(DecorativeRole.Border, "#E39399"),
        ["DecoBorder_8FE0B0"] = new(DecorativeRole.Border, "#8FE0B0"),
        ["DecoBorder_C4F1D9"] = new(DecorativeRole.Border, "#C4F1D9"),
        ["DecoBackground_B8162638"] = new(DecorativeRole.Background, "#B8162638"),
        ["DecoBorder_4E6E8BA8"] = new(DecorativeRole.Border, "#4E6E8BA8"),
        ["DecoBorder_62D8FF"] = new(DecorativeRole.Border, "#62D8FF"),
        ["DecoBorder_B0EEFF"] = new(DecorativeRole.Border, "#B0EEFF"),
        ["DecoBorder_365C78"] = new(DecorativeRole.Border, "#365C78"),
        ["DecoBorder_365A75"] = new(DecorativeRole.Border, "#365A75"),
        ["DecoBackground_E8152B42"] = new(DecorativeRole.Background, "#E8152B42"),
        ["DecoBorder_5EC6EB"] = new(DecorativeRole.Border, "#5EC6EB"),
        ["DecoBorder_315A7A"] = new(DecorativeRole.Border, "#315A7A"),
        ["DecoBackground_312C1C42"] = new(DecorativeRole.Background, "#312C1C42"),
        ["DecoBorder_785194"] = new(DecorativeRole.Border, "#785194"),
        ["DecoBackground_100A1724"] = new(DecorativeRole.Background, "#100A1724"),
        ["DecoBorder_193A5268"] = new(DecorativeRole.Border, "#193A5268"),
        ["DecoBackground_3A35234A"] = new(DecorativeRole.Background, "#3A35234A"),
        ["DecoBorder_9767B2"] = new(DecorativeRole.Border, "#9767B2"),
        ["DecoBackground_503B2455"] = new(DecorativeRole.Background, "#503B2455"),
        ["DecoBorder_E676FF"] = new(DecorativeRole.Border, "#E676FF"),
        ["DecoBackground_E3141A22"] = new(DecorativeRole.Background, "#E3141A22"),
        ["DecoBorder_5D3D46"] = new(DecorativeRole.Border, "#5D3D46"),
        ["DecoForeground_C9B9BF"] = new(DecorativeRole.Foreground, "#C9B9BF"),
        ["DecoBackground_6B44231D"] = new(DecorativeRole.Background, "#6B44231D"),
        ["DecoBorder_FF8058"] = new(DecorativeRole.Border, "#FF8058"),
        ["DecoForeground_FFD4C6"] = new(DecorativeRole.Foreground, "#FFD4C6"),
        ["DecoBackground_59412318"] = new(DecorativeRole.Background, "#59412318"),
        ["DecoBorder_C78135"] = new(DecorativeRole.Border, "#C78135"),
        ["DecoForeground_FFD277"] = new(DecorativeRole.Foreground, "#FFD277"),
        ["DecoBackground_79552D18"] = new(DecorativeRole.Background, "#79552D18"),
        ["DecoBorder_FFB65A"] = new(DecorativeRole.Border, "#FFB65A"),
        ["DecoBorder_4B9DC4"] = new(DecorativeRole.Border, "#4B9DC4"),
        ["DecoBorder_426E8B"] = new(DecorativeRole.Border, "#426E8B"),
        ["DecoBackground_33243B4C"] = new(DecorativeRole.Background, "#33243B4C"),
        ["DecoBorder_4F81A3"] = new(DecorativeRole.Border, "#4F81A3"),
        ["DecoBackground_241B4A2B"] = new(DecorativeRole.Background, "#241B4A2B"),
        ["DecoBorder_437E58"] = new(DecorativeRole.Border, "#437E58"),
        ["DecoBorder_4A83A8"] = new(DecorativeRole.Border, "#4A83A8"),
        ["DecoBorder_9A633B"] = new(DecorativeRole.Border, "#9A633B"),
        ["DecoBorder_4A7593"] = new(DecorativeRole.Border, "#4A7593"),
        ["DecoBorder_7BE0FF"] = new(DecorativeRole.Border, "#7BE0FF"),
        ["DecoBorder_5AB6D9"] = new(DecorativeRole.Border, "#5AB6D9"),
        ["DecoBorder_A77639"] = new(DecorativeRole.Border, "#A77639"),
        ["DecoForeground_FFE3AD"] = new(DecorativeRole.Foreground, "#FFE3AD"),
        ["DecoBorder_FFC864"] = new(DecorativeRole.Border, "#FFC864"),
        ["DecoBackground_173049"] = new(DecorativeRole.Background, "#173049"),
        ["DecoBorder_152B3D"] = new(DecorativeRole.Border, "#152B3D"),
        ["DecoGlowColor_49BFFF"] = new(DecorativeRole.GlowColor, "#49BFFF"),
        ["DecoGlowColor_8CE8FF"] = new(DecorativeRole.GlowColor, "#8CE8FF"),
        ["DecoGlowColor_C8F0FF"] = new(DecorativeRole.GlowColor, "#C8F0FF"),
        ["DecoGlowColor_6CE8FF"] = new(DecorativeRole.GlowColor, "#6CE8FF"),
        ["DecoGlowColor_FF704D"] = new(DecorativeRole.GlowColor, "#FF704D"),
        ["DecoGlowColor_52C4FF"] = new(DecorativeRole.GlowColor, "#52C4FF"),
        ["DecoBorder_12241A"] = new(DecorativeRole.Border, "#12241A"),
        ["DecoBackground_0B1712"] = new(DecorativeRole.Background, "#0B1712"),
        ["DecoBorder_F4C542"] = new(DecorativeRole.Border, "#F4C542"),
        ["DecoBackground_14D4A017"] = new(DecorativeRole.Background, "#14D4A017"),
        ["DecoBackground_07110A"] = new(DecorativeRole.Background, "#07110A"),
        ["DecoBorder_214C2A"] = new(DecorativeRole.Border, "#214C2A"),
        ["DecoBorder_0B141B"] = new(DecorativeRole.Border, "#0B141B"),
        ["DecoBackground_9F7AEA"] = new(DecorativeRole.Background, "#9F7AEA"),
        ["DecoBackground_664A5568"] = new(DecorativeRole.Background, "#664A5568"),
        ["DecoBorder_E2E8F0"] = new(DecorativeRole.Border, "#E2E8F0"),
        ["DecoForeground_CBD5E0"] = new(DecorativeRole.Foreground, "#CBD5E0"),
        ["DecoForeground_9F7AEA"] = new(DecorativeRole.Foreground, "#9F7AEA"),
        ["DecoBackground_0A1118"] = new(DecorativeRole.Background, "#0A1118"),
    };

    // Whole BoxShadow values (a shadow list cannot take a resource per colour), by the same rules.
    public static readonly IReadOnlyDictionary<string, string> Shadows = new Dictionary<string, string>
    {
        ["DecoShadow_1"] = "0 0 15 1 #6649BFFF",
        ["DecoShadow_2"] = "0 0 11 0 #5549BFFF",
        ["DecoShadow_3"] = "0 0 18 2 #7749BFFF",
        ["DecoShadow_4"] = "0 0 10 0 #35000000",
        ["DecoShadow_5"] = "0 0 16 0 #3835B9FF",
        ["DecoShadow_6"] = "0 0 14 1 #7749BFFF",
        ["DecoShadow_7"] = "0 8 18 0 #42000000",
        ["DecoShadow_8"] = "0 0 18 1 #6049BFFF",
        ["DecoShadow_9"] = "-8 9 18 0 #5B35D5E8, 8 9 18 0 #5B35D5E8, 0 3 7 0 #7A64E7FF, 0 10 22 0 #55000000",
        ["DecoShadow_10"] = "0 0 17 1 #5849CFFF, 0 7 14 0 #55000000",
        ["DecoShadow_11"] = "0 0 18 1 #5A61CFFF",
        ["DecoShadow_12"] = "0 0 30 0 #3B2F9FEA, 0 14 34 0 #50000000",
        ["DecoShadow_13"] = "-3 -3 11 0 #2548BFFF, 0 0 12 0 #2435BFFF, 0 10 26 0 #5A000000",
        ["DecoShadow_14"] = "0 0 15 0 #42E676FF",
        ["DecoShadow_15"] = "-3 -3 12 0 #2548BFFF, 0 9 22 0 #56000000",
        ["DecoShadow_16"] = "-3 -3 13 0 #3B35D5E8, 0 9 22 0 #56000000",
        ["DecoShadow_17"] = "0 0 10 0 #2535BFFF",
        ["DecoShadow_18"] = "-3 -3 13 0 #2E35D5E8, 0 10 24 0 #59000000",
        ["DecoShadow_19"] = "-3 -3 12 0 #31FF9558, 0 10 24 0 #59000000",
        ["DecoShadow_20"] = "0 5 14 0 #40000000",
        ["DecoShadow_21"] = "0 0 18 1 #6049BFFF, 0 8 20 0 #55000000",
    };

    // The colour a drawn control (the history charts) asks for: its themed resource, or the Dark value before a theme
    // is applied.
    public static Color Resolve(Avalonia.Controls.Control control, string key, string fallback) =>
        control.TryFindResource(key, control.ActualThemeVariant, out var value) ? value switch
        {
            Color color => color,
            ISolidColorBrush brush => brush.Color,
            _ => Color.Parse(fallback),
        } : Color.Parse(fallback);

    public static double Luminance(Color c)
    {
        static double Channel(byte v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
        return 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
    }

    private static bool IsDark(Color c) => Luminance(c) < 0.18;
    private static Color Opaque(Color c) => Color.FromRgb(c.R, c.G, c.B);

    // mode: a normalized ThemeCatalog mode; variant: the Dark or Light palette it resolves to; structural: that
    // palette's structural colours (Border, Text, Bg2...).
    public static Color For(DecorativeColor decorative, string mode, string variant, Func<string, Color> structural)
    {
        var c = Color.Parse(decorative.Dark);
        if (mode == "HighContrast")
            return decorative.Role switch
            {
                DecorativeRole.Border => structural("Border"),
                DecorativeRole.Foreground => structural("Text"),
                // The page background: black in MystTiq's own palette, the window colour of a Windows contrast theme.
                DecorativeRole.Background => IsDark(c) ? ThemeColorMath.WithAlpha(structural("Bg1"), c.A) : c,
                _ => Avalonia.Media.Colors.Transparent,
            };
        if (variant == "Light")
            return decorative.Role switch
            {
                DecorativeRole.Background => IsDark(c) ? ThemeColorMath.WithAlpha(ThemeColorMath.Blend(Opaque(c), structural("Bg2"), 0.85), c.A) : c,
                DecorativeRole.Border => IsDark(c)
                    ? ThemeColorMath.WithAlpha(ThemeColorMath.Blend(Opaque(c), structural("Border"), 0.7), c.A)
                    : ThemeColorMath.Darken(c, 0.35),
                DecorativeRole.Foreground => ThemeColorMath.Blend(Opaque(c), structural("Text"), 0.75),
                _ => ThemeColorMath.WithAlpha(c, (byte)(c.A / 2)),
            };
        if (mode == "Midnight" && decorative.Role == DecorativeRole.Background && IsDark(c))
            return ThemeColorMath.WithAlpha(ThemeColorMath.Darken(Opaque(c), 0.5), c.A);
        return c;
    }

    public static BoxShadows ShadowFor(string text, string mode, string variant)
    {
        if (mode == "HighContrast") return default;
        var parsed = BoxShadows.Parse(text);
        if (variant != "Light") return parsed;
        var softened = new List<BoxShadow>();
        foreach (var shadow in parsed)
        {
            var s = shadow;
            s.Color = ThemeColorMath.WithAlpha(s.Color, (byte)(s.Color.A / 2));
            softened.Add(s);
        }
        return softened.Count == 0 ? default : new BoxShadows(softened[0], softened.Skip(1).ToArray());
    }

    public static void Apply(IResourceDictionary resources, string mode, string variant, Func<string, Color> structural)
    {
        foreach (var (key, decorative) in Colors)
        {
            var color = For(decorative, mode, variant, structural);
            // A drop-shadow effect takes a Color; everything else a brush.
            resources[key] = decorative.Role == DecorativeRole.GlowColor ? color : new SolidColorBrush(color);
        }
        foreach (var (key, text) in Shadows)
            resources[key] = ShadowFor(text, mode, variant);
    }
}