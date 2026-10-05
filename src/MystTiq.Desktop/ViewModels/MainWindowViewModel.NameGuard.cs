// MystTiq v1.0.0.3: file reviewed for this release (2026-10-05).
using System.Collections.ObjectModel;
using System.Windows.Input;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v1.0.0.2 (asked 2026-10-04): the Players page's Unique player names card. Each name belongs to the first account seen
// with it, or to the account it is reserved for; the service turns away a player using another account's name.
public sealed partial class MainWindowViewModel
{
    private NameGuardConfigDto _nameGuardConfig = new();
    private string _nameGuardState = string.Empty;
    private string _newReservedName = string.Empty;
    private string _newReservedOwner = string.Empty;
    private NameClaimDto? _selectedNameClaim;
    private bool _isNameGuardExpanded;

    public NameGuardConfigDto NameGuardConfig { get => _nameGuardConfig; set => SetField(ref _nameGuardConfig, value ?? new()); }
    public ObservableCollection<NameClaimDto> NameClaims { get; } = [];
    public ObservableCollection<NameGuardEventDto> NameGuardEvents { get; } = [];
    public bool HasNameGuardEvents => NameGuardEvents.Count > 0;
    public string NameGuardState { get => _nameGuardState; private set => SetField(ref _nameGuardState, value); }
    public string NewReservedName { get => _newReservedName; set => SetField(ref _newReservedName, value ?? string.Empty); }
    public string NewReservedOwner { get => _newReservedOwner; set => SetField(ref _newReservedOwner, value ?? string.Empty); }
    public NameClaimDto? SelectedNameClaim { get => _selectedNameClaim; set => SetField(ref _selectedNameClaim, value); }
    public bool IsNameGuardExpanded { get => _isNameGuardExpanded; private set => SetField(ref _isNameGuardExpanded, value); }

    public ICommand ToggleNameGuardCommand { get; }
    public ICommand RefreshNameGuardCommand { get; }
    public ICommand SaveNameGuardCommand { get; }
    public ICommand ReserveNameCommand { get; }
    public ICommand ReleaseNameCommand { get; }

    private void ToggleNameGuard()
    {
        IsNameGuardExpanded = !IsNameGuardExpanded;
        if (IsNameGuardExpanded) Avalonia.Threading.Dispatcher.UIThread.Post(async () => await RefreshNameGuardAsync());
    }

    private async Task RefreshNameGuardAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            var snapshot = await _api.GetNameGuardAsync(SelectedProfile, BearerToken);
            ShowNameGuard(snapshot.Config);
            NameGuardEvents.Clear();
            foreach (var entry in snapshot.Events.OrderByDescending(e => e.At).Take(20)) NameGuardEvents.Add(entry);
            RaisePropertyChanged(nameof(HasNameGuardEvents));
            NameGuardState = NameGuardText.Describe(snapshot.Config);
        }
        catch (Exception ex) { NameGuardState = ex.Message; }
        finally { IsBusy = false; }
    }

    private async Task SaveNameGuardAsync()
    {
        if (SelectedProfile is null) return;
        IsBusy = true;
        try
        {
            NameGuardConfig.Claims = [.. NameClaims];
            var saved = await _api.SaveNameGuardAsync(SelectedProfile, NameGuardConfig, BearerToken);
            ShowNameGuard(saved);
            NameGuardState = NameGuardText.Describe(saved);
        }
        catch (Exception ex) { NameGuardState = ex.Message; }
        finally { IsBusy = false; }
    }

    private void ShowNameGuard(NameGuardConfigDto config)
    {
        NameGuardConfig = config;
        NameClaims.Clear();
        foreach (var claim in config.Claims.OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)) NameClaims.Add(claim);
    }

    // Reserving a name already listed gives it to the new owner (or blocks it); the change is kept when Save is clicked.
    private void ReserveName()
    {
        var (claims, message) = NameGuardText.Reserve(NameClaims, NewReservedName, NewReservedOwner, DateTimeOffset.UtcNow);
        NameGuardState = message;
        if (claims is null) return;
        NameClaims.Clear();
        foreach (var claim in claims) NameClaims.Add(claim);
        NewReservedName = string.Empty;
        NewReservedOwner = string.Empty;
    }

    private void ReleaseSelectedName()
    {
        if (SelectedNameClaim is null) return;
        NameClaims.Remove(SelectedNameClaim);
        SelectedNameClaim = null;
        NameGuardState = "Released once you save. The next player to use the name will own it.";
    }
}
