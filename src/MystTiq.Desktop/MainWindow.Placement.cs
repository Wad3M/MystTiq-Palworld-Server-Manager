// MystTiq v1.1.5.0: file reviewed for this release (2026-10-22).
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop;

// v1.1.5.0 (roadmap V-1): the window draws its own title bar (WindowDecorations="None"), which also took away the system's
// resize borders, so it could be maximized but never sized. Eight invisible grips along the edges and corners start the
// system's own resize (BeginResizeDrag: Windows and X11), hidden while maximized. The last normal size and position and the
// maximized state are remembered and put back on start, fitted onto the screens there are now (WindowPlacement.Fit).
public sealed partial class MainWindow
{
    private LocalWindowPlacementStore? placementStore;
    private DispatcherTimer? placementSaveTimer;
    private PixelPoint lastNormalPosition;
    private Size lastNormalSize;

    /// <summary>Puts back and from then on remembers the window's placement. The app calls it; the harnesses never do, so a
    /// test never moves or overwrites the owner's saved placement.</summary>
    public void UsePlacementStore(LocalWindowPlacementStore store)
    {
        placementStore = store;
        RestorePlacement(store.Load());
        PositionChanged += (_, _) => NotePlacement();
        Resized += (_, _) => NotePlacement();
        PropertyChanged += (_, args) => { if (args.Property == WindowStateProperty) NotePlacement(); };
    }

    private void RestorePlacement(WindowPlacementState? saved)
    {
        var screens = Screens?.All.Select(s => new ScreenArea(s.WorkingArea.X / s.Scaling, s.WorkingArea.Y / s.Scaling, s.WorkingArea.Width / s.Scaling, s.WorkingArea.Height / s.Scaling)).ToList() ?? [];
        var fitted = WindowPlacement.Fit(saved, screens, MinWidth, MinHeight);
        if (fitted is null) return;
        var scaling = Screens?.ScreenFromPoint(new PixelPoint((int)fitted.X, (int)fitted.Y))?.Scaling ?? 1;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = fitted.Width;
        Height = fitted.Height;
        Position = new PixelPoint((int)Math.Round(fitted.X * scaling), (int)Math.Round(fitted.Y * scaling));
        lastNormalPosition = Position;
        lastNormalSize = new Size(fitted.Width, fitted.Height);
        if (fitted.Maximized) WindowState = WindowState.Maximized;
        UpdateResizeGrips();
    }

    private void NotePlacement()
    {
        UpdateResizeGrips();
        if (placementStore is null || WindowState == WindowState.Minimized) return;
        if (WindowState == WindowState.Normal)
        {
            lastNormalPosition = Position;
            lastNormalSize = new Size(Width, Height);
        }
        placementSaveTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => SavePlacementNow());
        placementSaveTimer.Stop();
        placementSaveTimer.Start();
    }

    /// <summary>Saves at once (the timer after a change, and the app before it exits).</summary>
    public void SavePlacementNow()
    {
        placementSaveTimer?.Stop();
        if (placementStore is null || lastNormalSize.Width <= 0) return;
        var scaling = Screens?.ScreenFromPoint(lastNormalPosition)?.Scaling ?? 1;
        placementStore.Save(new WindowPlacementState(lastNormalPosition.X / scaling, lastNormalPosition.Y / scaling,
            lastNormalSize.Width, lastNormalSize.Height, WindowState == WindowState.Maximized));
    }

    private void UpdateResizeGrips()
    {
        if (ResizeGrips is not null) ResizeGrips.IsVisible = WindowState == WindowState.Normal;
    }

    private void ResizeGrip_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (WindowState != WindowState.Normal || sender is not Control { Tag: string edge } || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (!Enum.TryParse<WindowEdge>(edge, out var windowEdge)) return;
        BeginResizeDrag(windowEdge, e);
        e.Handled = true;
    }
}
