using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace MystTiq.Desktop.Converters;

// v0.7.45.0: Update Center's component version table -- colors each row's status text by
// ComponentVersionDto.Status (the raw wire value, not the display-friendly StatusText).
// v0.7.63.0 theme-system bugfix: previously cached static SolidColorBrush instances built once
// from hardcoded Dark-theme hex literals -- never re-themed on an accent/Light-Dark switch, and
// the Dark-tuned bright values also read as poor-contrast on the Light variant's pale background.
// Now delegates to SemanticStatusColorConverter, which resolves the live "{key}Brush" resource on
// every binding re-evaluation instead of caching a color.
public sealed class ComponentStatusColorConverter : IValueConverter
{
    public static readonly ComponentStatusColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as string) switch
        {
            "UpToDate" => "Green",
            "UpdateAvailable" => "Amber",
            "NotInstalled" => "Red",
            _ => "Muted"
        };
        return SemanticStatusColorConverter.Instance.Convert(key, targetType, parameter, culture);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
