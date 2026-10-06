// MystTiq v1.0.2.0: file reviewed for this release (2026-10-05).
using System.Diagnostics;
using System.Globalization;

namespace MystTiq.Core.Services;

/// <summary>
/// v1.0.0.1: how long a server has been starting, and when that counts as stuck. A Palworld server opens its game port
/// within seconds (5–9 s for every start logged on the development machine, 2026-07 to 2026-09); the main server there
/// spun one core for minutes without ever opening it (2026-09-30), and the status only said the port "has not been
/// confirmed". Now it says for how long, and calls it stuck after <see cref="StuckAfter"/>.
/// </summary>
public static class StartupWatch
{
    public static readonly TimeSpan StuckAfter = TimeSpan.FromMinutes(2);

    public static DateTimeOffset? ProcessStartedAt(int? processId)
    {
        if (processId is not { } id) return null;
        try
        {
            using var process = Process.GetProcessById(id);
            return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch { return null; }
    }

    public static bool IsStuck(bool ready, DateTimeOffset? startedAt, DateTimeOffset now) =>
        !ready && startedAt is { } started && now - started >= StuckAfter;

    /// <summary>The status line for a server whose process is running but whose game port is not open.</summary>
    public static string NotReadyDetail(int port, DateTimeOffset? startedAt, DateTimeOffset now)
    {
        if (startedAt is not { } started) return $"PalServer process is active; UDP {port} has not been confirmed.";
        var elapsed = Describe(now - started);
        return IsStuck(false, started, now)
            ? $"PalServer has been starting for {elapsed} without opening UDP {port}. It looks stuck; the Dashboard's stuck-start test can show whether a MOD causes it."
            : $"PalServer is starting ({elapsed}); UDP {port} is not open yet.";
    }

    // "45 s", "3 min 12 s", "1 h 4 min".
    public static string Describe(TimeSpan span)
    {
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        var c = CultureInfo.InvariantCulture;
        if (span.TotalMinutes < 1) return string.Format(c, "{0} s", (int)span.TotalSeconds);
        if (span.TotalHours < 1) return string.Format(c, "{0} min {1} s", (int)span.TotalMinutes, span.Seconds);
        return string.Format(c, "{0} h {1} min", (int)span.TotalHours, span.Minutes);
    }
}