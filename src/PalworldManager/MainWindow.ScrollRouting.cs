using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PalworldManager;

/// <summary>
/// Routes mouse-wheel input through nested scrolling controls. WPF controls such as
/// DataGrid, ListBox, and multiline TextBox own an internal ScrollViewer and otherwise
/// consume the wheel even when that viewer is already at its upper or lower boundary.
/// Shift + wheel is routed horizontally when the hovered control supports it.
/// </summary>
public partial class MainWindow
{
    private const double DefaultMouseWheelScrollStep = 48d;
    private const double ScrollBoundaryTolerance = 0.5d;

    private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || e.Delta == 0 || e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        var horizontal = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        foreach (var viewer in EnumerateAncestorScrollViewers(source))
        {
            if (horizontal)
            {
                if (!CanScrollHorizontally(viewer, e.Delta))
                    continue;

                var targetOffset = viewer.HorizontalOffset - GetWheelDistance(e.Delta);
                viewer.ScrollToHorizontalOffset(Math.Clamp(targetOffset, 0d, viewer.ScrollableWidth));
                e.Handled = true;
                return;
            }

            if (!CanScrollVertically(viewer, e.Delta))
                continue;

            var verticalTarget = viewer.VerticalOffset - GetWheelDistance(e.Delta);
            viewer.ScrollToVerticalOffset(Math.Clamp(verticalTarget, 0d, viewer.ScrollableHeight));
            e.Handled = true;
            return;
        }
    }

    private static double GetWheelDistance(int wheelDelta)
    {
        var lines = SystemParameters.WheelScrollLines;
        if (lines <= 0 || lines == -1)
            return wheelDelta / 120d * DefaultMouseWheelScrollStep;

        // Preserve smooth touchpad deltas while respecting the Windows wheel-lines setting.
        return wheelDelta / 120d * Math.Max(16d, lines * 16d);
    }

    private static IEnumerable<ScrollViewer> EnumerateAncestorScrollViewers(DependencyObject source)
    {
        var current = source;
        while (current is not null)
        {
            if (current is ScrollViewer viewer)
                yield return viewer;

            current = GetParent(current);
        }
    }

    private static DependencyObject? GetParent(DependencyObject current)
    {
        if (current is FrameworkContentElement contentElement)
            return contentElement.Parent;

        return VisualTreeHelper.GetParent(current);
    }

    private static bool CanScrollVertically(ScrollViewer viewer, int wheelDelta)
    {
        if (!viewer.IsVisible || viewer.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled ||
            viewer.ScrollableHeight <= ScrollBoundaryTolerance)
        {
            return false;
        }

        return wheelDelta > 0
            ? viewer.VerticalOffset > ScrollBoundaryTolerance
            : viewer.VerticalOffset < viewer.ScrollableHeight - ScrollBoundaryTolerance;
    }

    private static bool CanScrollHorizontally(ScrollViewer viewer, int wheelDelta)
    {
        if (!viewer.IsVisible || viewer.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled ||
            viewer.ScrollableWidth <= ScrollBoundaryTolerance)
        {
            return false;
        }

        return wheelDelta > 0
            ? viewer.HorizontalOffset > ScrollBoundaryTolerance
            : viewer.HorizontalOffset < viewer.ScrollableWidth - ScrollBoundaryTolerance;
    }
}
