using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MystTiq.Desktop.Converters;

// v0.7.64.0: reveal/hide toggle for the Bearer token field(s) -- see ShowBearerToken on
// MainWindowViewModel. Plain text glyphs rather than an icon asset, matching how the ribbon's own
// flatIcon glyphs are just Unicode characters, not images.
public sealed class BoolToEyeGlyphConverter : IValueConverter
{
    public static readonly BoolToEyeGlyphConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "🙈" : "👁";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
