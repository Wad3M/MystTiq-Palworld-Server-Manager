// MystTiq v1.1.5.1: file reviewed for this release (2026-10-26).
using System.Text.Json;

namespace MystTiq.Desktop.Services;

/// <summary>v1.1.5.0 (roadmap V-1): the main window's last normal size and position, and whether it was maximized.</summary>
public sealed record WindowPlacementState(double X, double Y, double Width, double Height, bool Maximized);

/// <summary>A screen's working area (the part not covered by the taskbar), in the same units as the placement.</summary>
public readonly record struct ScreenArea(double X, double Y, double Width, double Height);

/// <summary>
/// v1.1.5.0 (roadmap V-1): fits a remembered placement onto the screens there are now. A size below the minimum is raised to
/// it, a size larger than its screen is cut to that screen, and a window whose title bar would not be on any screen (its
/// monitor unplugged, the resolution lowered) is moved onto the screen it overlaps most, or onto the first screen.
/// </summary>
public static class WindowPlacement
{
    // At least this much of the title bar must be on a screen to count as reachable.
    public const double MinimumVisibleWidth = 160;
    public const double TitleBarHeight = 66;

    public static WindowPlacementState? Fit(WindowPlacementState? saved, IReadOnlyList<ScreenArea> screens, double minWidth, double minHeight)
    {
        if (saved is null || screens.Count == 0) return null;
        if (!double.IsFinite(saved.X) || !double.IsFinite(saved.Y) || !double.IsFinite(saved.Width) || !double.IsFinite(saved.Height)) return null;

        var width = Math.Max(saved.Width, minWidth);
        var height = Math.Max(saved.Height, minHeight);
        var reachable = screens.Any(s => TitleBarVisible(saved.X, saved.Y, width, s));
        var screen = reachable
            ? screens.First(s => TitleBarVisible(saved.X, saved.Y, width, s))
            : screens.OrderByDescending(s => Overlap(saved.X, saved.Y, width, height, s)).First();

        width = Math.Min(width, Math.Max(minWidth, screen.Width));
        height = Math.Min(height, Math.Max(minHeight, screen.Height));
        var x = saved.X;
        var y = saved.Y;
        if (!reachable)
        {
            x = screen.X + Math.Max(0, (screen.Width - width) / 2);
            y = screen.Y + Math.Max(0, (screen.Height - height) / 2);
        }
        // The title bar never above the top of the screen (it could not be dragged back).
        y = Math.Max(y, screen.Y);
        return new WindowPlacementState(x, y, width, height, saved.Maximized);
    }

    private static bool TitleBarVisible(double x, double y, double width, ScreenArea s)
    {
        var visibleWidth = Math.Min(x + width, s.X + s.Width) - Math.Max(x, s.X);
        var visibleTitle = Math.Min(y + TitleBarHeight, s.Y + s.Height) - Math.Max(y, s.Y);
        return visibleWidth >= MinimumVisibleWidth && visibleTitle >= TitleBarHeight / 2;
    }

    private static double Overlap(double x, double y, double width, double height, ScreenArea s) =>
        Math.Max(0, Math.Min(x + width, s.X + s.Width) - Math.Max(x, s.X)) * Math.Max(0, Math.Min(y + height, s.Y + s.Height) - Math.Max(y, s.Y));
}

/// <summary>v1.1.5.0 (roadmap V-1): window-placement.json beside the other desktop preferences.</summary>
public sealed class LocalWindowPlacementStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public LocalWindowPlacementStore()
    {
        StoragePath = Path.Combine(LocalMapPreferencesStore.GetLocalConfigRoot(), "MystTiq", "window-placement.json");
    }

    public string StoragePath { get; }

    public WindowPlacementState? Load()
    {
        try
        {
            return File.Exists(StoragePath) ? JsonSerializer.Deserialize<WindowPlacementState>(File.ReadAllText(StoragePath), JsonOptions) : null;
        }
        catch { return null; } // an unreadable file never stops the window opening
    }

    public void Save(WindowPlacementState placement)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
            var temp = StoragePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(placement, JsonOptions));
            File.Move(temp, StoragePath, overwrite: true);
        }
        catch { /* best effort: the next change saves again */ }
    }
}
