// MystTiq v1.1.6.0: file reviewed for this release (2026-10-26).
using System.Globalization;
using System.Reflection;
using MystTiq.Core.Services;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v1.0.7.0 (roadmap Q-2): the desktop had no last-chance error handler, so an error escaping any of its fire-and-forget
/// commands or event handlers closed the app and left nothing behind. Every such error is now written to
/// %LOCALAPPDATA%\MystTiq\crashes (the newest 50 are kept) and raised to the window, which shows it and stays open where
/// it can. Errors on the UI thread are marked handled; an error that ends the process is still recorded first.
/// </summary>
public static class CrashRecorder
{
    public const int MaximumRecords = 50;
    private static readonly object Gate = new();
    private static int installed;

    public static string Folder { get; set; } = Path.Combine(
        PortableMode.LocalAppData, "MystTiq", "crashes");

    /// <summary>Raised after an error is recorded: where it was caught, the error, and the record's path (null if it could not be written).</summary>
    public static event Action<string, Exception, string?>? Recorded;

    /// <summary>Process-level handlers; the UI thread's is added by the app once Avalonia runs.</summary>
    public static void Install()
    {
        if (Interlocked.Exchange(ref installed, 1) == 1) return;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex) Record("process", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Record("background task", e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>Writes one crash record and raises <see cref="Recorded"/>. Never throws.</summary>
    public static string? Record(string source, Exception error)
    {
        string? path = null;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Folder);
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
                path = Path.Combine(Folder, $"crash-{stamp}.log");
                var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
                File.WriteAllText(path,
                    $"MystTiq {version}{Environment.NewLine}" +
                    $"When: {DateTimeOffset.Now:O}{Environment.NewLine}" +
                    $"Caught by: {source}{Environment.NewLine}" +
                    $"OS: {Environment.OSVersion}{Environment.NewLine}{Environment.NewLine}" +
                    error);
                foreach (var old in new DirectoryInfo(Folder).GetFiles("crash-*.log").OrderByDescending(f => f.Name).Skip(MaximumRecords))
                    old.Delete();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { path = null; }
        try { Recorded?.Invoke(source, error, path); }
        catch (Exception) { /* a listener failing must not turn one error into two */ }
        return path;
    }
}
