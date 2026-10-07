// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
using System.Linq;
using System.Threading.Tasks;
using MystTiq.Core.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MystTiq.Desktop.Services;
using MystTiq.Desktop.ViewModels;
using MystTiq.Desktop.Views;

namespace MystTiq.Desktop;

public sealed partial class App : Application
{
    private IClassicDesktopStyleApplicationLifetime? desktopLifetime;
    private MainWindow? mainWindow;
    private MainWindowViewModel? viewModel;
    private LocalManagementBootstrapper? localBootstrapper;
    private bool explicitExitRequested;
    private DispatcherTimer? trayStatusTimer;

    public bool IsExplicitExitRequested => explicitExitRequested;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktopLifetime = desktop;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var themeStore = new LocalThemePreferencesStore();
            var (accentTheme, variant) = themeStore.Load();
            ThemeApplier.Apply(accentTheme, variant);

            var api = new MystTiqApiClient();
            var profileStore = new JsonConnectionProfileStore();
            var credentialStore = new CredentialStore();
            var localDiscovery = LocalInstallationDiscoveryService.ForCurrentPlatform();
            var serviceDiscovery = new MystTiqServiceDiscoveryService();
            localBootstrapper = new LocalManagementBootstrapper();
            viewModel = new MainWindowViewModel(api, profileStore, localDiscovery, serviceDiscovery, localBootstrapper, themeStore, credentialStore);
            mainWindow = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = mainWindow;

            // v0.7.73.0: the tray icon is the one thing left visible once the window is hidden or
            // the GUI is fully gone (v0.7.73.0: "no tray icon"
            // now specifically means "nothing running") -- but its tooltip was a static string,
            // never actually saying whether anything's running. Each tab already tracks its own
            // ServerIsRunning; this just periodically summarizes it into the one place still on
            // screen when everything else is hidden. 5s matches this app's other lightweight
            // background-tab polling cadence closely enough that the tray is never far behind what
            // the tabs themselves would show.
            trayStatusTimer = new DispatcherTimer(TimeSpan.FromSeconds(5), DispatcherPriority.Background, (_, _) => UpdateTrayStatus());
            trayStatusTimer.Start();
            UpdateTrayStatus();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void UpdateTrayStatus()
    {
        var icons = TrayIcon.GetIcons(this);
        var trayIcon = icons?.FirstOrDefault();
        if (trayIcon is null || viewModel is null) return;

        var running = viewModel.Tabs.Where(t => t.ServerIsRunning).ToList();
        trayIcon.ToolTipText = running.Count switch
        {
            0 => "MystTiq — idle, nothing running",
            1 => $"MystTiq — running: {running[0].ProfileName}",
            _ => $"MystTiq — {running.Count} servers running: {string.Join(", ", running.Select(t => t.ProfileName))}"
        };
    }

    public void HideMainWindowToTray()
    {
        if (mainWindow is null) return;
        mainWindow.ShowInTaskbar = false;
        mainWindow.Hide();
    }

    // v0.7.11.0: Avalonia's TrayIcon has no balloon/notification API of its own -- see
    // TrayReminderToast's own comment for why this small self-positioned window stands in for one.
    // Best-effort: a positioning/rendering failure here must never prevent the hide-to-tray itself.
    public void ShowTrayStillRunningReminder(string message)
    {
        // v0.7.66.0 bugfix: pass mainWindow as the owner so TrayReminderToast can resolve its target
        // screen from MainWindow's real, already-settled position instead of its own not-yet-placed
        // one -- see TrayReminderToast's own comment for why that mattered on a real multi-monitor
        // report. mainWindow is definitely hidden (not disposed) by this point: this reminder only
        // ever fires right after HideMainWindowToTray's own mainWindow.Hide() call.
        try { new TrayReminderToast(message, mainWindow).Show(); }
        catch { /* the window is already safely hidden to tray regardless of this notice */ }
    }


    // v1.0.0.1: the window's close button always hides MystTiq to the tray; the reminder is shown once per session.
    private bool trayReminderShown;
    public void CloseToTray()
    {
        HideMainWindowToTray();
        if (trayReminderShown) return;
        trayReminderShown = true;
        ShowTrayStillRunningReminder("MystTiq is still running in the tray. Open the tray icon to bring the window back, or choose Exit to stop the servers and MystTiq.");
    }

    private void ShowMainWindow()
    {
        if (mainWindow is null) return;
        mainWindow.ShowInTaskbar = true;
        if (!mainWindow.IsVisible) mainWindow.Show();
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
    }

    private void ShowWindow_OnClick(object? sender, EventArgs e) => ShowMainWindow();

    private void StartServer_OnClick(object? sender, EventArgs e) => Execute(viewModel?.StartCommand);
    private void RestartServer_OnClick(object? sender, EventArgs e) => Execute(viewModel?.RestartCommand);
    private void StopServer_OnClick(object? sender, EventArgs e) => Execute(viewModel?.StopCommand);

    private async void SafeExit_OnClick(object? sender, EventArgs e) => await SafeExitAsync();
    private async void ForceExit_OnClick(object? sender, EventArgs e) => await ForceExitAsync();

    // v0.7.74.0: pulled out of the tray menu's own click handlers so the window's own close-confirm
    // dialog (ConfirmMinimizeToTrayDialog, removed in v1.0.0.1) could offer the same two real exit options directly,
    // instead of only ever offering Cancel/Minimize and telling the user to go find the tray icon
    // afterward if they actually wanted to stop the server -- reported live as an extra, avoidable
    // step. Both paths now run through the exact same shutdown logic.
    public async Task SafeExitAsync()
    {
        // v1.0.0.1: every server the owned helper runs, then the helper (see MainWindowViewModel.ShutdownAllForExitAsync).
        if (viewModel is not null)
            await viewModel.ShutdownAllForExitAsync(force: false, localBootstrapper?.OwnedHelperEndpoint);
        if (localBootstrapper is not null)
            await localBootstrapper.StopOwnedSidecarAsync();
        RequestExplicitExit();
    }

    public async Task ForceExitAsync()
    {
        if (viewModel is not null)
            await viewModel.ShutdownAllForExitAsync(force: true, localBootstrapper?.OwnedHelperEndpoint);
        if (localBootstrapper is not null)
            await localBootstrapper.StopOwnedSidecarAsync();
        RequestExplicitExit();
    }

    private static void Execute(System.Windows.Input.ICommand? command)
    {
        if (command?.CanExecute(null) == true) command.Execute(null);
    }

    private void RequestExplicitExit()
    {
        explicitExitRequested = true;
        desktopLifetime?.Shutdown();
    }
}
