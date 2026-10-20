// MystTiq v1.1.2.0: file reviewed for this release (2026-10-19).
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.ViewModels;

// v1.0.0.1 (reported 2026-10-04: "when I close the application the mysttiq.exe still runs in the background"): closing
// the window always goes to the tray, and Exit from the tray stops every running component. Before, Safe Exit stopped
// only the open tab's server; with two servers in one helper the other kept running, unmanaged once the helper stopped.
public sealed partial class MainWindowViewModel
{
    /// <summary>
    /// v1.1.2.0 (roadmap F-19): why the desktop stops a server, sent with the stop and shown later as "Last stop". Kept in
    /// English: they are saved in the server's state and activity log, which other people and the API read too.
    /// </summary>
    public static class StopReasons
    {
        public const string StopButton = "Stop in MystTiq (button or tray menu)";
        public const string ForceStopButton = "Force Stop button in MystTiq";
        public const string RestartButton = "Restart in MystTiq (button or tray menu)";
        public const string TabClosed = "Server tab closed with Stop & Close";
        public const string NewServerImport = "New server: world import preparation";
        public static string Exit(bool force) => force ? "MystTiq closed with Force Exit" : "MystTiq closed with Exit";
    }

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
                    ? await _api.ForceStopServerAsync(scoped, null, CancellationToken.None, StopReasons.Exit(force))
                    : await _api.StopServerAsync(scoped, null, CancellationToken.None, StopReasons.Exit(force));
            }
            catch (Exception ex) { LifecycleStatusText = $"Exit shutdown warning ({server.Name}): {ex.Message}"; }
        }
    }
    /// <summary>
    /// v1.1.2.0 (roadmap F-20): Windows is shutting down or restarting. There is no time for a graceful stop (Windows
    /// ends apps after a few seconds), so each running server this app shows is asked to save its world, waiting at most
    /// <paramref name="budget"/> in all. The server is left in its running state, so the helper resumes it the next time
    /// MystTiq starts.
    /// </summary>
    public void SaveWorldsForSystemShutdown(TimeSpan budget)
    {
        var requests = new List<(ConnectionProfile Profile, string? Token)>();
        foreach (var tab in Tabs.Where(t => t.ServerIsRunning))
        {
            try { requests.Add((BuildProfileFromTab(tab), tab.BearerToken)); }
            catch { /* a tab without a usable profile is skipped */ }
        }
        if (requests.Count == 0) return;
        using var cancellation = new CancellationTokenSource(budget);
        var saves = requests.Select(r => Task.Run(() => _api.SaveWorldNowAsync(r.Profile, r.Token, cancellation.Token))).ToArray();
        try { Task.WaitAll(saves, budget); } catch { /* best effort: Windows is going down either way */ }
    }
}