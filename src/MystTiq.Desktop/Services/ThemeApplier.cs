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
    public static void Apply(string accentTheme, string variant)
    {
        if (Application.Current is not { } app) return;
        if (!ThemeCatalog.AccentThemes.Contains(accentTheme)) accentTheme = ThemeCatalog.AccentThemes[0];
        if (!ThemeCatalog.Variants.Contains(variant)) variant = ThemeCatalog.Variants[0];

        foreach (var (key, byVariant) in ThemeCatalog.Structural)
            app.Resources[key] = byVariant[variant];

        foreach (var (key, byVariant) in ThemeCatalog.Semantic)
            app.Resources[key] = byVariant[variant];

        foreach (var (key, byTheme) in ThemeCatalog.Accent)
            app.Resources[key] = byTheme[accentTheme][variant];

        foreach (var (key, byTheme) in ThemeCatalog.GradientStops)
            app.Resources[key] = byTheme[accentTheme][variant];

        // v0.7.55.0: Central Theme System Completion. Computes, rather than hand-authors, the
        // border/glow/card-gradient resources for every page-accent and status-glow color in
        // DesignSystem.axaml (accentHome/Server/World/.../glowGreen/Red/Amber/Purple/...), from
        // ThemeCatalog's own existing Accent/Semantic base colors -- see ThemeCatalog's own comment
        // on DerivedColorNames for why this has to be formula-driven rather than manually tuned
        // per (color x theme x variant) combination. Card{Name}Gradient overwrites the SAME resource
        // keys DesignSystem.axaml's <Styles.Resources> already declares statically (CardGreenGradient
        // etc.) -- Application-level resources already proven to win over StyleInclude-declared ones
        // for every other resource this method overwrites, so no XAML reference needs to change.
        var structuralBorder = ThemeCatalog.Structural["Border"][variant];
        foreach (var name in ThemeCatalog.DerivedColorNames)
        {
            var baseColor = ThemeCatalog.ResolveDerivedBaseColor(name, accentTheme, variant);

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

            app.Resources[$"Card{name}Gradient"] = BuildCardGradient(baseColor, variant);

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
            app.Resources[$"Context{name}Gradient"] = BuildContextGradient(ThemeCatalog.ResolveDerivedBaseColor(name, accentTheme, variant), variant);

        // Generic (non-page-accent) glass sheen family -- GlassOptionHoverGradient/PrimaryGlass*
        // read as the same "general interactive" blue highlight v0.7.48.0 already unified onto
        // BlueAccentHighlightBrush elsewhere, just not yet converted; Success/Danger tie to the
        // matching Semantic color.
        var blueBase = ThemeCatalog.Accent["Blue"][accentTheme][variant];
        var greenBase = ThemeCatalog.Semantic["Green"][variant];
        var redBase = ThemeCatalog.Semantic["Red"][variant];
        app.Resources["GlassOptionHoverGradient"] = BuildGlassSheen(blueBase, variant, bright: false);
        app.Resources["PrimaryGlassGradient"] = BuildGlassSheen(blueBase, variant, bright: false);
        app.Resources["PrimaryGlassHoverGradient"] = BuildGlassSheen(blueBase, variant, bright: true);
        app.Resources["SuccessGlassGradient"] = BuildGlassSheen(greenBase, variant, bright: false);
        app.Resources["SuccessGlassHoverGradient"] = BuildGlassSheen(greenBase, variant, bright: true);
        app.Resources["DangerGlassGradient"] = BuildGlassSheen(redBase, variant, bright: false);
        app.Resources["DangerGlassHoverGradient"] = BuildGlassSheen(redBase, variant, bright: true);

        // Structural (not page-accent-tinted) glass surfaces: the nav sidebar's selected/hover
        // states and the Dashboard's atmosphere-image overlay.
        app.Resources["NavGlassSurfaceGradient"] = BuildNavGlass(variant, NavGlassKind.Surface);
        app.Resources["NavSelectedGlassGradient"] = BuildNavGlass(variant, NavGlassKind.Selected);
        app.Resources["NavHoverGlassGradient"] = BuildNavGlass(variant, NavGlassKind.Hover);
        app.Resources["NavSelectedHoverGlassGradient"] = BuildNavGlass(variant, NavGlassKind.SelectedHover);
        app.Resources["DashboardGlassGradient"] = BuildNavGlass(variant, NavGlassKind.Dashboard);

        // Backups page's three accent cards (Protection=Green, Storage=Blue, Retention=Orange) plus
        // its corner-badge context gradient (Amber) -- near-opaque tinted glass, same structural
        // family as Card{Name}Gradient above, just with its own base-color mapping since "Backup"
        // itself has no single identity color.
        app.Resources["BackupProtectionGradient"] = BuildCardGradient(greenBase, variant);
        app.Resources["BackupStorageGradient"] = BuildCardGradient(blueBase, variant);
        app.Resources["BackupRetentionGradient"] = BuildCardGradient(ThemeCatalog.Accent["Orange"][accentTheme][variant], variant);
        app.Resources["BackupContextGradient"] = BuildContextGradient(ThemeCatalog.Semantic["Amber"][variant], variant);

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
        const double neutralTintAmount = 0.16;
        var neutralTintTarget = variant == "Light" ? ThemeColorMath.Lighten(blueBase, 0.55) : blueBase;
        Color Tint(Color original) => ThemeColorMath.WithAlpha(ThemeColorMath.Blend(original, neutralTintTarget, neutralTintAmount), original.A);

        app.Resources["ButtonGradientStop0"] = Tint(ThemeCatalog.GradientStops["ButtonGradientStop0"][accentTheme][variant]);
        app.Resources["ButtonGradientStop1"] = Tint(ThemeCatalog.GradientStops["ButtonGradientStop1"][accentTheme][variant]);
        app.Resources["ButtonGradientStop2"] = Tint(ThemeCatalog.GradientStops["ButtonGradientStop2"][accentTheme][variant]);
        app.Resources["ButtonGradientHoverStop0"] = Tint(ThemeCatalog.GradientStops["ButtonGradientHoverStop0"][accentTheme][variant]);
        app.Resources["ButtonGradientHoverStop1"] = Tint(ThemeCatalog.GradientStops["ButtonGradientHoverStop1"][accentTheme][variant]);
        app.Resources["ButtonGradientHoverStop2"] = Tint(ThemeCatalog.GradientStops["ButtonGradientHoverStop2"][accentTheme][variant]);
        app.Resources["ButtonGradientPressedStop0"] = Tint(ThemeCatalog.GradientStops["ButtonGradientPressedStop0"][accentTheme][variant]);
        app.Resources["ButtonGradientPressedStop1"] = Tint(ThemeCatalog.GradientStops["ButtonGradientPressedStop1"][accentTheme][variant]);
        app.Resources["ButtonGradientPressedStop2"] = Tint(ThemeCatalog.GradientStops["ButtonGradientPressedStop2"][accentTheme][variant]);

        app.Resources["CardGradientStop0"] = Tint(ThemeCatalog.GradientStops["CardGradientStop0"][accentTheme][variant]);
        app.Resources["CardGradientStop1"] = Tint(ThemeCatalog.GradientStops["CardGradientStop1"][accentTheme][variant]);
        app.Resources["CardGradientStop2"] = Tint(ThemeCatalog.GradientStops["CardGradientStop2"][accentTheme][variant]);
        app.Resources["CardGradientStop3"] = Tint(ThemeCatalog.GradientStops["CardGradientStop3"][accentTheme][variant]);

        // Generic list-row states (dataRow) -- previously hardcoded Background/BorderBrush literals
        // directly on the Style selector rather than a named gradient resource; DesignSystem.axaml's
        // Border.dataRow styles now bind through these instead.
        var isDark = variant != "Light";
        var rowBg2 = ThemeCatalog.Structural["Bg2"][variant];
        app.Resources["DataRowBg"] = new SolidColorBrush(ThemeColorMath.WithAlpha(rowBg2, isDark ? (byte)0x60 : (byte)0x20));
        app.Resources["DataRowBorderBrush"] = new SolidColorBrush(ThemeColorMath.Blend(blueBase, structuralBorder, 0.65));
        app.Resources["DataRowHoverBg"] = new SolidColorBrush(ThemeColorMath.WithAlpha(ThemeColorMath.Blend(blueBase, rowBg2, 0.75), isDark ? (byte)0xA0 : (byte)0x40));
        app.Resources["DataRowHoverBorderBrush"] = new SolidColorBrush(ThemeColorMath.Blend(blueBase, structuralBorder, 0.3));
        var amberBase = ThemeCatalog.Semantic["Amber"][variant];
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
        var tabStop0 = ThemeCatalog.GradientStops["ServerTabGradientStop0"][accentTheme][variant];
        var tabStop1 = ThemeCatalog.GradientStops["ServerTabGradientStop1"][accentTheme][variant];
        var tabStop2 = ThemeCatalog.GradientStops["ServerTabGradientStop2"][accentTheme][variant];
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

        app.RequestedThemeVariant = variant == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    private enum NavGlassKind { Surface, Selected, Hover, SelectedHover, Dashboard }

    // Mirrors ThemeCatalog.Structural's own Card/CardStrong Dark-vs-Light approach (blend toward
    // black for a dark glass tint, toward white for a light one) but tinted by the family's own
    // base color instead of a neutral gray -- the same "glass card, colored" look every one of
    // these gradients already has, just computed instead of hand-picked per combination.
    private static LinearGradientBrush BuildCardGradient(Color baseColor, string variant)
    {
        var isDark = variant != "Light";
        var stop0 = isDark ? ThemeColorMath.Darken(baseColor, 0.55) : ThemeColorMath.Lighten(baseColor, 0.80);
        var stop1 = isDark ? ThemeColorMath.Darken(baseColor, 0.70) : ThemeColorMath.Lighten(baseColor, 0.87);
        var stop2 = isDark ? ThemeColorMath.Darken(baseColor, 0.88) : ThemeColorMath.Lighten(baseColor, 0.94);
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
    private static LinearGradientBrush BuildNavGlass(string variant, NavGlassKind kind)
    {
        var isDark = variant != "Light";
        var border = ThemeCatalog.Structural["Border"][variant];
        var bg2 = ThemeCatalog.Structural["Bg2"][variant];
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
