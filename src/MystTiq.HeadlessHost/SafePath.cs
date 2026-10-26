// MystTiq v1.1.6.0: file reviewed for this release (2026-10-26).
namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.1.5.1 (GitHub code scanning, CodeQL "Uncontrolled data used in path expression", 2026-10-26): file names built from
/// what an API request sends (a MOD's type and package, a player ID) are checked twice: the value itself has the shape it
/// must have, and the finished path is resolved and must stay inside the folder it was meant for. A request carrying
/// "..\" (ASP.NET decodes %5C in a route segment) can no longer reach a file outside that folder.
/// </summary>
public static class SafePath
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>A Palworld player ID as save files name it: exactly 32 hexadecimal characters.</summary>
    public static bool IsPlayerId(string? value) => value is { Length: 32 } && value.All(Uri.IsHexDigit);

    /// <summary>
    /// The full path of <paramref name="name"/> inside <paramref name="root"/>; throws when the result would be the folder
    /// itself or anywhere outside it.
    /// </summary>
    public static string ChildOf(string root, string name)
    {
        var rootFull = Path.GetFullPath(root);
        var prefix = rootFull.EndsWith(Path.DirectorySeparatorChar) ? rootFull : rootFull + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(rootFull, name));
        if (!full.StartsWith(prefix, PathComparison))
            throw new InvalidDataException($"The name \"{name}\" leads outside {rootFull}.");
        return full;
    }
}
