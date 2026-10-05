// MystTiq v1.0.1.0: file reviewed for this release (2026-10-05).
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
        var (outcome, exitCode, error) = await ElevatedPowerShell.RunAsync(script, cancellationToken);
        return outcome switch
        {
            ElevatedPowerShell.Outcome.Succeeded => (true, "The firewall rule was added with administrator rights."),
            ElevatedPowerShell.Outcome.Failed => (false, $"The firewall change failed as administrator (exit code {exitCode})."),
            ElevatedPowerShell.Outcome.Declined => (false, "Windows asked for administrator rights and the request was declined. The firewall was not changed."),
            _ => (false, error is null ? "Windows did not start the firewall change." : $"The firewall change could not be started: {error}"),
        };
    }
}
