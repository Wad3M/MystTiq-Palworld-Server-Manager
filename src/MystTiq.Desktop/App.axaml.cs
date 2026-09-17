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
            // the GUI is fully gone (see ExitGui_OnClick's own v0.7.73.0 note on why "no tray icon"
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

    // v0.7.11.0: exposed for MainWindow's Closing handler -- when no server is running in any
    // tab, pressing the window's own close button should behave exactly like "Exit GUI Only" on
    // the tray menu (there's nothing running for the user to lose track of), not silently minimize
    // to tray forever the way it previously always did regardless of server state.
    public void ExitGuiOnlyIfNothingIsRunning() => RequestExplicitExit();

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
    // dialog (ConfirmMinimizeToTrayDialog) can offer the same two real exit options directly,
    // instead of only ever offering Cancel/Minimize and telling the user to go find the tray icon
    // afterward if they actually wanted to stop the server -- reported live as an extra, avoidable
    // step. Both paths now run through the exact same shutdown logic.
    public async Task SafeExitAsync()
    {
        if (viewModel is not null)
            await viewModel.ShutdownForExitAsync(force: false);
        if (localBootstrapper is not null)
            await localBootstrapper.StopOwnedSidecarAsync();
        RequestExplicitExit();
    }

    public async Task ForceExitAsync()
    {
        if (viewModel is not null)
            await viewModel.ShutdownForExitAsync(force: true);
        if (localBootstrapper is not null)
            await localBootstrapper.StopOwnedSidecarAsync();
        RequestExplicitExit();
    }

    // v0.7.73.0: "Exit GUI Only" used to always fully quit the Avalonia app -- including its own
    // tray icon -- even while it deliberately left PalServer/the headless host running in the
    // background. That made "no tray icon" stop meaning "nothing is running," the one thing the
    // tray is supposed to honestly signal (see MainWindow_Closing's own runningCount==0 check,
    // which this now matches). When something is actually running, this collapses to the same
    // minimize-to-tray state the window's own close button already uses, instead of exiting --
    // the tray icon (and therefore an honest "something's running" signal) stays up. Only a
    // genuinely idle app -- nothing running in any tab -- still exits with no tray at all.
    private void ExitGui_OnClick(object? sender, EventArgs e)
    {
        if (viewModel is not null && viewModel.Tabs.Any(t => t.ServerIsRunning))
        {
            HideMainWindowToTray();
            return;
        }
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
