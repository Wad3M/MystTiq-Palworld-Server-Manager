// MystTiq v1.1.6.0: file reviewed for this release (2026-10-26).
namespace MystTiq.Core.Services;

/// <summary>
/// v1.1.2.0 (roadmap F-19): why the server is being stopped. The owner's server stopped cleanly three times with nothing
/// saying why (2026-10-14, 10-16, 10-19). Each caller that stops it (the tray's Exit, a desktop button, Discord,
/// automation, a character move, Safe Start, a restart, the command line, the service stopping, the API) says why with
/// <see cref="Begin"/> around the stop; the lifecycle saves it with the stop in its state file, and the Dashboard shows the
/// last one. It flows with the call (AsyncLocal), so nothing between the caller and the lifecycle has to pass it on.
/// </summary>
public static class ServerStopReason
{
    private static readonly AsyncLocal<string?> current = new();

    public const string Unrecorded = "No reason was recorded for this stop";
    public const int MaximumLength = 200;

    /// <summary>The reason set by the caller, or null.</summary>
    public static string? Current => current.Value;

    /// <summary>The reason to save with a stop: the caller's, else <see cref="Unrecorded"/>.</summary>
    public static string Describe() => current.Value is { Length: > 0 } reason ? reason : Unrecorded;

    /// <summary>Sets the reason for the stops made until the returned scope is disposed.</summary>
    public static IDisposable Begin(string reason)
    {
        var previous = current.Value;
        current.Value = Clean(reason);
        return new Scope(previous);
    }

    /// <summary>One line, at most <see cref="MaximumLength"/> characters (a reason can come from an API caller).</summary>
    public static string Clean(string? reason)
    {
        var line = string.Join(' ', (reason ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (line.Length == 0) return Unrecorded;
        return line.Length <= MaximumLength ? line : line[..(MaximumLength - 1)] + "…";
    }

    private sealed class Scope(string? previous) : IDisposable
    {
        public void Dispose() => current.Value = previous;
    }
}
