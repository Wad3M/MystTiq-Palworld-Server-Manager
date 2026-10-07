// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.ViewModels;

// v1.0.0.1 (reported 2026-10-04: "when I close the application the mysttiq.exe still runs in the background"): closing
// the window always goes to the tray, and Exit from the tray stops every running component. Before, Safe Exit stopped
// only the open tab's server; with two servers in one helper the other kept running, unmanaged once the helper stopped.
public sealed partial class MainWindowViewModel
{
    /// <summary>
    /// Stops every server the given local helper runs (its whole fleet, open as a tab or not), gracefully or forced. With
    /// no owned helper (a Windows service, or a helper another program started), only the open tab's server is stopped,
    /// as before. Servers on other computers are never stopped by closing this app.
    /// </summary>
    public async Task ShutdownAllForExitAsync(bool force, string? ownedHelperEndpoint)
    {
        if (string.IsNullOrWhiteSpace(ownedHelperEndpoint) || !Uri.TryCreate(ownedHelperEndpoint, UriKind.Absolute, out var endpoint))
        {
            await ShutdownForExitAsync(force);
            return;
        }

        var helper = ConnectionProfile.LocalDefault with { BaseAddress = endpoint };
        IReadOnlyList<ServerProfileSummaryDto> servers;
        try { servers = await _api.GetServerProfilesAsync(helper, null, CancellationToken.None); }
        catch (Exception ex)
        {
            LifecycleStatusText = $"Exit shutdown warning: {ex.Message}";
            await ShutdownForExitAsync(force);
            return;
        }

        var running = servers.Where(s => s.Status.IsProcessLive).ToList();
        LifecycleStatusText = running.Count == 0
            ? "No server is running; closing MystTiq."
            : force ? $"Force-stopping the running servers before exit ({running.Count})…" : $"Stopping the running servers safely before exit ({running.Count})…";
        // One at a time: each stop saves its world, and the helper serialises lifecycle work per server anyway.
        foreach (var server in running)
        {
            try
            {
                var scoped = helper with { ServerId = server.Id };
                _ = force
                    ? await _api.ForceStopServerAsync(scoped, null, CancellationToken.None)
                    : await _api.StopServerAsync(scoped, null, CancellationToken.None);
            }
            catch (Exception ex) { LifecycleStatusText = $"Exit shutdown warning ({server.Name}): {ex.Message}"; }
        }
    }
}