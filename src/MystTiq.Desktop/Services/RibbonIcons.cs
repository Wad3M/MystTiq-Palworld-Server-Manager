using Avalonia.Media;

namespace MystTiq.Desktop.Services;

// v0.8.1.0: the user's Palworld-inspired Ribbon icons (MystTiq_Palworld_Ribbon_Icons.zip, kept under
// docs/ribbon-icons/ with its SVGs, icons.json, preview and README). Ten original single-colour vectors on a 24x24
// grid, drawn as stroked paths (1.8 stroke, round caps and joins, no fill) in the button's existing theme colour.
// Path data is exactly the package's icons.json / PalRibbonIcons.axaml. Parsed once and cached.
public static class RibbonIcons
{
    private static readonly Dictionary<string, string> PathData = new(StringComparer.Ordinal)
    {
        ["refresh"] = "M20 8 A9 9 0 1 0 20 17 M20 3 V8 H15 M17 12 A5 5 0 1 1 7 12 A5 5 0 1 1 17 12 M7 12 H10 M14 12 H17 M14 12 A2 2 0 1 1 10 12 A2 2 0 1 1 14 12",
        ["start"] = "M21 12 A9 9 0 1 1 3 12 A9 9 0 1 1 21 12 M3 12 H6 M18 12 H21 M10 8 L16 12 L10 16 Z",
        ["restart"] = "M4 8 A9 9 0 0 1 20 7 M20 3 V7 H16 M20 16 A9 9 0 0 1 4 17 M4 21 V17 H8 M17 12 A5 5 0 1 1 7 12 A5 5 0 1 1 17 12 M7 12 H10 M14 12 H17 M14 12 A2 2 0 1 1 10 12 A2 2 0 1 1 14 12",
        ["stop"] = "M21 12 A9 9 0 1 1 3 12 A9 9 0 1 1 21 12 M3 12 H6 M18 12 H21 M9 9 H15 V15 H9 Z",
        ["backup"] = "M3 11 H21 V21 H3 Z M3 16 H21 M7 11 V21 M17 11 V21 M12 2 V8 M9 5 L12 8 L15 5",
        ["console"] = "M7 3 L5 6 M17 3 L19 6 M3 6 H21 V19 H3 Z M9 22 H15 M12 19 V22 M7 10 L10 12.5 L7 15 M13 15 H17",
        ["doctor"] = "M4 10 L3 3 L9 6 Q12 5 15 6 L21 3 L20 10 Q22 19 12 21 Q2 19 4 10 M12 10 V16 M9 13 H15",
        ["verify-files"] = "M12 2 L21 6 V12 Q20 19 12 22 Q4 19 3 12 V6 Z M7 12 L10.5 15.5 L17 9",
        ["install-missing"] = "M3 14 H21 V18 H3 Z M5 18 V22 M19 18 V22 M8 3 H16 V8 H8 Z M12 8 V12 M9 10 L12 13 L15 10",
        ["force-stop"] = "M8 2 H16 L22 8 V16 L16 22 H8 L2 16 V8 Z M8 8 L16 16 M16 8 L8 16",
    };

    // The ten actions the icons were drawn for, by their exact Ribbon label.
    private static readonly Dictionary<string, string> ByLabel = new(StringComparer.Ordinal)
    {
        ["Refresh"] = "refresh", ["Start"] = "start", ["Restart"] = "restart", ["Stop"] = "stop",
        ["Backup"] = "backup", ["Console"] = "console", ["Doctor"] = "doctor", ["Verify Files"] = "verify-files",
        ["Install Missing"] = "install-missing", ["Force Stop"] = "force-stop",
    };

    // Buttons with other labels that already share one of those actions' glyphs mean the same thing (every
    // "Refresh ..." uses the ↻ glyph, "Restart Server" uses ⟳), so they take the same icon and the Ribbon stays
    // consistent. Everything else keeps its existing text glyph.
    private static readonly Dictionary<string, string> ByGlyph = new(StringComparer.Ordinal)
    {
        ["↻"] = "refresh", ["⟳"] = "restart",
    };

    private static readonly Dictionary<string, Geometry> Cache = new(StringComparer.Ordinal);

    public static IReadOnlyCollection<string> Keys => PathData.Keys;

    public static string? KeyFor(string label, string glyph) =>
        ByLabel.TryGetValue(label ?? string.Empty, out var key) ? key :
        ByGlyph.TryGetValue(glyph ?? string.Empty, out key) ? key : null;

    public static string? PathFor(string key) => PathData.TryGetValue(key, out var data) ? data : null;

    public static Geometry? Geometry(string? key)
    {
        if (key is null || !PathData.TryGetValue(key, out var data)) return null;
        lock (Cache)
        {
            if (!Cache.TryGetValue(key, out var geometry))
                Cache[key] = geometry = StreamGeometry.Parse(data);
            return geometry;
        }
    }

    // v0.8.8.0: the user's image icon batches (palworld_ribbon_icons_batch_1/2/3.zip): thirty full-colour illustrated
    // tiles, each with its own dark framed background, under Assets/RibbonIcons/. Matched by the exact English Ribbon
    // label like the vectors above; a button with one shows it instead of a vector or its glyph.
    // v0.8.10.0: the files are now the user's full framed originals (…_originals.zip, 1254 px), resized to 512 px.
    // Quick Actions' Backup runs the same command as Backups' Create, and Doctor opens the page Run Doctor belongs to,
    // so those two reuse that image; every other image belongs to one label.
    private static readonly Dictionary<string, string> ImageByLabel = new(StringComparer.Ordinal)
    {
        // Batch 1.
        ["Clear"] = "clear", ["Create"] = "create_backup", ["Export"] = "export", ["Import"] = "import", ["Open Root"] = "open_root",
        ["Pause"] = "pause", ["Reset"] = "reset", ["Save"] = "save", ["Verify All"] = "verify_all", ["Verify & Scan"] = "verify_scan",
        // Batch 2.
        ["Confirm Install"] = "confirm_install", ["Disable All"] = "disable_all", ["Enable All"] = "enable_all", ["Export Report"] = "export_report",
        ["Kill Processes"] = "kill_processes", ["Preview Install"] = "preview_install", ["Repair"] = "repair", ["Rollback"] = "rollback",
        ["Run Doctor"] = "run_doctor", ["Safe-Start"] = "safe_start",
        // Batch 3.
        ["Detect Instances"] = "detect_instances", ["Discover Saves"] = "discover_saves", ["Export CSV"] = "export_csv",
        ["Mark All Read"] = "mark_all_read", ["Refresh Bases"] = "refresh_bases", ["Repair Firewall"] = "repair_firewall",
        ["Run Diagnostics"] = "run_diagnostics", ["Self-Test"] = "self_test", ["Self-Tests"] = "self_tests", ["Validate"] = "validate",
        // v0.8.10.0: shared images (see above).
        ["Backup"] = "create_backup", ["Doctor"] = "run_doctor",
        // v0.8.13.0: the user's last ten (palworld_missing_10_icons.zip), so every Ribbon button has an image. Every
        // "Refresh …" button uses the refresh image, except Refresh Bases, which has its own; Restart Server shares Restart's.
        ["Refresh"] = "refresh", ["Refresh MODs"] = "refresh", ["Refresh Runtime"] = "refresh", ["Refresh World"] = "refresh",
        ["Refresh Ops"] = "refresh", ["Refresh Evidence"] = "refresh", ["Refresh Map"] = "refresh", ["Refresh History"] = "refresh",
        ["Start"] = "start", ["Restart"] = "restart", ["Restart Server"] = "restart", ["Stop"] = "stop", ["Console"] = "console",
        ["Force Stop"] = "force_stop", ["Verify Files"] = "verify_files", ["Install Missing"] = "install_missing",
        ["Run Analysis"] = "run_analysis", ["Preview Plan"] = "preview_plan",
    };

    // Shown in the 34x34 icon tile; decoded at 96 px so it stays sharp on high-DPI screens without holding 512 px bitmaps.
    public const int ImageDecodeWidth = 96;
    private static readonly Dictionary<string, Avalonia.Media.Imaging.Bitmap> ImageCache = new(StringComparer.Ordinal);

    // One entry per image file (two labels share one).
    public static IReadOnlyCollection<string> ImageKeys => ImageByLabel.Values.Distinct(StringComparer.Ordinal).ToArray();
    public static IReadOnlyCollection<string> ImageLabels => ImageByLabel.Keys;

    public static string? ImageKeyFor(string? label) => ImageByLabel.TryGetValue(label ?? string.Empty, out var key) ? key : null;

    public static Avalonia.Media.Imaging.Bitmap? Image(string? key)
    {
        if (key is null || !ImageByLabel.ContainsValue(key)) return null;
        lock (ImageCache)
        {
            if (!ImageCache.TryGetValue(key, out var bitmap))
            {
                using var stream = Avalonia.Platform.AssetLoader.Open(new Uri($"avares://MystTiq.Desktop/Assets/RibbonIcons/{key}.png"));
                ImageCache[key] = bitmap = Avalonia.Media.Imaging.Bitmap.DecodeToWidth(stream, ImageDecodeWidth, Avalonia.Media.Imaging.BitmapInterpolationMode.HighQuality);
            }
            return bitmap;
        }
    }
}
