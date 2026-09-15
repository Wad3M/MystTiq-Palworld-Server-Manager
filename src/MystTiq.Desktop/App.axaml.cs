using MystTiq.Core.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
        }
        base.OnFrameworkInitializationCompleted();
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

    private async void SafeExit_OnClick(object? sender, EventArgs e)
    {
        if (viewModel is not null)
            await viewModel.ShutdownForExitAsync(force: false);
        if (localBootstrapper is not null)
            await localBootstrapper.StopOwnedSidecarAsync();
        RequestExplicitExit();
    }

    private async void ForceExit_OnClick(object? sender, EventArgs e)
    {
        if (viewModel is not null)
            await viewModel.ShutdownForExitAsync(force: true);
        if (localBootstrapper is not null)
            await localBootstrapper.StopOwnedSidecarAsync();
        RequestExplicitExit();
    }

    private void ExitGui_OnClick(object? sender, EventArgs e) => RequestExplicitExit();

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
