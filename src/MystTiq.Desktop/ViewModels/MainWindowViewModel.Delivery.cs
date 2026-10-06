// MystTiq v1.0.3.0: file reviewed for this release (2026-10-05).
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.ViewModels;

// v1.0.2.0 (roadmap R-2, alert delivery proof): which switched-on outside channel (Discord, email, webhook) has no
// proven delivery, on the Dashboard (a warning) and in Alert Center (every channel and the latest sends).
public sealed partial class MainWindowViewModel
{
    private NotificationDeliveryHealthDto _deliveryHealth = new();
    public NotificationDeliveryHealthDto DeliveryHealth { get => _deliveryHealth; private set => SetField(ref _deliveryHealth, value); }
    private DateTimeOffset _deliveryHealthFetchedAt = DateTimeOffset.MinValue;
    private string? _deliveryHealthProfileId;

    // At most once a minute from the status poll; at once from Alert Center and after a test send.
    private async Task RefreshDeliveryHealthAsync(ConnectionProfile profile, object? requestTab, bool force)
    {
        if (!force && _deliveryHealthProfileId == profile.Id && DateTimeOffset.UtcNow - _deliveryHealthFetchedAt < TimeSpan.FromMinutes(1)) return;
        _deliveryHealthFetchedAt = DateTimeOffset.UtcNow;
        _deliveryHealthProfileId = profile.Id;
        try
        {
            var health = await _api.GetNotificationDeliveryHealthAsync(profile, BearerToken);
            if (requestTab is not null && !ReferenceEquals(requestTab, ActiveTab)) return;
            DeliveryHealth = health;
        }
        catch { /* an older service has no delivery record: nothing is shown */ }
    }
}
