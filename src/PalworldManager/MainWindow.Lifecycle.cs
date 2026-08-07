using PalworldManager.Services.Infrastructure;

namespace PalworldManager;

/// <summary>
/// Owns main-window startup and shutdown orchestration so constructor setup remains readable
/// and all long-lived resources are released through one predictable path.
/// </summary>
public partial class MainWindow
{
    private bool lifecycleDisposed;
    private readonly CancellationTokenSource windowLifetimeCts = new();

    private void InitializeWindowLifecycle()
    {
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;

        monitorTimer.Tick += MonitorTimer_Tick;
        automationTimer.Tick += AutomationTimer_Tick;
        uiHeartbeatTimer.Tick += UiHeartbeatTimer_Tick;

        monitorTimer.Start();
        automationTimer.Start();
        uiHeartbeatTimer.Start();
        idleWatchdogTimer = new System.Threading.Timer(
            _ => IdleWatchdogTick(),
            null,
            ApplicationConstants.Timing.IdleWatchdogInterval,
            ApplicationConstants.Timing.IdleWatchdogInterval);
    }

    private async void MonitorTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            await MonitorTickAsync();
        }
        catch (OperationCanceledException) when (windowLifetimeCts.IsCancellationRequested)
        {
            // Window is shutting down.
        }
        catch (Exception ex)
        {
            Log($"[MONITOR] Refresh failed: {ex.Message}");
        }
    }

    private void UiHeartbeatTimer_Tick(object? sender, EventArgs e) =>
        lastUiHeartbeatUtc = DateTime.UtcNow;

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var token = windowLifetimeCts.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            if (await Task.Run(server.IsRunning, token))
            {
                var adopted = await Task.Run(server.TryAdoptRunningServer, token);
                Log(adopted
                    ? "[SESSION] Existing PalServer process adopted after manager startup."
                    : "[SESSION] PalServer was detected, but MystTiq could not adopt the process. Stop/Restart will use the discovered-process fallback path.");
                ScheduleRestPollingResume();
                BeginModLoadTracking();
                StartSessionLogTail();
            }

            token.ThrowIfCancellationRequested();
            await MonitorTickAsync();
            token.ThrowIfCancellationRequested();
            await InitializeWorldDataOnStartupAsync();
            token.ThrowIfCancellationRequested();
            await LoadUe4ssReleasesAsync();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Normal shutdown while startup work is still completing.
        }
        catch (Exception ex)
        {
            Log($"[STARTUP] Window initialization failed: {ex.Message}");
            infrastructureNotifications.Publish(
                NotificationLevel.Error,
                ex.Message,
                "Startup",
                "Startup initialization failed");
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (lifecycleDisposed) return;
        lifecycleDisposed = true;

        Loaded -= MainWindow_Loaded;
        Closing -= MainWindow_Closing;
        Closed -= MainWindow_Closed;
        monitorTimer.Tick -= MonitorTimer_Tick;
        automationTimer.Tick -= AutomationTimer_Tick;
        uiHeartbeatTimer.Tick -= UiHeartbeatTimer_Tick;

        try { windowLifetimeCts.Cancel(); } catch (ObjectDisposedException) { }
        StopSessionLogTail();
        CancelAndDispose(ref restResumeCts);
        CancelAndDispose(ref stabilityTestCts);
        CancelAndDispose(ref diagnosticsCts);
        CancelAndDispose(ref activeOperationCts);
        CancelAndDispose(ref logTailCts);

        monitorTimer.Stop();
        automationTimer.Stop();
        uiHeartbeatTimer.Stop();

        idleWatchdogTimer?.Dispose();
        idleWatchdogTimer = null;

        server.OutputReceived -= HandleServerOutput;
        server.ServerExited -= HandleServerExit;
        infrastructureNotifications.Published -= HandleInfrastructureNotification;
        pageOperations.ProgressChanged -= HandlePageOperationProgress;
        activeWorldContext.Changed -= ActiveWorldContext_Changed;

        SafeDispose(server);
        historicalAnalytics?.Flush();
        SafeDispose(sessionLog);
        SafeDispose(modMetadataClient);
        SafeDispose(pageOperations);
        SafeDispose(ue4ssReleases);
        SafeDispose(activeWorldContext);
        SafeDispose(windowLifetimeCts);

        ObserveTask(rcon.DisposeAsync().AsTask(), "RCON disposal");
    }

    private static void CancelAndDispose(ref CancellationTokenSource? source)
    {
        var current = Interlocked.Exchange(ref source, null);
        if (current is null) return;
        try { current.Cancel(); } catch (ObjectDisposedException) { }
        current.Dispose();
    }

    private static void SafeDispose(IDisposable? disposable)
    {
        if (disposable is null) return;
        try { disposable.Dispose(); } catch { }
    }
}
