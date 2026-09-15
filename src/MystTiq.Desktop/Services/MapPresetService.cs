using Avalonia.Platform;

namespace MystTiq.Desktop.Services;

public sealed record MapPreset(string Key, string DisplayName, string AssetUri);

// v0.7.20.0: bundled map background presets (Palpagos -- the base game map -- and World Tree),
// so a user doesn't have to find/crop their own Palworld map screenshot before the World Map card
// shows anything geography-shaped. Deliberately reuses SetMapBackgroundImagePath's existing
// load/persist/fallback logic unchanged rather than adding a parallel avares://-loading path: a
// preset is extracted from the embedded asset to a real file (once, cached) under the same local
// config root LocalMapPreferencesStore already uses, and from that point on it IS just a browsed
// file as far as the rest of the app is concerned.
public sealed class MapPresetService
{
    public static IReadOnlyList<MapPreset> Presets { get; } =
    [
        new("palpagos", "Palpagos", "avares://MystTiq.Desktop/Assets/Maps/palpagos.png"),
        new("worldtree", "World Tree", "avares://MystTiq.Desktop/Assets/Maps/worldtree.png")
    ];

    private readonly string extractedRoot;

    public MapPresetService()
    {
        extractedRoot = Path.Combine(LocalMapPreferencesStore.GetLocalConfigRoot(), "MystTiq", "MapPresets");
    }

    // Returns a real filesystem path suitable for SetMapBackgroundImagePath -- extracting the
    // bundled asset on first use only; subsequent calls reuse the already-extracted file.
    public string GetOrExtractPresetPath(MapPreset preset)
    {
        Directory.CreateDirectory(extractedRoot);
        var targetPath = Path.Combine(extractedRoot, $"{preset.Key}.png");
        if (File.Exists(targetPath)) return targetPath;

        using var assetStream = AssetLoader.Open(new Uri(preset.AssetUri));
        using var fileStream = File.Create(targetPath);
        assetStream.CopyTo(fileStream);
        return targetPath;
    }

    // v0.7.21.0: lets the ViewModel tell whether the currently-active background is a known
    // preset (and which one) purely from the file path already stored for it -- no separate
    // "which preset is active" field to keep in sync, since the extracted path IS the source of
    // truth for what's currently loaded.
    public MapPreset? TryGetPresetForPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return Presets.FirstOrDefault(p =>
            string.Equals(GetOrExtractPresetPath(p), path, StringComparison.OrdinalIgnoreCase));
    }
}
