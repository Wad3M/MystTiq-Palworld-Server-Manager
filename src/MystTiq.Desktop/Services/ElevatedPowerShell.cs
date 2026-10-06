// MystTiq v1.0.3.0: file reviewed for this release (2026-10-05).
using System.ComponentModel;
using System.Diagnostics;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v1.0.0.4: runs a PowerShell script as an administrator through Windows' own prompt. Shared by the firewall fix (v0.9.6.0)
/// and the save-folder access fix; each says in its own words what happened.
/// </summary>
public static class ElevatedPowerShell
{
    public enum Outcome { Succeeded, Failed, Declined, NotStarted }

    public static async Task<(Outcome Outcome, int ExitCode, string? Error)> RunAsync(string script, CancellationToken cancellationToken = default)
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
            if (process is null) return (Outcome.NotStarted, -1, null);
            await process.WaitForExitAsync(cancellationToken);
            return (process.ExitCode == 0 ? Outcome.Succeeded : Outcome.Failed, process.ExitCode, null);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return (Outcome.Declined, -1, null);
        }
        catch (Exception ex)
        {
            return (Outcome.NotStarted, -1, ex.Message);
        }
    }
}
