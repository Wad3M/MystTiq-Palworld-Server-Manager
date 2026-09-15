using System.Runtime.InteropServices;
using Avalonia;

namespace MystTiq.Desktop;

internal static class Program
{
    // v0.7.11.0: a second launch must not open a second GUI against the same local sidecar/config
    // -- a named Mutex is the standard single-instance primitive and works cross-platform in
    // .NET (Windows: a real kernel mutex; Unix: backed by a named semaphore), unlike the
    // Windows-only "Global\" mutex-naming convention, which is deliberately not used here.
    private const string SingleInstanceMutexName = "MystTiq.Desktop.SingleInstance";
    private static Mutex? singleInstanceMutex;

    [STAThread]
    public static void Main(string[] args)
    {
        singleInstanceMutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, createdNew: out var createdNew);
        if (!createdNew)
        {
            NotifyAlreadyRunning();
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            singleInstanceMutex.ReleaseMutex();
            singleInstanceMutex.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    // Best-effort, visible notice for a double-launch. A native message box is the simplest way
    // to tell a user who just double-clicked the exe (and has no console attached) that nothing
    // silently failed -- non-Windows platforms fall back to a console message, consistent with
    // this app's existing per-platform graceful-degradation style (e.g. NetworkDiagnosticsService).
    private static void NotifyAlreadyRunning()
    {
        const string message = "MystTiq is already running. Check the system tray, or your taskbar, for the existing window.";
        if (OperatingSystem.IsWindows())
        {
            try { MessageBox(IntPtr.Zero, message, "MystTiq — Palworld Server Manager", MB_OK | MB_ICONINFORMATION); return; }
            catch { /* fall through to console */ }
        }
        Console.Error.WriteLine(message);
    }

    private const uint MB_OK = 0x0;
    private const uint MB_ICONINFORMATION = 0x40;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
