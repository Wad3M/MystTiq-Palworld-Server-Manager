// MystTiq v1.1.1.3: file reviewed for this release (2026-10-19).
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Threading;
using MystTiq.Core.Services;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Windows.Input;

namespace MystTiq.Desktop.ViewModels;

/// <summary>
/// v1.0.8.9 (roadmap C-9): the Fleet page -- the configured servers, Clone World with its restart, and the fleet actions
/// (Backup All, Doctor All, Update All) -- moved out of MainWindowViewModel unchanged. The window shows it as vm.Fleet; the
/// setup wizard's own clone step stays with the window and reads the port offset from here.
/// </summary>
public sealed class FleetViewModel : PageViewModel
{
    private ILocalInstallationDiscoveryService _localDiscovery { get; }
    private ILocalManagementBootstrapper _localBootstrapper { get; }

    public FleetViewModel(SessionContext session, ILocalInstallationDiscoveryService localDiscovery, ILocalManagementBootstrapper localBootstrapper) : base(session, NavigationPage.Fleet)
    {
        _localDiscovery = localDiscovery;
        _localBootstrapper = localBootstrapper;
        RefreshFleetCommand = new AsyncCommand(RefreshFleetAsync, () => !Session.IsBusy);
        CloneWorldCommand = new AsyncCommand(CloneWorldAsync, () => !Session.IsBusy && Session.IsConnected && !string.IsNullOrWhiteSpace(CloneNewProfileId) && CloneWorldConfirmed);
        RestartAfterCloneCommand = new AsyncCommand(RestartAfterCloneAsync, () => !Session.IsBusy && CloneWorldNeedsRestart);
        BackupAllCommand = new AsyncCommand(BackupAllAsync, () => !Session.IsBusy && Session.IsConnected);
        DoctorAllCommand = new AsyncCommand(DoctorAllAsync, () => !Session.IsBusy && Session.IsConnected);
        UpdateAllCommand = new AsyncCommand(UpdateAllAsync, () => !Session.IsBusy && Session.IsConnected);
    }

    private string _fleetState = "Open Fleet to list configured server profiles.";

    private string _cloneNewProfileId = string.Empty;

    private string _cloneNewProfileName = string.Empty;

    private string _cloneWorldStatusText = "Clones this connection's server (binaries + world) into a new profile. Requires the source server to be stopped.";

    private bool _cloneWorldNeedsRestart;

    private string? _cloneWorldNewProfileId;

    private int _cloneWorldPortOffset = 100;

    private bool _cloneWorldConfirmed;

    public ObservableCollection<ServerProfileSummaryDto> FleetServers { get; } = [];

    public ObservableCollection<FleetActionResultDto> FleetActionResults { get; } = [];

    public string FleetState { get => _fleetState; private set => SetField(ref _fleetState, value); }

    public string CloneNewProfileId { get => _cloneNewProfileId; set { if (SetField(ref _cloneNewProfileId, value ?? string.Empty)) (CloneWorldCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }

    public string CloneNewProfileName { get => _cloneNewProfileName; set => SetField(ref _cloneNewProfileName, value ?? string.Empty); }

    public string CloneWorldStatusText { get => _cloneWorldStatusText; private set => SetField(ref _cloneWorldStatusText, value); }

    // v0.7.84.0: a successful clone here previously left the "needs a MystTiq service restart to
    // come online" text as the only signal, with no in-app way to act on it (the same
    // previously-undisclosed gap closed for the wizard's own Clone path in v0.7.83.0). Unlike the
    // wizard's brand-new, not-yet-in-use tab, this page's connection is already actively in use, so
    // the restart is offered as an explicit button rather than fired automatically -- the user
    // decides when it's convenient to take the whole local service down briefly.
    public bool CloneWorldNeedsRestart
    {
        get => _cloneWorldNeedsRestart;
        private set
        {
            if (!SetField(ref _cloneWorldNeedsRestart, value)) return;
            (RestartAfterCloneCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string? CloneWorldNewProfileId { get => _cloneWorldNewProfileId; private set => SetField(ref _cloneWorldNewProfileId, value); }

    public ICommand RestartAfterCloneCommand { get; }

    // v0.7.81.0: Clone World turned into an explicit step-by-step workflow (direct request: "should
    // be a workflow with drop down options to clarify what it is doing"). The source was always
    // implicitly "whichever profile this tab is connected to" (Session.SelectedProfile) -- now labeled
    // explicitly rather than left unstated. PortOffset already existed on WorldCloneRequestDto but
    // was never exposed in the UI at all (the server just used its own default); now a real dropdown.
    public IReadOnlyList<int> CloneWorldPortOffsetOptions { get; } = [100, 200, 300, 400];

    public int CloneWorldPortOffset { get => _cloneWorldPortOffset; set => SetField(ref _cloneWorldPortOffset, value); }

    public bool CloneWorldConfirmed { get => _cloneWorldConfirmed; set { if (SetField(ref _cloneWorldConfirmed, value)) (CloneWorldCommand as AsyncCommand)?.RaiseCanExecuteChanged(); } }

    public ICommand RefreshFleetCommand { get; }

    public ICommand CloneWorldCommand { get; }

    public ICommand BackupAllCommand { get; }

    public ICommand DoctorAllCommand { get; }

    public ICommand UpdateAllCommand { get; }

    internal async Task RefreshFleetAsync()
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var summaries = await Session.Api.GetServerProfilesAsync(Session.SelectedProfile, Session.BearerToken);
            FleetServers.Clear();
            foreach (var summary in summaries) FleetServers.Add(summary);
            FleetState = $"{FleetServers.Count} server profile(s) configured.";
        }
        catch (Exception ex) { FleetState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    internal async Task CloneWorldAsync()
    {
        if (Session.SelectedProfile is null || string.IsNullOrWhiteSpace(CloneNewProfileId) || !CloneWorldConfirmed) return;
        Session.IsBusy = true;
        CloneWorldStatusText = "Cloning… this copies the entire server installation and can take a while.";
        CloneWorldNeedsRestart = false;
        try
        {
            var newProfileId = CloneNewProfileId.Trim();
            var request = new WorldCloneRequestDto
            {
                NewProfileId = newProfileId,
                NewProfileName = string.IsNullOrWhiteSpace(CloneNewProfileName) ? null : CloneNewProfileName.Trim(),
                PortOffset = CloneWorldPortOffset
            };
            var result = await Session.Api.CloneWorldAsync(Session.SelectedProfile, request, Session.BearerToken);
            CloneWorldStatusText = result.Message;
            if (result.Success)
            {
                // Reset the workflow back to a fresh state rather than leaving a checked
                // confirmation box sitting ready to re-fire against stale profile ID/name text.
                CloneWorldConfirmed = false;
                await RefreshFleetAsync();
                CloneWorldNewProfileId = newProfileId;
                CloneWorldNeedsRestart = true;
            }
        }
        catch (Exception ex) { CloneWorldStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    // v0.7.84.0: explicit, user-triggered restart offered after a successful Clone World here --
    // reuses the exact same RestartOwnedSidecarAsync built for the wizard's own second-server flow
    // in v0.7.83.0. Deliberately does NOT touch this tab's own TargetServerId/reconnect the way the
    // wizard does (this tab should stay on its own already-established profile); it only brings the
    // local service back up with the new profile now visible, matching what Fleet's own profile list
    // already needed a restart+refresh to show.
    private async Task RestartAfterCloneAsync()
    {
        if (string.IsNullOrWhiteSpace(CloneWorldNewProfileId)) return;
        Session.IsBusy = true;
        CloneWorldStatusText = "Restarting the local MystTiq service to bring the new server online…";
        try
        {
            var snapshot = await _localDiscovery.DiscoverAsync();
            if (snapshot.ServiceInstalled)
            {
                CloneWorldStatusText = "This machine runs MystTiq as an installed Windows Service, which the desktop app can't restart on its own -- restart it manually, then refresh this page.";
                return;
            }
            var restartResult = await _localBootstrapper.RestartOwnedSidecarAsync(snapshot, CloneWorldNewProfileId);
            CloneWorldStatusText = restartResult.Available
                ? $"Restarted. '{CloneWorldNewProfileId}' is now online."
                : $"Automatic restart failed: {restartResult.Detail}";
            if (restartResult.Available)
            {
                CloneWorldNeedsRestart = false;
                await RefreshFleetAsync();
            }
        }
        catch (Exception ex) { CloneWorldStatusText = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private async Task RunFleetActionAsync(string label, Func<ConnectionProfile, string?, Task<IReadOnlyList<FleetActionResultDto>>> action)
    {
        if (Session.SelectedProfile is null) return;
        Session.IsBusy = true;
        try
        {
            var results = await action(Session.SelectedProfile, Session.BearerToken);
            FleetActionResults.Clear();
            foreach (var result in results) FleetActionResults.Add(result);
            var failures = results.Count(r => !r.Success);
            FleetState = failures == 0
                ? $"{label}: completed on all {results.Count} server(s)."
                : $"{label}: {failures} of {results.Count} server(s) failed. See results below.";
            await RefreshFleetAsync();
        }
        catch (Exception ex) { FleetState = ex.Message; }
        finally { Session.IsBusy = false; }
    }

    private Task BackupAllAsync() => RunFleetActionAsync("Backup All", (profile, token) => Session.Api.BackupAllAsync(profile, token));

    private Task DoctorAllAsync() => RunFleetActionAsync("Doctor All", (profile, token) => Session.Api.DoctorAllAsync(profile, token));

    private Task UpdateAllAsync() => RunFleetActionAsync("Update All", (profile, token) => Session.Api.UpdateAllAsync(profile, token));
}
