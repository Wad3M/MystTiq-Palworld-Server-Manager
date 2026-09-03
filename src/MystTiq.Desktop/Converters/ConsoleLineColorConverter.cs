using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace MystTiq.Desktop.Converters;

public sealed class ConsoleLineColorConverter : IValueConverter
{
    public static readonly ConsoleLineColorConverter Instance = new();

    private static readonly IBrush Error = new SolidColorBrush(Color.Parse("#FF646D"));
    private static readonly IBrush Warning = new SolidColorBrush(Color.Parse("#F4B83F"));
    private static readonly IBrush Status = new SolidColorBrush(Color.Parse("#35D5E8"));
    private static readonly IBrush MystTiqBrush = new SolidColorBrush(Color.Parse("#A997FF"));
    private static readonly IBrush RestBrush = new SolidColorBrush(Color.Parse("#52C4FF"));
    private static readonly IBrush ModsBrush = new SolidColorBrush(Color.Parse("#E676FF"));
    private static readonly IBrush PlayersBrush = new SolidColorBrush(Color.Parse("#63DF9A"));
    private static readonly IBrush WorkspaceBrush = new SolidColorBrush(Color.Parse("#8CA8C2"));
    private static readonly IBrush Info = new SolidColorBrush(Color.Parse("#58E36B"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var line = value as string ?? "";
        if (Contains(line, "ERROR") || Contains(line, "FATAL") || Contains(line, "EXCEPTION")) return Error;
        if (Contains(line, "WARN")) return Warning;
        if (Contains(line, "[MYSTTIQ]")) return MystTiqBrush;
        if (Contains(line, "[STATUS")) return Status;
        if (Contains(line, "[PLAYERS]")) return PlayersBrush;
        if (Contains(line, "[WORKSPACE]")) return WorkspaceBrush;
        if (Contains(line, "REST") || Contains(line, "/api/v1/")) return RestBrush;
        if (Contains(line, "[UE4SS]") || Contains(line, "[MODS]") || Contains(line, "PalDefender") || Contains(line, "AdminCommands") || Contains(line, "UE4SS")) return ModsBrush;
        return Info;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static bool Contains(string line, string token) =>
        line.Contains(token, StringComparison.OrdinalIgnoreCase);
}
