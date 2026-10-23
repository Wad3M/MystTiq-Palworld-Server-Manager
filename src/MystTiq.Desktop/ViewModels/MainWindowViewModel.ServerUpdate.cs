// MystTiq v1.1.5.0: file reviewed for this release (2026-10-23).
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v1.1.4.0 (roadmap U-1, owner 2026-10-19: "add a server update card by adjusting the top bar to fit it there. A button to
// update the server."): the Dashboard's Server update card, beside the shortened LIFECYCLE bar. The build check is the
// service's (SteamCMD's answer is kept 15 minutes there); the update runs on the service (stop, safety backup, SteamCMD,
// start again), so it finishes even if this window closes. While it runs, its step is read every status poll.
public sealed partial class MainWindowViewModel
{
    private static readonly TimeSpan ServerUpdateCheckEvery = TimeSpan.FromMinutes(15);
    private DateTimeOffset _serverUpdateCheckedAt = DateTimeOffset.MinValue;
    private TabSession? _serverUpdateTab;
    private bool _serverUpdateChecking;

    private string _serverUpdateState = "Checking";
    /// <summary>The tag (English, coloured by StatusTags): Up to date, Update available, Updating, Unavailable, Not installed, Checking.</summary>
    public string ServerUpdateState { get => _serverUpdateState; private set { if (SetField(ref _serverUpdateState, value)) RaisePropertyChanged(nameof(CanUpdateServer)); } }

    private string _serverUpdateLineText = string.Empty;
    /// <summary>The build ("Build 25247047", or "25247047 → 25300000" with an update), or the running update's step.</summary>
    public string ServerUpdateLineText { get => _serverUpdateLineText; private set => SetField(ref _serverUpdateLineText, value); }

    private string _serverUpdateDetail = string.Empty;
    public string ServerUpdateDetail { get => _serverUpdateDetail; private set => SetField(ref _serverUpdateDetail, value); }

    private ServerUpdateCardDto? _serverUpdateCard;
    private MystTiqUpdateViewModel? _mystTiqUpdate;
    public MystTiqUpdateViewModel MystTiqUpdate => _mystTiqUpdate ??= new(async v => { await Diagnostics.DownloadMystTiqReleaseAsync(v); return Diagnostics.ComponentUpdateMessage; });
    public async Task UpdateMystTiqAsync() => LifecycleStatusText = await MystTiqUpdate.UpdateAsync();
    public ServerUpdateCardDto? ServerUpdateCard => _serverUpdateCard;

    private bool _isServerUpdateRunning;
    public bool IsServerUpdateRunning { get => _isServerUpdateRunning; private set { if (SetField(ref _isServerUpdateRunning, value)) RaisePropertyChanged(nameof(CanUpdateServer)); } }

    /// <summary>The button: connected, an Admin, a server installed, no update running.</summary>
    public bool CanUpdateServer => ManagementApiConnected && CanManageAdmin && !IsServerUpdateRunning && ServerUpdateState != "Not installed";

    /// <summary>Shows a card answer (also used by the ArtworkHarness).</summary>
    public void ApplyServerUpdateCard(ServerUpdateCardDto card)
    {
        MystTiqUpdate.Apply(card);
        _serverUpdateCard = card;
        RaisePropertyChanged(nameof(ServerUpdateCard));
        ServerUpdateDetail = card.Detail;
        ApplyServerUpdateJob(card.Job, card);
    }

    private void ApplyServerUpdateJob(ServerUpdateJobDto? job, ServerUpdateCardDto? card)
    {
        card ??= _serverUpdateCard;
        IsServerUpdateRunning = job is { Running: true };
        if (job is { Running: true })
        {
            ServerUpdateState = "Updating";
            ServerUpdateLineText = job.Message;
            return;
        }
        ServerUpdateState = card?.State is { Length: > 0 } state && state != "Updating" ? state : "Unavailable";
        ServerUpdateLineText = card is null ? string.Empty : ServerUpdateBuildLine(card);
        // The last update's result stays readable in the tooltip until the next check.
        if (job is { Running: false, FinishedAt: not null } && card is not null)
            ServerUpdateDetail = $"{job.Message}\n\n{card.Detail}".Trim();
    }

    public static string ServerUpdateBuildLine(ServerUpdateCardDto card) => ServerUpdateText.BuildLine(card);

    /// <summary>
    /// From the connection and each status poll: while an update runs, its step (cheap); otherwise the build check when the
    /// tab changed, when asked, or every 15 minutes. Never holds the window busy.
    /// </summary>
    private async Task RefreshServerUpdateCardAsync(ConnectionProfile profile, TabSession? requestTab, bool force)
    {
        if (!ReferenceEquals(requestTab, _serverUpdateTab))
        {
            _serverUpdateTab = requestTab;
            _serverUpdateCheckedAt = DateTimeOffset.MinValue;
            _serverUpdateCard = null;
            ServerUpdateState = "Checking";
            ServerUpdateLineText = string.Empty;
            ServerUpdateDetail = string.Empty;
            IsServerUpdateRunning = false;
        }
        try
        {
            if (IsServerUpdateRunning)
            {
                var job = await _api.GetServerUpdateJobAsync(profile, BearerToken);
                if (!ReferenceEquals(requestTab, ActiveTab)) return;
                ApplyServerUpdateJob(job, null);
                if (job is { Running: false }) _serverUpdateCheckedAt = DateTimeOffset.MinValue; // check the new build next
                return;
            }
            if (_serverUpdateChecking || (!force && DateTimeOffset.UtcNow - _serverUpdateCheckedAt < ServerUpdateCheckEvery)) return;
            _serverUpdateChecking = true;
            var card = await _api.GetServerUpdateAsync(profile, BearerToken);
            _serverUpdateCheckedAt = DateTimeOffset.UtcNow;
            if (!ReferenceEquals(requestTab, ActiveTab)) return;
            ApplyServerUpdateCard(card);
        }
        catch (Exception ex)
        {
            if (!ReferenceEquals(requestTab, ActiveTab)) return;
            _serverUpdateCheckedAt = DateTimeOffset.UtcNow; // an older service without the card is not asked every 5 s
            ServerUpdateState = "Unavailable";
            ServerUpdateLineText = string.Empty;
            ServerUpdateDetail = ex.Message;
        }
        finally { _serverUpdateChecking = false; }
    }

    /// <summary>The confirmation's lines: the builds, what happens to a running server and its players, the safety backup.</summary>
    public IReadOnlyList<string> ServerUpdateConfirmationLines() => ServerUpdateText.ConfirmationLines(_serverUpdateCard, ServerIsRunning, OnlinePlayerCountText);

    /// <summary>Starts the update on the service (after the window's confirmation).</summary>
    public async Task StartServerUpdateAsync()
    {
        ConnectionProfile profile;
        try { profile = BuildProfileFromEditor(SelectedProfile?.Id); }
        catch (Exception ex) { LifecycleStatusText = ex.Message; return; }
        var requestTab = ActiveTab;
        try
        {
            var job = await _api.StartServerUpdateAsync(profile, BearerToken);
            if (!ReferenceEquals(requestTab, ActiveTab)) return;
            ApplyServerUpdateJob(job, null);
            LifecycleStatusText = job.Running ? Localizer.Instance["dashboard.serverUpdate.started"] + " " + job.Message : job.Message;
        }
        catch (Exception ex)
        {
            LifecycleStatusText = Localizer.Instance["dashboard.serverUpdate.refused"] + " " + ex.Message;
        }
    }
}
