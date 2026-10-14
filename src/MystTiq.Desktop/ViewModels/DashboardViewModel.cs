// MystTiq v1.0.8.2: file reviewed for this release (2026-10-12).
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

/// <summary>
/// v1.0.8.1 (roadmap C-1): the Dashboard's display state -- its cards' texts, the recent console activity and the
/// addresses line -- moved out of MainWindowViewModel unchanged. The window's refreshes write the texts here (the status
/// and world readings feed several pages, so they stay with the window); the page binds <c>Dashboard.*</c>. The health
/// state stays with the window: its glow follows the server's start and stop.
/// </summary>
public sealed class DashboardViewModel : PageViewModel
{
    public DashboardViewModel(SessionContext session) : base(session, NavigationPage.Dashboard) { }

    private string _BackupStripText = "Backup: —";
    private string _BackupText = "—";
    private string _GuildText = "—";
    private string _HealthDetail = "Waiting for a server sample.";
    private string _LastActivityText = "Manager started";
    private string _ModText = "—";
    private string _ModsStripText = "Mods: —";
    private string _PlayersSessionText = "0 online";
    private string _PulseBackupText = "Backup: —";
    private string _PulseSaveText = "Save: —";
    private string _RconText = "RCON: Checking";
    private string _RestText = "REST: Checking";
    private string _ServerDescriptionText = "—";
    private string _ServerNameText = "—";
    private string _SessionText = "No active session";
    private string _WorldClockDetailText = "Waiting for authoritative saved-world clock";
    private string _WorldClockText = "Day — • --:--";
    private string _WorldNicknameText = "World —";
    private string _WorldPulseText = "World data not sampled";
    private string _WorldText = "—";

    public string BackupStripText { get => _BackupStripText; internal set => SetField(ref _BackupStripText, value); }
    public string BackupText { get => _BackupText; internal set => SetField(ref _BackupText, value); }
    public string GuildText { get => _GuildText; internal set => SetField(ref _GuildText, value); }
    public string HealthDetail { get => _HealthDetail; internal set => SetField(ref _HealthDetail, value); }
    public string LastActivityText { get => _LastActivityText; internal set => SetField(ref _LastActivityText, value); }
    public string ModText { get => _ModText; internal set => SetField(ref _ModText, value); }
    public string ModsStripText { get => _ModsStripText; internal set => SetField(ref _ModsStripText, value); }
    public string PlayersSessionText { get => _PlayersSessionText; internal set => SetField(ref _PlayersSessionText, value); }
    public string PulseBackupText { get => _PulseBackupText; internal set => SetField(ref _PulseBackupText, value); }
    public string PulseSaveText { get => _PulseSaveText; internal set => SetField(ref _PulseSaveText, value); }
    public string RconText { get => _RconText; internal set => SetField(ref _RconText, value); }
    public string RestText { get => _RestText; internal set => SetField(ref _RestText, value); }
    public string ServerDescriptionVerbatim { get => _ServerDescriptionText; internal set => SetField(ref _ServerDescriptionText, value); }
    public string ServerName { get => _ServerNameText; internal set => SetField(ref _ServerNameText, value); }
    public string SessionText { get => _SessionText; internal set => SetField(ref _SessionText, value); }
    public string WorldClockDetailText { get => _WorldClockDetailText; internal set => SetField(ref _WorldClockDetailText, value); }
    public string WorldClockText { get => _WorldClockText; internal set => SetField(ref _WorldClockText, value); }
    public string WorldNicknameText { get => _WorldNicknameText; internal set => SetField(ref _WorldNicknameText, value); }
    public string WorldPulseText { get => _WorldPulseText; internal set => SetField(ref _WorldPulseText, value); }
    public string WorldText { get => _WorldText; internal set => SetField(ref _WorldText, value); }

    // v1.0.7.3 (roadmap Q-6): replaced in one update, not line by line.
    public BulkObservableCollection<string> ActivityLines { get; } = [];

    private string _localAddressesVerbatim = "—";
    private string _publicAddressVerbatim = "—";
    private string _publicAddressNote = string.Empty;
    private DateTimeOffset _addressesFetchedAt = DateTimeOffset.MinValue;
    private string? _addressesProfileId;

    // Data: addresses and the port, shown exactly as they are.
    public string LocalAddressesVerbatim { get => _localAddressesVerbatim; private set => SetField(ref _localAddressesVerbatim, value); }
    public string PublicAddressVerbatim { get => _publicAddressVerbatim; private set => SetField(ref _publicAddressVerbatim, value); }
    // Text: where the public address came from, or why it is missing (translated).
    public string PublicAddressNote { get => _publicAddressNote; private set => SetField(ref _publicAddressNote, value); }

    internal async Task RefreshHostAddressesAsync(ConnectionProfile profile, object? requestTab, bool force)
    {
        if (!force && _addressesProfileId == profile.Id && DateTimeOffset.UtcNow - _addressesFetchedAt < TimeSpan.FromMinutes(1)) return;
        _addressesFetchedAt = DateTimeOffset.UtcNow;
        _addressesProfileId = profile.Id;
        try
        {
            var addresses = await Session.Api.GetHostAddressesAsync(profile, refresh: force, Session.BearerToken);
            if (!ReferenceEquals(requestTab, Session.ActiveTab) || addresses is null) return;
            var (local, publicAddress, note) = HostAddressText.Describe(addresses);
            LocalAddressesVerbatim = local;
            PublicAddressVerbatim = publicAddress;
            PublicAddressNote = note;
        }
        catch { /* the addresses line keeps what it showed; the next refresh tries again */ }
    }
}
