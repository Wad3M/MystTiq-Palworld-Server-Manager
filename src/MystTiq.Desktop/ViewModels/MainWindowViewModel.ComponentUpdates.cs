// MystTiq v1.0.3.0: file reviewed for this release (2026-10-05).
using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Threading;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v1.0.1.0 (asked 2026-10-05: "the update page should have a button to update every option. It can be greyed out if it is
// self updating, but should always have the option to update"): the Update button on every Update Center row. What it does
// is the row's ComponentVersionDto.UpdateMethod; each reuses the app's existing path where there is one (SteamCMD for the
// server files, the UE4SS page's preview-then-apply, pip) and the new ones are PalDefender, Palworld Save Tools and MystTiq.
public sealed partial class MainWindowViewModel
{
    public ICommand UpdateComponentCommand { get; }

    private string _componentUpdateMessage = string.Empty;
    // The last Update's outcome, shown at the top of the component list.
    public string ComponentUpdateMessage
    {
        get => _componentUpdateMessage;
        private set { if (SetField(ref _componentUpdateMessage, value)) RaisePropertyChanged(nameof(HasComponentUpdateMessage)); }
    }
    public bool HasComponentUpdateMessage => !string.IsNullOrEmpty(ComponentUpdateMessage);

    private void UpdateComponent(ComponentVersionDto? component)
    {
        if (component is null || !component.CanUpdate) return;
        Dispatcher.UIThread.Post(async () => await UpdateComponentAsync(component));
    }

    public async Task UpdateComponentAsync(ComponentVersionDto component)
    {
        if (IsBusy)
        {
            ComponentUpdateMessage = "Another operation is running; try Update again when it has finished.";
            return;
        }
        switch (component.UpdateMethod)
        {
            case ComponentUpdateMethod.ServerFiles:
                ComponentUpdateMessage = $"Updating {component.Component}…";
                await UpdatePalworldServerAsync();
                ComponentUpdateMessage = DistributionDetail;
                await RefreshComponentVersionsAsync();
                break;
            case ComponentUpdateMethod.Ue4ssInstall:
                Navigate(nameof(NavigationPage.Ue4ss));
                await SelectAndPreviewLatestUe4ssAsync();
                break;
            case ComponentUpdateMethod.Pip:
                await UpdatePipAsync();
                ComponentUpdateMessage = DistributionDetail;
                break;
            case ComponentUpdateMethod.PalDefender:
                await RunComponentUpdateAsync("PalDefender", (profile, bearer) => _api.UpdatePalDefenderAsync(profile, bearer));
                break;
            case ComponentUpdateMethod.SaveTools:
                await RunComponentUpdateAsync("Palworld Save Tools", (profile, bearer) => _api.UpdateSaveToolsAsync(profile, bearer));
                break;
            case ComponentUpdateMethod.DownloadRelease:
                await DownloadMystTiqReleaseAsync(component.LatestVersion);
                break;
            case ComponentUpdateMethod.OfficialPage:
                if (component.SourceUrl is { } url && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                {
                    try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); ComponentUpdateMessage = $"Opened {url}"; }
                    catch (Exception ex) { ComponentUpdateMessage = ex.Message; }
                }
                break;
        }
    }

    private async Task RunComponentUpdateAsync(string name, Func<ConnectionProfile, string?, Task<ComponentUpdateResultDto>> update)
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { ComponentUpdateMessage = ex.Message; return; }

        IsBusy = true;
        ComponentUpdateMessage = $"Updating {name}…";
        try
        {
            var result = await update(profile, BearerToken);
            ComponentUpdateMessage = result.Message;
        }
        catch (Exception ex) { ComponentUpdateMessage = ex.Message; }
        finally { IsBusy = false; }
        await RefreshComponentVersionsAsync();
    }

    private async Task DownloadMystTiqReleaseAsync(string version)
    {
        IsBusy = true;
        ComponentUpdateMessage = $"Downloading MystTiq v{version}…";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("MystTiq-Palworld-Server-Manager");
            var fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MystTiq", "versions");
            var progress = new Progress<string>(text => ComponentUpdateMessage = text);
            var result = await new MystTiqSelfUpdate(http).DownloadAsync(version, AppContext.BaseDirectory, fallback, progress, CancellationToken.None);
            ComponentUpdateMessage = result.Message;
            if (result.Success && result.Folder is { } folder && OperatingSystem.IsWindows())
            {
                try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true }); } catch { }
            }
        }
        catch (Exception ex) { ComponentUpdateMessage = $"MystTiq v{version} could not be downloaded: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // The UE4SS page with the newest release selected and its install previewed; the user applies it there.
    private async Task<bool> SelectAndPreviewLatestUe4ssAsync()
    {
        if (Ue4ssPalworldForkReleases.Count == 0)
            await RefreshUe4ssReleaseCatalogAsync();

        var latest = Ue4ssPalworldForkReleases.Where(r => !r.Prerelease).OrderByDescending(r => r.PublishedAt).FirstOrDefault()
            ?? Ue4ssPalworldForkReleases.OrderByDescending(r => r.PublishedAt).FirstOrDefault();
        if (latest is null)
        {
            Ue4ssInstallState = "No UE4SS release catalog data was available to auto-install from.";
            return false;
        }

        SelectedUe4ssFork = "Palworld Fork";
        SelectedUe4ssRelease = latest;
        await PreviewUe4ssInstallAsync();
        return true;
    }
}
