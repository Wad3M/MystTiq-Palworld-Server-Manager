using MystTiq.Core.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MystTiq.Desktop.Services;
using MystTiq.Desktop.ViewModels;

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

            var api = new MystTiqApiClient();
            var profileStore = new JsonConnectionProfileStore();
            var localDiscovery = LocalInstallationDiscoveryService.ForCurrentPlatform();
            var serviceDiscovery = new MystTiqServiceDiscoveryService();
            localBootstrapper = new LocalManagementBootstrapper();
            viewModel = new MainWindowViewModel(api, profileStore, localDiscovery, serviceDiscovery, localBootstrapper);
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
