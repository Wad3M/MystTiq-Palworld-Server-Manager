using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;

namespace MystTiq.Desktop.Views;

// v0.7.11.0: Avalonia's TrayIcon has no built-in balloon/notification API (confirmed against the
// current Avalonia docs and source before building this) -- this is a small, self-positioned,
// auto-dismissing window standing in for one, shown whenever the main window is hidden to the
// tray while at least one server is still running.
public sealed partial class TrayReminderToast : Window
{
    private static readonly TimeSpan DismissAfter = TimeSpan.FromSeconds(5);

    // v0.7.66.0 bugfix: this toast previously resolved its own target screen via
    // Screens.ScreenFromWindow(this) -- but at the moment Opened fires, the toast's own Position is
    // whatever the OS/window manager assigned it as a brand-new WindowStartupLocation="Manual"
    // window with no Position ever explicitly set, not yet the corner this method is about to move
    // it to. On a multi-monitor layout that default can land on the wrong monitor entirely (reported
    // live: two monitors, secondary stacked above primary at a negative Y origin), so the "which
    // screen" resolution was itself unreliable, independent of the math that follows it. The owning
    // MainWindow, by contrast, has a real, user-placed position at the moment this toast is shown
    // (it only ever appears right after MainWindow.Hide() -- see App.ShowTrayStillRunningReminder)
    // -- resolving the screen from the owner instead of the not-yet-settled toast is reliable.
    private readonly Window? owner;

    public TrayReminderToast()
    {
        InitializeComponent();
        Opened += (_, _) => PositionBottomRight();
        // v0.7.67.0 bugfix: found live-testing the v0.7.66.0 fix above on the actual reported
        // hardware -- Bounds at the moment Opened fires is NOT yet the final SizeToContent-driven
        // content size; it was observed reporting a large placeholder size (~1521x770, roughly
        // two-thirds of this machine's combined virtual desktop width) that is not caught by the
        // existing "Bounds.Width > 0 ? Bounds.Width : 320" fallback, since it IS greater than zero,
        // just wrong. That placeholder size, fed into the bottom-right math, produced a position far
        // from any actual corner -- independent of, and in addition to, the wrong-screen bug that
        // prompted v0.7.66.0. LayoutUpdated fires after each real layout pass, once the window has
        // actually measured/arranged its content to the final small size SizeToContent settles on;
        // repositioning there (every time it fires, not just once, since it's cheap and idempotent
        // once the size stabilizes) recovers from whatever transient size Opened observed.
        LayoutUpdated += (_, _) => PositionBottomRight();
    }

    public TrayReminderToast(string message, Window? owner = null) : this()
    {
        this.owner = owner;
        MessageText.Text = message;
        var timer = new DispatcherTimer { Interval = DismissAfter };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Close();
        };
        timer.Start();
    }

    // v0.7.75.0 bugfix: reported live -- with v0.7.66.0/v0.7.67.0's wrong-screen and
    // transient-size fixes both in place, the toast still landed close to the corner but with its
    // right edge slightly cut off. Screen.WorkingArea is in physical pixels, but Bounds.Width/Height
    // (this Window's own size) is in DIPs (logical pixels) -- on any display scaled above 100%,
    // subtracting a DIP measurement from a physical-pixel edge undershoots by the scale factor,
    // landing the window's true physical right/bottom edge past the working area. RenderScaling
    // converts the DIP size to physical pixels before the subtraction; at exactly 100% scaling this
    // is a no-op (scale 1.0), so it doesn't regress the case that already worked.
    private void PositionBottomRight()
    {
        var screen = (owner is not null ? Screens.ScreenFromWindow(owner) : null)
            ?? Screens.ScreenFromWindow(this) ?? Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null) return;
        var area = screen.WorkingArea;
        var scaling = RenderScaling > 0 ? RenderScaling : 1.0;
        var widthPx = (Bounds.Width > 0 ? Bounds.Width : 320) * scaling;
        var heightPx = (Bounds.Height > 0 ? Bounds.Height : 80) * scaling;
        var marginPx = 16 * scaling;
        Position = new Avalonia.PixelPoint(
            area.X + area.Width - (int)widthPx - (int)marginPx,
            area.Y + area.Height - (int)heightPx - (int)marginPx);
    }
}
