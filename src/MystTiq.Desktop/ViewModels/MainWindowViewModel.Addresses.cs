// MystTiq v1.0.0.5: file reviewed for this release (2026-10-05).
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v1.0.0.1 (requested 2026-09-30): the Dashboard's stuck-start panel and addresses line.
public sealed partial class MainWindowViewModel
{
    private bool _isStartupStuck;
    public bool IsStartupStuck
    {
        get => _isStartupStuck;
        private set { if (SetField(ref _isStartupStuck, value)) RaisePropertyChanged(nameof(ShowStuckStartPanel)); }
    }

    public bool ShowStuckStartPanel => IsStartupStuck || ModSafeStartStatus is not null;
    public bool IsModSafeStartRunning => ModSafeStartStatus is { IsRunning: true };
    public string StuckStartStatusText => StuckStartText.Describe(ModSafeStartStatus);

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

    private async Task RefreshHostAddressesAsync(ConnectionProfile profile, object? requestTab, bool force)
    {
        if (!force && _addressesProfileId == profile.Id && DateTimeOffset.UtcNow - _addressesFetchedAt < TimeSpan.FromMinutes(1)) return;
        _addressesFetchedAt = DateTimeOffset.UtcNow;
        _addressesProfileId = profile.Id;
        try
        {
            var addresses = await _api.GetHostAddressesAsync(profile, refresh: force, BearerToken);
            if (!ReferenceEquals(requestTab, ActiveTab) || addresses is null) return;
            var (local, publicAddress, note) = HostAddressText.Describe(addresses);
            LocalAddressesVerbatim = local;
            PublicAddressVerbatim = publicAddress;
            PublicAddressNote = note;
        }
        catch { /* the addresses line keeps what it showed; the next refresh tries again */ }
    }
}