using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MystTiq.Desktop.Converters;

// v0.7.63.0 Central Theme System bugfix: several status-dot/health-label colors were computed as
// hardcoded hex literals directly in ViewModels/Models (TabSession.StatusDotColor,
// MainWindowViewModel.HealthStateColor, ServerProfileSummaryDto.StatusDotColor) and bound via a
// plain {Binding}. A plain {Binding} never re-evaluates on its own when ThemeApplier rewrites
// Application.Current.Resources -- only {DynamicResource} does that, and Foreground/Fill can't
// take a {DynamicResource} whose target is chosen by C# logic. So every one of those dots/labels
// stayed locked to their original Dark/Default hex value through every accent-theme and Light/Dark
// switch, the same bug class the user reported live ("colour system doesn't control every
// resource"). Fix: those properties now return a semantic KEY ("Green"/"Amber"/"Red"/"Muted"/...)
// instead of a hex string, and this converter resolves that key to the live "{key}Brush" resource
// on every binding re-evaluation -- looked up fresh each time, never cached, so it always reflects
// whatever ThemeApplier last wrote. Mirrors the TryGetResource idiom TabSession.AccentBrush already
// established for the same reason (v0.7.52.0).
public sealed class SemanticStatusColorConverter : IValueConverter
{
    public static readonly SemanticStatusColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string;
        if (string.IsNullOrWhiteSpace(key)) key = "Muted";

        if (Application.Current?.TryGetResource($"{key}Brush", null, out var resource) == true && resource is IBrush brush)
            return brush;

        // Fallback only reached if the key names a resource that doesn't exist (a typo, not a
        // legitimate "no theme applied yet" state -- ThemeApplier always seeds these at startup).
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
