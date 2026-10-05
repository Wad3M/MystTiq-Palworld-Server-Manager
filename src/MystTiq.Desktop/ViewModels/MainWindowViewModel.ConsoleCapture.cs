// MystTiq v1.0.0.6: file reviewed for this release (2026-10-05).
// MystTiq v1.0.0.1: startup-window console capture controls.
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string _consoleCaptureState = "Not checked";
    private string _consoleCaptureDetail = string.Empty;
    private bool _consoleCapturePackagedAvailable;
    private bool _consoleCaptureInstalled;
    private System.Windows.Input.ICommand? _refreshConsoleCaptureCommand;
    private System.Windows.Input.ICommand? _installConsoleCaptureCommand;
    private System.Windows.Input.ICommand? _uninstallConsoleCaptureCommand;

    public string ConsoleCaptureState { get => _consoleCaptureState; private set => SetField(ref _consoleCaptureState, value); }
    public string ConsoleCaptureDetail { get => _consoleCaptureDetail; private set => SetField(ref _consoleCaptureDetail, value); }
    public bool ConsoleCapturePackagedAvailable
    {
        get => _consoleCapturePackagedAvailable;
        private set
        {
            if (!SetField(ref _consoleCapturePackagedAvailable, value)) return;
            RaisePropertyChanged(nameof(CanInstallConsoleCapture));
        }
    }
    public bool ConsoleCaptureInstalled
    {
        get => _consoleCaptureInstalled;
        private set
        {
            if (!SetField(ref _consoleCaptureInstalled, value)) return;
            RaisePropertyChanged(nameof(CanInstallConsoleCapture));
            RaisePropertyChanged(nameof(CanRemoveConsoleCapture));
        }
    }

    public bool CanInstallConsoleCapture => ConsoleCapturePackagedAvailable && !ConsoleCaptureInstalled;
    public bool CanRemoveConsoleCapture => ConsoleCaptureInstalled;

    public System.Windows.Input.ICommand RefreshConsoleCaptureCommand =>
        _refreshConsoleCaptureCommand ??= new AsyncCommand(RefreshConsoleCaptureAsync);
    public System.Windows.Input.ICommand InstallConsoleCaptureCommand =>
        _installConsoleCaptureCommand ??= new AsyncCommand(InstallConsoleCaptureAsync);
    public System.Windows.Input.ICommand UninstallConsoleCaptureCommand =>
        _uninstallConsoleCaptureCommand ??= new AsyncCommand(UninstallConsoleCaptureAsync);

    private async Task RefreshConsoleCaptureAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { ConsoleCaptureState = "Unavailable"; ConsoleCaptureDetail = ex.Message; return; }

        try { await RefreshConsoleCaptureStatusCoreAsync(profile); }
        catch (Exception ex) { ConsoleCaptureState = "Unavailable"; ConsoleCaptureDetail = ex.Message; }
    }

    private async Task RefreshConsoleCaptureStatusCoreAsync(ConnectionProfile profile)
    {
        var status = await _api.GetConsoleCaptureStatusAsync(profile, BearerToken);
        ConsoleCapturePackagedAvailable = status.PackagedProxyAvailable;
        ConsoleCaptureInstalled = status.Installed;
        ConsoleCaptureState = status.Installed
            ? "Enabled — startup-window text is being captured"
            : status.PackagedProxyAvailable
                ? "Available — not enabled"
                : "Unavailable in this build";
        ConsoleCaptureDetail = status.Detail;
    }

    private async Task InstallConsoleCaptureAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { ConsoleCaptureState = "Install failed"; ConsoleCaptureDetail = ex.Message; return; }

        ConsoleCaptureState = "Enabling startup-window capture…";
        try
        {
            var result = await _api.InstallConsoleCaptureAsync(profile, BearerToken);
            ConsoleCaptureDetail = result.Detail;
            await RefreshConsoleCaptureStatusCoreAsync(profile);
        }
        catch (Exception ex)
        {
            ConsoleCaptureState = "Install failed";
            ConsoleCaptureDetail = ex.Message;
        }
    }

    private async Task UninstallConsoleCaptureAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { ConsoleCaptureState = "Remove failed"; ConsoleCaptureDetail = ex.Message; return; }

        ConsoleCaptureState = "Removing startup-window capture…";
        try
        {
            var result = await _api.UninstallConsoleCaptureAsync(profile, BearerToken);
            ConsoleCaptureDetail = result.Detail;
            await RefreshConsoleCaptureStatusCoreAsync(profile);
        }
        catch (Exception ex)
        {
            ConsoleCaptureState = "Remove failed";
            ConsoleCaptureDetail = ex.Message;
        }
    }
}
