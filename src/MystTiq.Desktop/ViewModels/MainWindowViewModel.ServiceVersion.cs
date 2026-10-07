// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
namespace MystTiq.Desktop.ViewModels;

// v1.0.6.1 (reported 2026-10-06): the desktop connects to whatever service answers on this computer's address. A v1.0.6.0
// desktop ran the server through a v1.0.0.0 service left running, so none of the fixes since v1.0.0.0 applied, including
// the launch fix and the identity guard, and a player got a new character. When the local service is older than this app,
// the Dashboard says so, and Update Service To This Version (MainWindow, after a confirmation) stops the older service and
// starts this app's own; a running PalServer keeps running and is adopted.
public sealed partial class MainWindowViewModel
{
    public static System.Version AppVersion => typeof(MainWindowViewModel).Assembly.GetName().Version ?? new System.Version(0, 0);
    private string _localServiceOutdatedText = string.Empty;
    public string LocalServiceOutdatedText { get => _localServiceOutdatedText; private set { if (SetField(ref _localServiceOutdatedText, value)) RaisePropertyChanged(nameof(IsLocalServiceOutdated)); } }
    public bool IsLocalServiceOutdated => LocalServiceOutdatedText.Length > 0;

    // The local service is older than this app (a remote one is someone else's to update, and is not flagged).
    public static bool IsOlderLocalService(string? serviceVersion, Uri address, System.Version appVersion) =>
        System.Net.IPAddress.TryParse(address.Host, out var ip) && System.Net.IPAddress.IsLoopback(ip) &&
        System.Version.TryParse(serviceVersion, out var service) && service < appVersion;

    private void CheckLocalServiceVersion(string? serviceVersion, Uri address) =>
        LocalServiceOutdatedText = IsOlderLocalService(serviceVersion, address, AppVersion)
            ? $"The MystTiq service on this PC is v{serviceVersion}, older than this app (v{AppVersion}). Your servers run through it, so what changed since v{serviceVersion} is not in effect there, including the launch fixes and the identity guard."
            : string.Empty;

    public async Task UpdateLocalServiceAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _localBootstrapper.StopOutdatedLocalServicesAsync(AppVersion);
            if (result.Stopped.Count == 0)
            {
                LifecycleStatusText = result.Refused.Count > 0
                    ? "The older service could not be stopped: " + string.Join("; ", result.Refused)
                    : "No older MystTiq service was found running on this PC.";
                return;
            }
            ManagementApiConnected = false;
            LocalServiceOutdatedText = string.Empty;
        }
        finally { IsBusy = false; }
        var connected = await EnsureManagementConnectionForLifecycleAsync();
        LifecycleStatusText = connected
            ? $"The older service was stopped and this app's own (v{AppVersion}) runs your servers now."
            : $"The older service was stopped, but this app's service did not start: {LifecycleStatusText}";
    }
}
