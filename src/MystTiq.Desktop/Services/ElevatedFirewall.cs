// MystTiq v0.9.10.0: file reviewed for this release (2026-09-30).
using System.ComponentModel;
using System.Diagnostics;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v0.9.6.0: adding a firewall rule needs administrator rights, and MystTiq's service usually runs as the signed-in user
/// (the Desktop's own sidecar always does). When the service answers that Windows refused the change, the Desktop runs the
/// same script itself through Windows' administrator prompt. Only for a server on this computer: the script changes this
/// computer's firewall, which is the right one only when the server runs here.
/// </summary>
public static class ElevatedFirewall
{
    public static bool CanRun(ConnectionProfile? profile) => OperatingSystem.IsWindows() && profile is not null && profile.BaseAddress.IsLoopback;

    /// <summary>Runs the script as an administrator (Windows asks first) and says whether it succeeded.</summary>
    public static async Task<(bool Success, string Message)> RunAsync(string script, CancellationToken cancellationToken = default)
    {
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes("$ProgressPreference='SilentlyContinue';" + script));
        var start = new ProcessStartInfo(File.Exists(powershell) ? powershell : "powershell.exe",
            "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " + encoded)
        {
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };
        try
        {
            using var process = Process.Start(start);
            if (process is null) return (false, "Windows did not start the firewall change.");
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0
                ? (true, "The firewall rule was added with administrator rights.")
                : (false, $"The firewall change failed as administrator (exit code {process.ExitCode}).");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (false, "Windows asked for administrator rights and the request was declined. The firewall was not changed.");
        }
        catch (Exception ex)
        {
            return (false, $"The firewall change could not be started: {ex.Message}");
        }
    }
}
