// MystTiq v0.9.9.0: file reviewed for this release (2026-09-29).
namespace MystTiq.Desktop.Services;

/// <summary>
/// v0.9.6.0: a new server's launch arguments are the current server's with the new server's own port. PalServer binds
/// the -port= launch argument, not PalWorldSettings.ini's PublicPort, so copying the first server's -port= made both
/// servers use the same port, and without one the new server bound 8211 whatever port the wizard was given.
/// </summary>
public static class LaunchArgumentsText
{
    public static string[] WithPort(string? currentArguments, string? gamePort)
    {
        var arguments = (currentArguments ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(a => !a.StartsWith("-port=", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (int.TryParse(gamePort, out var port) && port is > 0 and <= 65535) arguments.Add($"-port={port}");
        return [.. arguments];
    }
}