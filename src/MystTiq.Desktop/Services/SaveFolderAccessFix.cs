// MystTiq v1.0.0.6: file reviewed for this release (2026-10-05).
using System.Security.Principal;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v1.0.0.4: found live on 2026-10-05: the server's SaveGames folder belonged to administrators (created by something that
/// ran as administrator), so MystTiq, running as the signed-in user, could not replace it and every restore failed. This
/// gives the signed-in Windows account Modify rights on the server's Pal\Saved folder and everything in it, through
/// Windows' administrator prompt, when the owner clicks Fix Save Folder Access. Nothing else is changed.
/// </summary>
public static class SaveFolderAccessFix
{
    /// <summary>The icacls command: Modify for this account (by SID, so a renamed account still matches), inherited, recursive.</summary>
    public static string Script(string savedFolder, string accountSid) =>
        $"& icacls.exe '{savedFolder.Replace("'", "''")}' /grant '*{accountSid.Replace("'", "''")}:(OI)(CI)M' /T /C /Q; exit $LASTEXITCODE";

    public static string? CurrentAccountSid() => OperatingSystem.IsWindows() ? WindowsIdentity.GetCurrent().User?.Value : null;

    public static async Task<(bool Success, string Message)> RunAsync(string savedFolder, CancellationToken cancellationToken = default)
    {
        var sid = CurrentAccountSid();
        if (sid is null) return (false, "Windows did not start the save folder access change.");
        var (outcome, exitCode, error) = await ElevatedPowerShell.RunAsync(Script(savedFolder, sid), cancellationToken);
        return outcome switch
        {
            ElevatedPowerShell.Outcome.Succeeded => (true, "Your Windows account can now change the save folder. Restore again."),
            ElevatedPowerShell.Outcome.Failed => (false, $"The save folder access could not be changed as administrator (exit code {exitCode})."),
            ElevatedPowerShell.Outcome.Declined => (false, "Windows asked for administrator rights and the request was declined. The save folder was not changed."),
            _ => (false, error is null ? "Windows did not start the save folder access change." : $"The save folder access change could not be started: {error}"),
        };
    }
}
