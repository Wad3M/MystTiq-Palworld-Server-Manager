using Avalonia.Media;

namespace MystTiq.Desktop.Services;

// Theme/skin data: every resource key that can be swapped at runtime, for every
// (AccentTheme x Variant) combination. ThemeApplier reads this and writes the values directly onto
// Application.Current.Resources; every consumer already binds through DynamicResource, so nothing
// else needs to change to pick up a new theme.
//
// Scope note (see docs/architecture for the themes-skins-personalization-icon-set doc and its full
// rationale): this covers the palette
// resources in Styles/DesignSystem.axaml's <Styles.Resources> block -- the structural surface
// colors, the five accent hues, the three semantic colors, and the ~20 gradients that define the
// app's primary chrome (buttons, cards, the command bar, the nav sidebar, the tab bar, the
// category-selection glass, success/warning/danger). The many smaller decorative BorderBrush/
// BoxShadow/Effect colors scattered through individual <Style> selectors elsewhere in that file are
// deliberately NOT covered this pass -- BoxShadow's shorthand string syntax cannot bind to
// DynamicResource at all (a hard Avalonia limitation, not an oversight), and the rest are secondary
// hover/glow accents rather than primary surfaces. They stay at their current dark-tuned values in
// every theme/variant -- a real, disclosed limitation, not a silently-cut corner.
public static class ThemeCatalog
{
    public static readonly string[] AccentThemes = ["Default", "Emerald", "Crimson", "Violet"];
    public static readonly string[] Variants = ["Dark", "Light"];

    // Structural surface colors: identical across all four accent themes, differ only Dark vs Light.
    public static readonly Dictionary<string, Dictionary<string, Color>> Structural = new()
    {
        ["Bg0"] = new() { ["Dark"] = Color.Parse("#07111D"), ["Light"] = Color.Parse("#D7E4F4") },
        ["Bg1"] = new() { ["Dark"] = Color.Parse("#0A1624"), ["Light"] = Color.Parse("#DDE8F4") },
        ["Bg2"] = new() { ["Dark"] = Color.Parse("#0E1D2E"), ["Light"] = Color.Parse("#E9F0F7") },
        ["Card"] = new() { ["Dark"] = Color.Parse("#E6112235"), ["Light"] = Color.Parse("#F2F6FA") },
        ["CardStrong"] = new() { ["Dark"] = Color.Parse("#F0122539"), ["Light"] = Color.Parse("#F6F9FC") },
        ["Border"] = new() { ["Dark"] = Color.Parse("#365C78"), ["Light"] = Color.Parse("#A0BCD0") },
        // "Soft" means lower-contrast against the window background in both variants -- darker than
        // Border on a dark background, lighter than Border on a light background.
        ["BorderSoft"] = new() { ["Dark"] = Color.Parse("#223D55"), ["Light"] = Color.Parse("#C3D8E8") },
        ["Muted"] = new() { ["Dark"] = Color.Parse("#91A8BF"), ["Light"] = Color.Parse("#4B6580") },
        ["Text"] = new() { ["Dark"] = Color.Parse("#F4F8FC"), ["Light"] = Color.Parse("#121A22") },
        // Input/list-row surfaces -- TextBox/ComboBox/ScrollBar/ListBoxItem previously hardcoded
        // their own backgrounds outside the Bg0..Card family; these need to swap too, or a light
        // theme would still show black input boxes and list rows.
        ["InputFieldBg"] = new() { ["Dark"] = Color.Parse("#0B1A29"), ["Light"] = Color.Parse("#EEF3F9") },
        ["ScrollTrackBg"] = new() { ["Dark"] = Color.Parse("#081522"), ["Light"] = Color.Parse("#E3EAF2") },
        ["ListRowAltBg"] = new() { ["Dark"] = Color.Parse("#0C1826"), ["Light"] = Color.Parse("#EBF0F7") },
        ["ListRowHoverBg"] = new() { ["Dark"] = Color.Parse("#1B3247"), ["Light"] = Color.Parse("#D7E4F0") },
        ["ListRowSelectedBg"] = new() { ["Dark"] = Color.Parse("#24425C"), ["Light"] = Color.Parse("#C7DAEA") },
        // v0.7.55.0: the semi-transparent scrim behind the Dashboard's full-bleed atmosphere image
        // (dashboard-atmosphere-v3.png) -- dims the art without hiding it. Dark keeps today's exact
        // value; Light uses the same alpha over the Light Bg0 tone instead of the dark one, since a
        // dark scrim over a light background would otherwise read as a stray dark patch.
        ["Bg0Scrim"] = new() { ["Dark"] = Color.Parse("#3A07111D"), ["Light"] = Color.Parse("#3AD7E4F4") },
    };

    // Semantic colors: fixed meaning (success/warning/danger), so identical across all four accent
    // themes -- only the Light variant darkens them enough to stay readable on a light background.
    public static readonly Dictionary<string, Dictionary<string, Color>> Semantic = new()
    {
        ["Green"] = new() { ["Dark"] = Color.Parse("#63DF7B"), ["Light"] = Color.Parse("#1DA037") },
        ["Amber"] = new() { ["Dark"] = Color.Parse("#F4B83F"), ["Light"] = Color.Parse("#BA7E06") },
        ["Red"] = new() { ["Dark"] = Color.Parse("#FF646D"), ["Light"] = Color.Parse("#B9000B") },
    };

    // Accent colors: the five hues that actually change identity per theme. Each theme keeps the
    // same five "slots" (Blue/Cyan/Violet/Magenta/Orange) recognizably in that hue family so a
    // resource named "OrangeBrush" never renders as an unrelated color -- just re-tinted per theme.
    // Keyed [resourceName][theme][variant], matching GradientStops' shape below (so ThemeApplier
    // can iterate both the same way).
    public static readonly Dictionary<string, Dictionary<string, Dictionary<string, Color>>> Accent = new()
    {
        ["Blue"] = Combo(("#2EA8FF", "#2AC9A0", "#FF3D6B", "#6D4DFF"), ("#1478C2", "#16967A", "#C41448", "#4426C2")),
        ["Cyan"] = Combo(("#35D5E8", "#3EE8C0", "#FF6B8F", "#7A8CFF"), ("#1C93A3", "#1FA687", "#C93860", "#4759C4")),
        ["Violet"] = Combo(("#A96DFF", "#6DFFAA", "#FF6DA9", "#A96DFF"), ("#7B3FE0", "#2FCB7A", "#C9317F", "#7B3FE0")),
        ["Magenta"] = Combo(("#E676FF", "#8AFFA0", "#FF4D8A", "#E676FF"), ("#B23FD6", "#33B85A", "#C22160", "#B23FD6")),
        ["Orange"] = Combo(("#FF9558", "#B8E23F", "#FF7A4D", "#E8598A"), ("#D9662A", "#7A9422", "#C24720", "#B22757")),
    };

    // Gradient-stop resources for the ~20 structural/accent/semantic gradients that define the
    // app's primary chrome. Default+Dark is byte-for-byte identical to today's hardcoded values --
    // the baseline look does not change unless the user picks a different theme/variant.
    public static readonly Dictionary<string, Dictionary<string, Dictionary<string, Color>>> GradientStops = new()
    {
        ["ButtonGradientStop0"] = Combo(("#FF27455E", "#FF27455E", "#FF27455E", "#FF27455E"), ("#FF96B5CF", "#FF96B5CF", "#FF96B5CF", "#FF96B5CF")),
        ["ButtonGradientStop1"] = Combo(("#FF183248", "#FF183248", "#FF183248", "#FF183248"), ("#FF80ABCF", "#FF80ABCF", "#FF80ABCF", "#FF80ABCF")),
        ["ButtonGradientStop2"] = Combo(("#FF102537", "#FF102537", "#FF102537", "#FF102537"), ("#FF72A4CF", "#FF72A4CF", "#FF72A4CF", "#FF72A4CF")),
        ["ButtonGradientHoverStop0"] = Combo(("#FF356886", "#FF356886", "#FF356886", "#FF356886"), ("#FFAAC8DA", "#FFAAC8DA", "#FFAAC8DA", "#FFAAC8DA")),
        ["ButtonGradientHoverStop1"] = Combo(("#FF22516E", "#FF22516E", "#FF22516E", "#FF22516E"), ("#FF92BED8", "#FF92BED8", "#FF92BED8", "#FF92BED8")),
        ["ButtonGradientHoverStop2"] = Combo(("#FF173C56", "#FF173C56", "#FF173C56", "#FF173C56"), ("#FF80B3D7", "#FF80B3D7", "#FF80B3D7", "#FF80B3D7")),
        ["ButtonGradientPressedStop0"] = Combo(("#FF173A50", "#FF173A50", "#FF173A50", "#FF173A50"), ("#FF7FB3D4", "#FF7FB3D4", "#FF7FB3D4", "#FF7FB3D4")),
        ["ButtonGradientPressedStop1"] = Combo(("#FF102D42", "#FF102D42", "#FF102D42", "#FF102D42"), ("#FF72ACD6", "#FF72ACD6", "#FF72ACD6", "#FF72ACD6")),
        ["ButtonGradientPressedStop2"] = Combo(("#FF0A2032", "#FF0A2032", "#FF0A2032", "#FF0A2032"), ("#FF63A3D8", "#FF63A3D8", "#FF63A3D8", "#FF63A3D8")),

        ["SuccessGradientStop0"] = Combo(("#FF3FCB8C", "#FF3FCB8C", "#FF3FCB8C", "#FF3FCB8C"), ("#FFC4ECDA", "#FFC4ECDA", "#FFC4ECDA", "#FFC4ECDA")),
        ["SuccessGradientStop1"] = Combo(("#FF23A96F", "#FF23A96F", "#FF23A96F", "#FF23A96F"), ("#FFA5E8CB", "#FFA5E8CB", "#FFA5E8CB", "#FFA5E8CB")),
        ["SuccessGradientStop2"] = Combo(("#FF14724A", "#FF14724A", "#FF14724A", "#FF14724A"), ("#FF82E3BA", "#FF82E3BA", "#FF82E3BA", "#FF82E3BA")),
        ["SuccessGradientHoverStop0"] = Combo(("#FF5EE0AA", "#FF5EE0AA", "#FF5EE0AA", "#FF5EE0AA"), ("#FFD9F6EA", "#FFD9F6EA", "#FFD9F6EA", "#FFD9F6EA")),
        ["SuccessGradientHoverStop1"] = Combo(("#FF34C482", "#FF34C482", "#FF34C482", "#FF34C482"), ("#FFBCEAD5", "#FFBCEAD5", "#FFBCEAD5", "#FFBCEAD5")),
        ["SuccessGradientHoverStop2"] = Combo(("#FF1C8F5C", "#FF1C8F5C", "#FF1C8F5C", "#FF1C8F5C"), ("#FF95E5C2", "#FF95E5C2", "#FF95E5C2", "#FF95E5C2")),
        ["SuccessGradientPressedStop0"] = Combo(("#FF14724A", "#FF14724A", "#FF14724A", "#FF14724A"), ("#FF82E3BA", "#FF82E3BA", "#FF82E3BA", "#FF82E3BA")),
        ["SuccessGradientPressedStop1"] = Combo(("#FF0F5C3C", "#FF0F5C3C", "#FF0F5C3C", "#FF0F5C3C"), ("#FF74E1B4", "#FF74E1B4", "#FF74E1B4", "#FF74E1B4")),
        ["SuccessGradientPressedStop2"] = Combo(("#FF0B4630", "#FF0B4630", "#FF0B4630", "#FF0B4630"), ("#FF67DFB3", "#FF67DFB3", "#FF67DFB3", "#FF67DFB3")),

        ["WarningGradientStop0"] = Combo(("#FFFFCB5C", "#FFFFCB5C", "#FFFFCB5C", "#FFFFCB5C"), ("#FFFDF4E1", "#FFFDF4E1", "#FFFDF4E1", "#FFFDF4E1")),
        ["WarningGradientStop1"] = Combo(("#FFF0B02E", "#FFF0B02E", "#FFF0B02E", "#FFF0B02E"), ("#FFF8E7C4", "#FFF8E7C4", "#FFF8E7C4", "#FFF8E7C4")),
        ["WarningGradientStop2"] = Combo(("#FFB87A10", "#FFB87A10", "#FFB87A10", "#FFB87A10"), ("#FFF1D19A", "#FFF1D19A", "#FFF1D19A", "#FFF1D19A")),
        ["WarningGradientHoverStop0"] = Combo(("#FFFFDE8A", "#FFFFDE8A", "#FFFFDE8A", "#FFFFDE8A"), ("#FFFEF7E7", "#FFFEF7E7", "#FFFEF7E7", "#FFFEF7E7")),
        ["WarningGradientHoverStop1"] = Combo(("#FFFFC454", "#FFFFC454", "#FFFFC454", "#FFFFC454"), ("#FFFDF2DD", "#FFFDF2DD", "#FFFDF2DD", "#FFFDF2DD")),
        ["WarningGradientHoverStop2"] = Combo(("#FFD4941C", "#FFD4941C", "#FFD4941C", "#FFD4941C"), ("#FFF1DBB1", "#FFF1DBB1", "#FFF1DBB1", "#FFF1DBB1")),
        ["WarningGradientPressedStop0"] = Combo(("#FFB87A10", "#FFB87A10", "#FFB87A10", "#FFB87A10"), ("#FFF1D19A", "#FFF1D19A", "#FFF1D19A", "#FFF1D19A")),
        ["WarningGradientPressedStop1"] = Combo(("#FF96630D", "#FF96630D", "#FF96630D", "#FF96630D"), ("#FFEEC887", "#FFEEC887", "#FFEEC887", "#FFEEC887")),
        ["WarningGradientPressedStop2"] = Combo(("#FF744C09", "#FF744C09", "#FF744C09", "#FF744C09"), ("#FFEDBF73", "#FFEDBF73", "#FFEDBF73", "#FFEDBF73")),

        ["DangerGradientStop0"] = Combo(("#FFF06469", "#FFF06469", "#FFF06469", "#FFF06469"), ("#FFFAE0E1", "#FFFAE0E1", "#FFFAE0E1", "#FFFAE0E1")),
        ["DangerGradientStop1"] = Combo(("#FFA92E43", "#FFA92E43", "#FFA92E43", "#FFA92E43"), ("#FFE5AEB8", "#FFE5AEB8", "#FFE5AEB8", "#FFE5AEB8")),

        // v0.7.55.0 bug fix: the v0.7.35.0 button color system (item 32, inspectAction/targetAction)
        // added these two gradients directly as static DesignSystem.axaml resources and never wired
        // them into this catalog -- meaning they were among the ~300 colors that never moved when a
        // theme/variant changed, closing that specific gap. Dark values are byte-identical to what
        // DesignSystem.axaml already hardcoded (so the default look is unchanged); Light values are
        // a straightforward lightening, matching the same pastel-light-variant pattern every other
        // semantic gradient (Success/Warning/Danger) above already uses. Fixed across all four
        // accent themes (not accent-theme-driven), same as Success/Warning/Danger: these represent
        // a fixed action-type meaning (non-mutating "inspect" vs. "act on this" target), not page
        // identity, so they should read the same regardless of which accent theme is selected.
        ["InspectGradientStop0"] = Combo(("#FF4FE0F0", "#FF4FE0F0", "#FF4FE0F0", "#FF4FE0F0"), ("#FFCFF4F8", "#FFCFF4F8", "#FFCFF4F8", "#FFCFF4F8")),
        ["InspectGradientStop1"] = Combo(("#FF2AAECC", "#FF2AAECC", "#FF2AAECC", "#FF2AAECC"), ("#FFB0E4EC", "#FFB0E4EC", "#FFB0E4EC", "#FFB0E4EC")),
        ["InspectGradientStop2"] = Combo(("#FF1A7488", "#FF1A7488", "#FF1A7488", "#FF1A7488"), ("#FF8ED2DE", "#FF8ED2DE", "#FF8ED2DE", "#FF8ED2DE")),
        ["InspectGradientHoverStop0"] = Combo(("#FF8CF3FF", "#FF8CF3FF", "#FF8CF3FF", "#FF8CF3FF"), ("#FFE3FAFC", "#FFE3FAFC", "#FFE3FAFC", "#FFE3FAFC")),
        ["InspectGradientHoverStop1"] = Combo(("#FF4FD0E6", "#FF4FD0E6", "#FF4FD0E6", "#FF4FD0E6"), ("#FFC6EEF4", "#FFC6EEF4", "#FFC6EEF4", "#FFC6EEF4")),
        ["InspectGradientHoverStop2"] = Combo(("#FF2C93A8", "#FF2C93A8", "#FF2C93A8", "#FF2C93A8"), ("#FFA3DAE4", "#FFA3DAE4", "#FFA3DAE4", "#FFA3DAE4")),
        ["InspectGradientPressedStop0"] = Combo(("#FF1A7488", "#FF1A7488", "#FF1A7488", "#FF1A7488"), ("#FF8ED2DE", "#FF8ED2DE", "#FF8ED2DE", "#FF8ED2DE")),
        ["InspectGradientPressedStop1"] = Combo(("#FF155C6C", "#FF155C6C", "#FF155C6C", "#FF155C6C"), ("#FF75C4D2", "#FF75C4D2", "#FF75C4D2", "#FF75C4D2")),
        ["InspectGradientPressedStop2"] = Combo(("#FF0F4650", "#FF0F4650", "#FF0F4650", "#FF0F4650"), ("#FF5FB6C6", "#FF5FB6C6", "#FF5FB6C6", "#FF5FB6C6")),
        ["TargetGradientStop0"] = Combo(("#FFC48CFF", "#FFC48CFF", "#FFC48CFF", "#FFC48CFF"), ("#FFE9D9FF", "#FFE9D9FF", "#FFE9D9FF", "#FFE9D9FF")),
        ["TargetGradientStop1"] = Combo(("#FF9857E0", "#FF9857E0", "#FF9857E0", "#FF9857E0"), ("#FFD6BCF2", "#FFD6BCF2", "#FFD6BCF2", "#FFD6BCF2")),
        ["TargetGradientStop2"] = Combo(("#FF653C9E", "#FF653C9E", "#FF653C9E", "#FF653C9E"), ("#FFBFA1DE", "#FFBFA1DE", "#FFBFA1DE", "#FFBFA1DE")),
        ["TargetGradientHoverStop0"] = Combo(("#FFDAB4FF", "#FFDAB4FF", "#FFDAB4FF", "#FFDAB4FF"), ("#FFF2E7FF", "#FFF2E7FF", "#FFF2E7FF", "#FFF2E7FF")),
        ["TargetGradientHoverStop1"] = Combo(("#FFB87BFF", "#FFB87BFF", "#FFB87BFF", "#FFB87BFF"), ("#FFE4D0FA", "#FFE4D0FA", "#FFE4D0FA", "#FFE4D0FA")),
        ["TargetGradientHoverStop2"] = Combo(("#FF7C4FC0", "#FF7C4FC0", "#FF7C4FC0", "#FF7C4FC0"), ("#FFD1B8E8", "#FFD1B8E8", "#FFD1B8E8", "#FFD1B8E8")),
        ["TargetGradientPressedStop0"] = Combo(("#FF653C9E", "#FF653C9E", "#FF653C9E", "#FF653C9E"), ("#FFBFA1DE", "#FFBFA1DE", "#FFBFA1DE", "#FFBFA1DE")),
        ["TargetGradientPressedStop1"] = Combo(("#FF4E2E7C", "#FF4E2E7C", "#FF4E2E7C", "#FF4E2E7C"), ("#FFAB8DC9", "#FFAB8DC9", "#FFAB8DC9", "#FFAB8DC9")),
        ["TargetGradientPressedStop2"] = Combo(("#FF38225C", "#FF38225C", "#FF38225C", "#FF38225C"), ("#FF9B7DB8", "#FF9B7DB8", "#FF9B7DB8", "#FF9B7DB8")),

        ["PrimaryGradientStop0"] = Combo(("#FF20A9FF", "#FF20FF51", "#FFFF204C", "#FF7620FF"), ("#FFC1E5FC", "#FFC1FCCE", "#FFFCC1CC", "#FFD8C1FC")),
        ["PrimaryGradientStop1"] = Combo(("#FF2584E8", "#FF25E869", "#FFE82533", "#FF8925E8"), ("#FFBED8F5", "#FFBEF5D1", "#FFF5BEC1", "#FFDABEF5")),
        ["PrimaryGradientStop2"] = Combo(("#FF5C4DE4", "#FF4DE4DA", "#FFE49B4D", "#FFE44DD5"), ("#FFD5D1F6", "#FFD1F6F3", "#FFF6E4D1", "#FFF6D1F2")),

        ["CommandSurfaceGradientStop0"] = Combo(("#F3173048", "#F3173048", "#F3173048", "#F3173048"), ("#FF7FA8D0", "#FF7FA8D0", "#FF7FA8D0", "#FF7FA8D0")),
        ["CommandSurfaceGradientStop1"] = Combo(("#F111263B", "#F111263B", "#F111263B", "#F111263B"), ("#FF74A2D0", "#FF74A2D0", "#FF74A2D0", "#FF74A2D0")),
        ["CommandSurfaceGradientStop2"] = Combo(("#F00B1B2D", "#F00B1B2D", "#F00B1B2D", "#F00B1B2D"), ("#FF6699D2", "#FF6699D2", "#FF6699D2", "#FF6699D2")),
        ["CommandSurfaceGradientStop3"] = Combo(("#F006121F", "#F006121F", "#F006121F", "#F006121F"), ("#FF5794D6", "#FF5794D6", "#FF5794D6", "#FF5794D6")),

        ["NavSurfaceGradientStop0"] = Combo(("#F1122536", "#F1122536", "#F1122536", "#F1122536"), ("#FF76A3CB", "#FF76A3CB", "#FF76A3CB", "#FF76A3CB")),
        ["NavSurfaceGradientStop1"] = Combo(("#F00A1826", "#F00A1826", "#F00A1826", "#F00A1826"), ("#FF659ACF", "#FF659ACF", "#FF659ACF", "#FF659ACF")),
        ["NavSurfaceGradientStop2"] = Combo(("#F0071320", "#F0071320", "#F0071320", "#F0071320"), ("#FF5C95D3", "#FF5C95D3", "#FF5C95D3", "#FF5C95D3")),

        ["StatusSurfaceGradientStop0"] = Combo(("#F1112639", "#F1112639", "#F1112639", "#F1112639"), ("#FF74A4CF", "#FF74A4CF", "#FF74A4CF", "#FF74A4CF")),
        ["StatusSurfaceGradientStop1"] = Combo(("#F0091725", "#F0091725", "#F0091725", "#F0091725"), ("#FF6299D1", "#FF6299D1", "#FF6299D1", "#FF6299D1")),
        ["StatusSurfaceGradientStop2"] = Combo(("#F00C1A2A", "#F00C1A2A", "#F00C1A2A", "#F00C1A2A"), ("#FF6A98CD", "#FF6A98CD", "#FF6A98CD", "#FF6A98CD")),

        ["CardGradientStop0"] = Combo(("#F51C3B55", "#F51C3B55", "#F51C3B55", "#F51C3B55"), ("#FF87B0D2", "#FF87B0D2", "#FF87B0D2", "#FF87B0D2")),
        ["CardGradientStop1"] = Combo(("#F2163048", "#F2163048", "#F2163048", "#F2163048"), ("#FF7DA9D1", "#FF7DA9D1", "#FF7DA9D1", "#FF7DA9D1")),
        ["CardGradientStop2"] = Combo(("#F00D2032", "#F00D2032", "#F00D2032", "#F00D2032"), ("#FF6BA0D1", "#FF6BA0D1", "#FF6BA0D1", "#FF6BA0D1")),
        ["CardGradientStop3"] = Combo(("#F0081422", "#F0081422", "#F0081422", "#F0081422"), ("#FF5F94D1", "#FF5F94D1", "#FF5F94D1", "#FF5F94D1")),

        ["ServerTabGradientStop0"] = Combo(("#F0071523", "#F0071523", "#F0071523", "#F0071523"), ("#FF5B98D6", "#FF5B98D6", "#FF5B98D6", "#FF5B98D6")),
        ["ServerTabGradientStop1"] = Combo(("#E90D2236", "#E90D2236", "#E90D2236", "#E90D2236"), ("#FF6BA1D4", "#FF6BA1D4", "#FF6BA1D4", "#FF6BA1D4")),
        ["ServerTabGradientStop2"] = Combo(("#E712304A", "#E712304A", "#E712304A", "#E712304A"), ("#FF76AAD7", "#FF76AAD7", "#FF76AAD7", "#FF76AAD7")),

        ["CategoryActiveGradientStop0"] = Combo(("#CCA8C0E8", "#CCA8E8C5", "#CCE8ABA8", "#CCD0A8E8"), ("#FFECF1F9", "#FFECF9F2", "#FFF9ECEC", "#FFF4ECF9")),
        ["CategoryActiveGradientStop1"] = Combo(("#C088D0FC", "#C088FCA1", "#C0FC88A0", "#C0B488FC"), ("#FFE7F5FD", "#FFE7FDEC", "#FFFDE7EC", "#FFF0E7FD")),
        ["CategoryActiveGradientStop2"] = Combo(("#8A3F94E0", "#8A3FE070", "#8AE03F51", "#8A8B3FE0"), ("#FFC9DFF4", "#FFC9F4D6", "#FFF4C9CE", "#FFDDC9F4")),
        ["CategoryActiveGradientStop3"] = Combo(("#521E5A9E", "#521E9E4D", "#529E1E25", "#52621E9E"), ("#FF9DC0E7", "#FF9DE7B8", "#FFE79DA0", "#FFC49DE7")),
        ["CategoryActiveGradientStop4"] = Combo(("#260E2648", "#260E4826", "#26480E0E", "#26300E48"), ("#FF6E9BDC", "#FF6EDC9C", "#FFDC6E6E", "#FFAE6EDC")),

        ["ServerTabActiveGradientStop0"] = Combo(("#F0061321", "#F0061321", "#F0061321", "#F0061321"), ("#FF5795D8", "#FF5795D8", "#FF5795D8", "#FF5795D8")),
        ["ServerTabActiveGradientStop1"] = Combo(("#F0092035", "#F0092035", "#F0092035", "#F0092035"), ("#FF60A1DC", "#FF60A1DC", "#FF60A1DC", "#FF60A1DC")),
        ["ServerTabActiveGradientStop2"] = Combo(("#F00D3655", "#F00D5520", "#F0550D18", "#F02C0D55"), ("#FF6FB0E2", "#FF6FE28D", "#FFE26F80", "#FFA06FE2")),
        ["ServerTabActiveGradientStop3"] = Combo(("#F0105D86", "#F0108625", "#F086102C", "#F0391086"), ("#FF84C7EA", "#FF84EA97", "#FFEA849C", "#FFA884EA")),
    };

    private static Dictionary<string, Dictionary<string, Color>> Combo(
        (string Default, string Emerald, string Crimson, string Violet) dark,
        (string Default, string Emerald, string Crimson, string Violet) light) => new()
    {
        ["Default"] = new() { ["Dark"] = Color.Parse(dark.Default), ["Light"] = Color.Parse(light.Default) },
        ["Emerald"] = new() { ["Dark"] = Color.Parse(dark.Emerald), ["Light"] = Color.Parse(light.Emerald) },
        ["Crimson"] = new() { ["Dark"] = Color.Parse(dark.Crimson), ["Light"] = Color.Parse(light.Crimson) },
        ["Violet"] = new() { ["Dark"] = Color.Parse(dark.Violet), ["Light"] = Color.Parse(light.Violet) },
    };

    // v0.7.55.0: Central Theme System Completion. Grounded against a full read of DesignSystem.axaml
    // before writing this -- most of its ~300 individually-hardcoded colors turned out not to be
    // arbitrary one-offs: they're page-accent tints (the "card flare" accentHome/Server/World/
    // Backups/Mods/Tools/System system, v0.7.4.0) and status glows (glowGreen/Red/Amber/Purple/
    // DarkGreen/Cyan/Violet) that already, by construction, reuse this catalog's own Accent/
    // Semantic hex values as their glow color -- just hardcoded per-instance instead of referenced,
    // so they never moved when a theme/variant changed. DerivedColorNames lists every base color
    // (existing Accent/Semantic slots, plus two with no natural existing match) that needs a full
    // family of derived resources -- border brush, two glow-shadow strengths, and a tinted card
    // gradient -- computed by ThemeApplier from a formula in every theme and variant, not
    // hand-authored per combination (confirmed there is no way to manually tune ~300 values x 4
    // themes x 2 variants with any confidence without being able to see the rendered result).
    // "Purple" is a deliberate alias of Violet (confirmed identical hex, #A96DFF, in every place
    // DesignSystem.axaml uses either name) -- no separate computation, both read the same source.
    // "DarkGreen" (a deliberately darker/muted green, distinct from Semantic Green, used by a
    // couple of "protected/settled" status cards) and "Neutral" (a desaturated blue-gray "no strong
    // opinion" tint) have no existing Accent/Semantic slot, so they're derived from other Structural/
    // Semantic values instead of an accent-theme-driven one.
    public static readonly string[] DerivedColorNames =
        ["Blue", "Cyan", "Violet", "Magenta", "Orange", "Green", "Amber", "Red", "Purple", "DarkGreen", "Neutral"];

    // v0.7.52.0: Per-Tab Color Coding (item 2) -- the assignable pool for ConnectionProfile's own
    // AccentColorKey. Reuses the same derived names/resources DerivedColorNames already produces
    // (e.g. "BlueAccentBorderBrush") rather than inventing a second color system, but excludes
    // "Neutral" since that's a deliberately muted grey tone meant for "no strong opinion" status
    // decoration, not a distinct-enough identity marker for telling servers apart at a glance.
    public static readonly string[] TabIdentityColorNames =
        ["Blue", "Cyan", "Violet", "Magenta", "Orange", "Green", "Amber", "Red", "Purple", "DarkGreen"];

    public static Color ResolveDerivedBaseColor(string name, string accentTheme, string variant) => name switch
    {
        "Blue" or "Cyan" or "Violet" or "Magenta" or "Orange" => Accent[name][accentTheme][variant],
        "Green" or "Amber" or "Red" => Semantic[name][variant],
        "Purple" => Accent["Violet"][accentTheme][variant],
        "DarkGreen" => ThemeColorMath.Darken(Semantic["Green"][variant], 0.32),
        "Neutral" => ThemeColorMath.Blend(Structural["Border"][variant], Structural["Muted"][variant], 0.5),
        _ => Accent["Blue"][accentTheme][variant],
    };
}
