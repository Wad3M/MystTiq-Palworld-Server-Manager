using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace MystTiq.Desktop.Services;

// Applies an (accentTheme, variant) selection by writing every resource key
// ThemeCatalog.cs knows about directly onto Application.Current.Resources. Every consumer already
// binds through DynamicResource (colors, brushes, gradients), so this re-flows the whole UI live --
// no app restart, no ResourceDictionary swap, no per-page code.
public static class ThemeApplier
{
    // v0.8.16.0: whether the operating system is set to light. A property so the headless harness can stand in for the
    // platform; the real app asks Avalonia's platform settings (Windows' app mode, the Linux desktop's color scheme).
    public static Func<bool> SystemPrefersLight { get; set; } = () =>
        Application.Current?.PlatformSettings?.GetColorValues().ThemeVariant == Avalonia.Platform.PlatformThemeVariant.Light;

    // The mode last applied (normalized), for the System mode's live follow.
    public static string CurrentMode { get; private set; } = "Dark";

    // v0.8.25.0: the Windows contrast theme's colours when one is on, else null (a property so the headless harness can
    // stand in for Windows), and the one in use after the last Apply.
    public static Func<SystemContrastPalette?> SystemContrast { get; set; } = SystemContrastPalette.ReadFromSystem;
    public static SystemContrastPalette? CurrentContrast { get; private set; }

    // The palette a mode is shown with (Light or Dark), a Windows contrast theme included: the art and the Light check
    // follow it too, so a light contrast theme (Desert) gets the day art.
    public static string ResolveVariant(string? mode)
    {
        var normalized = ThemeCatalog.NormalizeMode(mode);
        var contrast = normalized is "HighContrast" or "System" ? SystemContrast() : null;
        return contrast is not null ? (contrast.IsLight ? "Light" : "Dark") : ThemeCatalog.BaseVariant(normalized, SystemPrefersLight());
    }

    // v0.8.16.0: "variant" is now a mode (ThemeCatalog.Modes): Dark and Light as before, or Midnight, HighContrast or
    // System, each resolved to the Dark or Light palette plus that mode's own overrides.
    public static void Apply(string accentTheme, string variant)
    {
        if (Application.Current is not { } app) return;
        if (!ThemeCatalog.AccentThemes.Contains(accentTheme)) accentTheme = ThemeCatalog.AccentThemes[0];
        var mode = ThemeCatalog.NormalizeMode(variant);
        CurrentMode = mode;
        // v0.8.25.0: a Windows contrast theme wins in High contrast and in Follow the system: its colours replace
        // MystTiq's own high-contrast palette, and its window colour decides the base palette (Desert is light).
        var contrast = mode is "HighContrast" or "System" ? SystemContrast() : null;
        if (contrast is not null) mode = "HighContrast";
        CurrentContrast = contrast;
        variant = contrast is not null ? (contrast.IsLight ? "Light" : "Dark") : ThemeCatalog.BaseVariant(mode, SystemPrefersLight());
        var highContrast = mode == "HighContrast";
        var midnight = mode == "Midnight";
        // High contrast's own surfaces: black and dark greys, or the contrast theme's window colour stepped toward its text.
        Color HcSurface(double step, string ownHex) => contrast?.Shade(step) ?? (ownHex == "#000000" ? Colors.Black : Color.Parse(ownHex));

        Color S(string key) => contrast is not null && contrast.Structural.TryGetValue(key, out var sc) ? sc
            : ThemeCatalog.ModeStructural.TryGetValue(mode, out var o) && o.TryGetValue(key, out var c) ? c : ThemeCatalog.Structural[key][variant];
        // Status colours keep their meaning; on a light contrast theme they come from the Light palette so they stay readable.
        Color Sem(string key) => contrast is { IsLight: true } ? ThemeCatalog.Semantic[key]["Light"]
            : ThemeCatalog.ModeSemantic.TryGetValue(mode, out var o) && o.TryGetValue(key, out var c) ? c : ThemeCatalog.Semantic[key][variant];
        // High contrast lifts the five accent hues so they read on black; the other modes keep them. A contrast theme's
        // accents are its own: the highlight for blue (selection, primary), its link colour for the others.
        Color Acc(string key) => contrast is not null ? (key == "Blue" ? contrast.Highlight : contrast.Hotlight)
            : highContrast ? ThemeColorMath.Lighten(ThemeCatalog.Accent[key][accentTheme][variant], 0.3) : ThemeCatalog.Accent[key][accentTheme][variant];
        Color Stop(string key) => ModeStop(key, ThemeCatalog.GradientStops[key][accentTheme][variant], mode, contrast);
        Color Derived(string name) => name switch
        {
            "Blue" or "Cyan" or "Violet" or "Magenta" or "Orange" => Acc(name),
            "Green" or "Amber" or "Red" => Sem(name),
            "Purple" => Acc("Violet"),
            "DarkGreen" => ThemeColorMath.Darken(Sem("Green"), 0.32),
            "Neutral" => ThemeColorMath.Blend(S("Border"), S("Muted"), 0.5),
            _ => Acc("Blue"),
        };

        foreach (var key in ThemeCatalog.Structural.Keys)
            app.Resources[key] = S(key);

        foreach (var key in ThemeCatalog.Semantic.Keys)
            app.Resources[key] = Sem(key);

        foreach (var key in ThemeCatalog.Accent.Keys)
            app.Resources[key] = Acc(key);

        foreach (var key in ThemeCatalog.GradientStops.Keys)
            app.Resources[key] = Stop(key);

        app.Resources["CardBorderBrush"] = new SolidColorBrush(contrast?.WindowText ?? ThemeCatalog.CardBorder(mode));

        // v0.7.55.0: Central Theme System Completion. Computes, rather than hand-authors, the
        // border/glow/card-gradient resources for every page-accent and status-glow color in
        // DesignSystem.axaml (accentHome/Server/World/.../glowGreen/Red/Amber/Purple/...), from
        // ThemeCatalog's own existing Accent/Semantic base colors -- see ThemeCatalog's own comment
        // on DerivedColorNames for why this has to be formula-driven rather than manually tuned
        // per (color x theme x variant) combination. Card{Name}Gradient overwrites the SAME resource
        // keys DesignSystem.axaml's <Styles.Resources> already declares statically (CardGreenGradient
        // etc.) -- Application-level resources already proven to win over StyleInclude-declared ones
        // for every other resource this method overwrites, so no XAML reference needs to change.
        var structuralBorder = S("Border");
        var pageBase = S("Bg1");
        app.Resources["PageArtReadabilityGradient"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
            GradientStops = new GradientStops
            {
                new GradientStop(ThemeColorMath.WithAlpha(pageBase, 0xE8), 0),
                new GradientStop(ThemeColorMath.WithAlpha(pageBase, 0xC0), 0.45),
                new GradientStop(ThemeColorMath.WithAlpha(pageBase, 0x18), 1)
            }
        };
        foreach (var name in ThemeCatalog.DerivedColorNames)
        {
            var baseColor = Derived(name);

            app.Resources[$"{name}AccentBorderBrush"] = new SolidColorBrush(ThemeColorMath.Blend(baseColor, structuralBorder, 0.55));
            // A bright, lightened version of the same base color -- distinct purpose from the muted
            // BorderBrush above: this is for hover/focus/checked highlights that need to visibly pop
            // (matching the app's existing #91E4FF/#75C8FF/#72D0FF-style bright highlight literals),
            // not the passive, low-contrast accent tint the card-flare borders use.
            app.Resources[$"{name}AccentHighlightBrush"] = new SolidColorBrush(ThemeColorMath.Lighten(baseColor, 0.5));

            app.Resources[$"{name}AccentGlowShadowLow"] = new BoxShadows(
                new BoxShadow { OffsetX = 0, OffsetY = 0, Blur = 14, Spread = 0, Color = ThemeColorMath.WithAlpha(baseColor, 0x30) },
                [new BoxShadow { OffsetX = 0, OffsetY = 8, Blur = 20, Spread = 0, Color = Color.FromArgb(0x50, 0, 0, 0) }]);

            app.Resources[$"{name}AccentGlowShadowHigh"] = new BoxShadows(
                new BoxShadow { OffsetX = 0, OffsetY = 0, Blur = 24, Spread = 2, Color = ThemeColorMath.WithAlpha(baseColor, 0x5A) },
                [new BoxShadow { OffsetX = 0, OffsetY = 9, Blur = 24, Spread = 0, Color = Color.FromArgb(0x58, 0, 0, 0) }]);

            app.Resources[$"Card{name}Gradient"] = highContrast ? new SolidColorBrush(HcSurface(0, "#000000")) : BuildCardGradient(baseColor, variant, deep: midnight);

            // v0.7.56.0: Central Theme System Completion, Remaining Decorative Gradients. GraphFill
            // is the lowest-risk of the ~240 colors deferred from v0.7.48.0 -- a translucent alpha
            // fade to fully transparent naturally overlays whatever background sits under it, so it
            // needs no separate Dark/Light glass-opacity tuning the way an opaque-ish "glass panel"
            // gradient does. Covers all 11 DerivedColorNames for consistency with AccentBorderBrush/
            // Card{Name}Gradient above, even though only Blue/Cyan/Violet/Green/Amber have an existing
            // XAML consumer today.
            app.Resources[$"{name}GraphFill"] = BuildGraphFill(baseColor);
        }

        // Page-accent-tinted "context" cards (ribbonGroup.contextXxx and prototypeActions).
        foreach (var name in new[] { "Blue", "Violet", "Amber", "Magenta", "Orange" })
            app.Resources[$"Context{name}Gradient"] = BuildContextGradient(Derived(name), variant);

        // Generic (non-page-accent) glass sheen family -- GlassOptionHoverGradient/PrimaryGlass*
        // read as the same "general interactive" blue highlight v0.7.48.0 already unified onto
        // BlueAccentHighlightBrush elsewhere, just not yet converted; Success/Danger tie to the
        // matching Semantic color.
        var blueBase = Acc("Blue");
        var greenBase = Sem("Green");
        var redBase = Sem("Red");
        // v0.8.16.0: this sheen is also the sidebar's background, so Midnight builds it from a much darker blue and high
        // contrast makes it a flat dark grey (still distinct from the black page, so hover shows).
        app.Resources["GlassOptionHoverGradient"] = highContrast ? new SolidColorBrush(HcSurface(0.12, "#1E1E1E"))
            : BuildGlassSheen(midnight ? ThemeColorMath.Darken(blueBase, 0.6) : blueBase, variant, bright: false);
        app.Resources["PrimaryGlassGradient"] = BuildGlassSheen(blueBase, variant, bright: false);
        app.Resources["PrimaryGlassHoverGradient"] = BuildGlassSheen(blueBase, variant, bright: true);
        app.Resources["SuccessGlassGradient"] = BuildGlassSheen(greenBase, variant, bright: false);
        app.Resources["SuccessGlassHoverGradient"] = BuildGlassSheen(greenBase, variant, bright: true);
        app.Resources["DangerGlassGradient"] = BuildGlassSheen(redBase, variant, bright: false);
        app.Resources["DangerGlassHoverGradient"] = BuildGlassSheen(redBase, variant, bright: true);

        // Structural (not page-accent-tinted) glass surfaces: the nav sidebar's selected/hover
        // states and the Dashboard's atmosphere-image overlay.
        // High contrast builds the nav glass from a dark grey instead of its white border, or the sidebar would wash out.
        var navBorder = highContrast ? (contrast?.GrayText ?? Color.Parse("#404040")) : S("Border");
        var navBg2 = S("Bg2");
        app.Resources["NavGlassSurfaceGradient"] = BuildNavGlass(variant, NavGlassKind.Surface, navBorder, navBg2);
        app.Resources["NavSelectedGlassGradient"] = BuildNavGlass(variant, NavGlassKind.Selected, navBorder, navBg2);
        app.Resources["NavHoverGlassGradient"] = BuildNavGlass(variant, NavGlassKind.Hover, navBorder, navBg2);
        app.Resources["NavSelectedHoverGlassGradient"] = BuildNavGlass(variant, NavGlassKind.SelectedHover, navBorder, navBg2);
        app.Resources["DashboardGlassGradient"] = BuildNavGlass(variant, NavGlassKind.Dashboard, navBorder, navBg2);

        // Backups page's three accent cards (Protection=Green, Storage=Blue, Retention=Orange) plus
        // its corner-badge context gradient (Amber) -- near-opaque tinted glass, same structural
        // family as Card{Name}Gradient above, just with its own base-color mapping since "Backup"
        // itself has no single identity color.
        app.Resources["BackupProtectionGradient"] = BuildCardGradient(greenBase, variant, deep: midnight || highContrast);
        app.Resources["BackupStorageGradient"] = BuildCardGradient(blueBase, variant, deep: midnight || highContrast);
        app.Resources["BackupRetentionGradient"] = BuildCardGradient(Acc("Orange"), variant, deep: midnight || highContrast);
        app.Resources["BackupContextGradient"] = BuildContextGradient(Sem("Amber"), variant);

        // v0.7.78.0: requested directly ("accent theme should tint everything, not just selection
        // elements") -- ButtonGradient/Hover/Pressed and the base CardGradient were confirmed the
        // two most universally-visible surfaces still identical across all 4 accent themes (their
        // GradientStops Combo entries repeat the same Dark hex four times; only Dark-vs-Light
        // varies). Rather than hand-authoring 4 new theme variants per stop, each stop is nudged
        // toward blueBase (the theme's own identity color, already varying correctly per
        // accentTheme) by a small blend factor -- subtle enough not to hurt readability on a
        // "neutral" surface, but enough to visibly shift hue when the theme changes. Alpha is
        // preserved explicitly since Blend interpolates it too (toward blueBase's own, normally
        // fully-opaque, alpha).
        // v0.7.81.0 bugfix: reported live -- in Light mode, blending toward raw blueBase (a fairly
        // saturated accent hue meant to read well against a DARK neutral) pulled buttons/cards
        // noticeably darker/more saturated than the rest of the Light chrome, especially under
        // Violet/Crimson-leaning accent themes. Light mode now blends toward a lightened copy of
        // blueBase instead, so the result stays a pale tint rather than dragging brightness down.
        // High contrast keeps its surfaces pure black and grey: no tint.
        var neutralTintAmount = highContrast ? 0.0 : 0.16;
        var neutralTintTarget = variant == "Light" ? ThemeColorMath.Lighten(blueBase, 0.55) : blueBase;
        Color Tint(Color original) => ThemeColorMath.WithAlpha(ThemeColorMath.Blend(original, neutralTintTarget, neutralTintAmount), original.A);

        foreach (var key in new[] { "ButtonGradientStop0", "ButtonGradientStop1", "ButtonGradientStop2", "ButtonGradientHoverStop0", "ButtonGradientHoverStop1",
                     "ButtonGradientHoverStop2", "ButtonGradientPressedStop0", "ButtonGradientPressedStop1", "ButtonGradientPressedStop2",
                     "CardGradientStop0", "CardGradientStop1", "CardGradientStop2", "CardGradientStop3" })
            app.Resources[key] = Tint(Stop(key));

        // Generic list-row states (dataRow) -- previously hardcoded Background/BorderBrush literals
        // directly on the Style selector rather than a named gradient resource; DesignSystem.axaml's
        // Border.dataRow styles now bind through these instead.
        var isDark = variant != "Light";
        var rowBg2 = S("Bg2");
        app.Resources["DataRowBg"] = new SolidColorBrush(ThemeColorMath.WithAlpha(rowBg2, isDark ? (byte)0x60 : (byte)0x20));
        app.Resources["DataRowBorderBrush"] = new SolidColorBrush(ThemeColorMath.Blend(blueBase, structuralBorder, 0.65));
        app.Resources["DataRowHoverBg"] = new SolidColorBrush(ThemeColorMath.WithAlpha(ThemeColorMath.Blend(blueBase, rowBg2, 0.75), isDark ? (byte)0xA0 : (byte)0x40));
        app.Resources["DataRowHoverBorderBrush"] = new SolidColorBrush(ThemeColorMath.Blend(blueBase, structuralBorder, 0.3));
        var amberBase = Sem("Amber");
        app.Resources["DataRowSelectedBg"] = new SolidColorBrush(ThemeColorMath.WithAlpha(ThemeColorMath.Blend(amberBase, rowBg2, 0.78), isDark ? (byte)0xB0 : (byte)0x50));
        app.Resources["DataRowSelectedBorderBrush"] = new SolidColorBrush(amberBase);
        app.Resources["DataRowSelectedGlowShadow"] = new BoxShadows(
            new BoxShadow { OffsetX = 0, OffsetY = 0, Blur = 12, Spread = 0, Color = ThemeColorMath.WithAlpha(amberBase, 0x2C) });

        // v0.7.77.0: the one confirmed genuine gap found during a full audit of every hardcoded
        // <GradientStop Color="#..."> literal in DesignSystem.axaml -- ServerTabHoverGradient's
        // three stops were raw hex with no DynamicResource indirection at all, unlike its siblings
        // ServerTabGradient/ServerTabActiveGradient (both already theme-aware via GradientStops).
        // Every OTHER hardcoded literal found in that audit turned out to be a false alarm: either a
        // static-looking value whose parent resource is directly overwritten at the Application
        // level below (Card{Name}Gradient, Nav*Glass*, *GlassGradient, Backup*Gradient,
        // Context{Name}Gradient), or a "*Brush" wrapper whose own Color attribute already references
        // a covered Color resource via DynamicResource internally (confirmed by reading
        // DesignSystem.axaml directly, e.g. TextBrush's Color="{DynamicResource Text}"). Derived here
        // as a lightened version of ServerTabGradient's own three stops (already theme-aware),
        // matching the same base-to-hover brightening relationship already used for
        // Button/Success/Warning's own Hover pairs, rather than hand-picking 24 new literals
        // (4 themes x 2 variants x 3 stops) this project's own history says is unreliable without
        // being able to see the rendered result.
        var tabStop0 = Stop("ServerTabGradientStop0");
        var tabStop1 = Stop("ServerTabGradientStop1");
        var tabStop2 = Stop("ServerTabGradientStop2");
        app.Resources["ServerTabHoverGradient"] = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(ThemeColorMath.WithAlpha(ThemeColorMath.Lighten(tabStop0, 0.18), tabStop0.A), 0),
                new GradientStop(ThemeColorMath.WithAlpha(ThemeColorMath.Lighten(tabStop1, 0.18), tabStop1.A), 0.58),
                new GradientStop(ThemeColorMath.WithAlpha(ThemeColorMath.Lighten(tabStop2, 0.18), tabStop2.A), 1),
            }
        };

        // v0.8.25.0: the last three keyed gradients no mode reached (their Dark literals stayed in every mode):
        // WarningGlassGradient like its Primary/Success/Danger glass siblings, the Restore pair as card surfaces of the
        // orange accent (the hover one brighter, as the other hover pairs).
        app.Resources["WarningGlassGradient"] = BuildGlassSheen(amberBase, variant, bright: false);
        app.Resources["RestoreGradient"] = highContrast ? new SolidColorBrush(HcSurface(0, "#000000")) : BuildCardGradient(Acc("Orange"), variant, deep: midnight);
        app.Resources["RestoreGradientHover"] = highContrast ? new SolidColorBrush(HcSurface(0.12, "#1E1E1E")) : BuildCardGradient(ThemeColorMath.Lighten(Acc("Orange"), 0.15), variant, deep: midnight);

        // v0.8.25.0: the decorative colours that were literals in the styles (glows, shadows, fixed borders).
        DecorativePalette.Apply(app.Resources, mode, variant, S);
        // Button text: white on MystTiq's coloured buttons; a Windows contrast theme's button text on its button face.
        app.Resources["ButtonForegroundBrush"] = new SolidColorBrush(contrast?.ButtonText ?? Colors.White);
        // The success button is pale green on the light palettes (Light, and a light contrast theme): dark text there.
        // White on it read at under 2:1 in Light (found by the v0.8.25.0 contrast-theme render).
        app.Resources["SuccessButtonForegroundBrush"] = new SolidColorBrush(variant == "Light" ? contrast?.ButtonText ?? S("Text") : Colors.White);

        app.RequestedThemeVariant = variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    // v0.8.16.0: Midnight takes the chrome surfaces (command bar, sidebar, status bar, cards, tabs, buttons) toward black,
    // keeping their alpha. High contrast makes them black, with buttons a step of grey so hover and press still show. The
    // accent-coloured stops (primary, success, warning, danger, the selected tab and category) are left as they are.
    private static readonly string[] SurfaceStopPrefixes =
        ["CommandSurfaceGradient", "NavSurfaceGradient", "StatusSurfaceGradient", "CardGradient", "ServerTabGradient", "ButtonGradient"];

    public static Color ModeStop(string key, Color color, string mode, SystemContrastPalette? contrast = null)
    {
        if (!SurfaceStopPrefixes.Any(key.StartsWith)) return color;
        return ThemeCatalog.NormalizeMode(mode) switch
        {
            "Midnight" => ThemeColorMath.WithAlpha(ThemeColorMath.Darken(color, 0.7), color.A),
            // v0.8.25.0: a Windows contrast theme's button face, stepped toward its text on hover and press.
            "HighContrast" when contrast is not null && key.StartsWith("ButtonGradientHover", StringComparison.Ordinal) => ThemeColorMath.Blend(contrast.ButtonFace, contrast.ButtonText, 0.16),
            "HighContrast" when contrast is not null && key.StartsWith("ButtonGradientPressed", StringComparison.Ordinal) => ThemeColorMath.Blend(contrast.ButtonFace, contrast.ButtonText, 0.28),
            "HighContrast" when contrast is not null && key.StartsWith("ButtonGradient", StringComparison.Ordinal) => contrast.ButtonFace,
            "HighContrast" when contrast is not null => contrast.Window,
            "HighContrast" when key.StartsWith("ButtonGradientHover", StringComparison.Ordinal) => Color.Parse("#FF2A2A2A"),
            "HighContrast" when key.StartsWith("ButtonGradientPressed", StringComparison.Ordinal) => Color.Parse("#FF3C3C3C"),
            "HighContrast" when key.StartsWith("ButtonGradient", StringComparison.Ordinal) => Color.Parse("#FF101010"),
            "HighContrast" => Colors.Black,
            _ => color,
        };
    }

    // v0.8.16.0: density, for every tab. DesignSystem.axaml's base Button/TextBox/ComboBox/card/list-row styles read these
    // resources, so a class-specific style (the Ribbon's big buttons, section toggles) keeps its own size either way.
    public static string CurrentDensity { get; private set; } = "Comfortable";

    public static void ApplyDensity(string? density)
    {
        CurrentDensity = ThemeCatalog.NormalizeDensity(density);
        if (Application.Current is not { } app) return;
        var compact = CurrentDensity == "Compact";
        app.Resources["ControlMinHeight"] = compact ? 26d : 31d;
        app.Resources["CardPadding"] = compact ? new Thickness(7) : new Thickness(11);
        app.Resources["InputPadding"] = compact ? new Thickness(7, 2) : new Thickness(9, 4);
        app.Resources["ListItemPadding"] = compact ? new Thickness(4, 1) : new Thickness(4, 3);
    }

    private enum NavGlassKind { Surface, Selected, Hover, SelectedHover, Dashboard }

    // Mirrors ThemeCatalog.Structural's own Card/CardStrong Dark-vs-Light approach (blend toward
    // black for a dark glass tint, toward white for a light one) but tinted by the family's own
    // base color instead of a neutral gray -- the same "glass card, colored" look every one of
    // these gradients already has, just computed instead of hand-picked per combination.
    // v0.8.16.0: deep (Midnight, and the Backups cards in high contrast) takes each stop further toward black.
    private static LinearGradientBrush BuildCardGradient(Color baseColor, string variant, bool deep = false)
    {
        var isDark = variant != "Light";
        var stop0 = isDark ? ThemeColorMath.Darken(baseColor, deep ? 0.78 : 0.55) : ThemeColorMath.Lighten(baseColor, 0.80);
        var stop1 = isDark ? ThemeColorMath.Darken(baseColor, deep ? 0.88 : 0.70) : ThemeColorMath.Lighten(baseColor, 0.87);
        var stop2 = isDark ? ThemeColorMath.Darken(baseColor, deep ? 0.96 : 0.88) : ThemeColorMath.Lighten(baseColor, 0.94);
        var alpha = isDark ? (byte)0xF0 : (byte)0xF5;
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(ThemeColorMath.WithAlpha(stop0, alpha), 0),
                new GradientStop(ThemeColorMath.WithAlpha(stop1, alpha), 0.32),
                new GradientStop(ThemeColorMath.WithAlpha(stop2, alpha), 1),
            }
        };
    }

    // v0.7.56.0: a simple top-to-bottom fade to full transparency -- used by area-chart fills.
    // No Dark/Light branching needed: alpha-blending onto full transparency naturally reads
    // correctly over any background, unlike the opaque-ish "glass panel" gradients below.
    private static LinearGradientBrush BuildGraphFill(Color baseColor) => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(ThemeColorMath.WithAlpha(baseColor, 0x70), 0),
            new GradientStop(ThemeColorMath.WithAlpha(baseColor, 0x28), 0.62),
            new GradientStop(ThemeColorMath.WithAlpha(baseColor, 0x00), 1),
        }
    };

    // v0.7.56.0: a translucent 2-stop diagonal fade used by small accent-tinted corner/context
    // cards (ribbonGroup.contextXxx, MOD Library's prototypeActions, Backups' corner badge).
    // Dark fades toward a near-black tinted corner (mirroring the existing hand-authored
    // ContextBlueGradient etc.); Light fades toward a near-white tinted corner instead, since a
    // dark lower-right stop would read as a stray dark smear on a light background.
    private static LinearGradientBrush BuildContextGradient(Color baseColor, string variant)
    {
        var isDark = variant != "Light";
        var stop0 = isDark ? ThemeColorMath.Darken(baseColor, 0.45) : ThemeColorMath.Lighten(baseColor, 0.55);
        var stop1 = isDark ? ThemeColorMath.Darken(baseColor, 0.88) : ThemeColorMath.Lighten(baseColor, 0.90);
        var alpha0 = isDark ? (byte)0x46 : (byte)0x50;
        var alpha1 = isDark ? (byte)0xCA : (byte)0xE6;
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(ThemeColorMath.WithAlpha(stop0, alpha0), 0),
                new GradientStop(ThemeColorMath.WithAlpha(stop1, alpha1), 1),
            }
        };
    }

    // v0.7.56.0: the "directional option sheen" family (GlassOptionHoverGradient, PrimaryGlass*,
    // SuccessGlass*, DangerGlass*) -- a bright translucent lip fading to a deeper tinted face,
    // matching the structure the app's own hand-authored versions of these already use. Dark stays
    // close to that existing look (lip lightened, face darkened); Light inverts the direction (lip
    // pushed brighter still, face only lightly tinted) so the "glass" reads as glass over a light
    // backdrop instead of producing a muddy mid-tone.
    // v0.7.81.0 bugfix: reported live -- the checked category tab (bright:false, via
    // GlassOptionHoverGradient) read as a "weird" muddy purple/indigo in Light mode rather than a
    // proper light tone. Root cause: mid's Light lighten amount (0.15) barely moved off baseColor,
    // and its alpha (0x7A ~ 48%) was high enough to dominate over the pale backdrop rather than
    // blend into it. mid's Light lighten now matches face's own 0.65 more closely (0.45, still a
    // bit deeper for the mid-stop's own gradient role) and both Light alphas are reduced so more of
    // the actual light background shows through instead of a half-opaque saturated hue sitting on
    // top of it.
    private static LinearGradientBrush BuildGlassSheen(Color baseColor, string variant, bool bright)
    {
        var isDark = variant != "Light";
        var lipAmt = bright ? 0.72 : 0.55;
        var lip = isDark ? ThemeColorMath.Lighten(baseColor, lipAmt) : ThemeColorMath.Lighten(baseColor, Math.Min(0.94, lipAmt + 0.2));
        var mid = isDark ? ThemeColorMath.Darken(baseColor, 0.25) : ThemeColorMath.Lighten(baseColor, bright ? 0.55 : 0.45);
        var face = isDark ? ThemeColorMath.Darken(baseColor, 0.55) : ThemeColorMath.Lighten(baseColor, 0.72);
        var lipAlpha = bright ? (byte)0xC4 : (byte)0x9A;
        var midAlpha = isDark ? (bright ? (byte)0x9E : (byte)0x7A) : (bright ? (byte)0x70 : (byte)0x50);
        var faceAlpha = isDark ? (bright ? (byte)0x8A : (byte)0x68) : (bright ? (byte)0x60 : (byte)0x44);
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(ThemeColorMath.WithAlpha(lip, lipAlpha), 0),
                new GradientStop(ThemeColorMath.WithAlpha(mid, midAlpha), 0.4),
                new GradientStop(ThemeColorMath.WithAlpha(face, faceAlpha), 1),
            }
        };
    }

    // v0.7.56.0: the nav sidebar's structural (non-page-accent) glass states plus the Dashboard's
    // atmosphere-image overlay -- derived from Structural.Border/Bg2 rather than an accent color,
    // since these represent "this nav row/panel" chrome, not a page identity. Dark keeps close to
    // the existing near-black selected look; Light uses a much lighter, more translucent version
    // of the same structure so selection/hover still reads as a visible lift, not a dark smear.
    private static LinearGradientBrush BuildNavGlass(string variant, NavGlassKind kind, Color border, Color bg2)
    {
        var isDark = variant != "Light";
        var lip = isDark ? ThemeColorMath.Lighten(border, 0.35) : ThemeColorMath.Lighten(border, 0.55);
        var deep = isDark ? ThemeColorMath.Darken(bg2, 0.6) : ThemeColorMath.Lighten(bg2, 0.6);

        (Color start, Color end, byte startAlpha, byte endAlpha) = kind switch
        {
            NavGlassKind.Surface => (lip, deep, isDark ? (byte)0x8C : (byte)0x50, isDark ? (byte)0x4A : (byte)0xC0),
            NavGlassKind.Selected => (deep, deep, isDark ? (byte)0xD0 : (byte)0x30, isDark ? (byte)0x9A : (byte)0xB0),
            NavGlassKind.Hover => (lip, deep, isDark ? (byte)0xA8 : (byte)0x60, isDark ? (byte)0x2A : (byte)0x90),
            NavGlassKind.SelectedHover => (lip, deep, isDark ? (byte)0xE0 : (byte)0x70, isDark ? (byte)0xA8 : (byte)0xC0),
            NavGlassKind.Dashboard => (lip, deep, isDark ? (byte)0x4C : (byte)0x30, isDark ? (byte)0x26 : (byte)0x60),
            _ => (lip, deep, (byte)0x80, (byte)0x40),
        };

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = kind == NavGlassKind.Surface ? new RelativePoint(0, 1, RelativeUnit.Relative) : new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(ThemeColorMath.WithAlpha(start, startAlpha), 0),
                new GradientStop(ThemeColorMath.WithAlpha(end, endAlpha), 1),
            }
        };
    }
}
