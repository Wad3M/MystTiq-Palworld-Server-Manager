// MystTiq v1.1.5.1: file reviewed for this release (2026-10-26).
using System.Runtime.InteropServices;

namespace MystTiq.HeadlessHost;

/// <summary>
/// v1.0.8.0: a process started by a parent that ignores Ctrl+C (Git Bash, some launchers and schedulers) inherits that on
/// Windows, so "mysttiq-server api-run" in such a terminal could not be stopped with Ctrl+C at all (found by the v1.0.7.5
/// long set). At start-up the service turns normal Ctrl+C handling back on for itself; its own handler then shuts it down
/// cleanly. Elsewhere it does nothing.
/// </summary>
internal static class ConsoleCtrlC
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(IntPtr handler, bool add);

    public static void Restore()
    {
        if (OperatingSystem.IsWindows()) SetConsoleCtrlHandler(IntPtr.Zero, false);
    }
}
