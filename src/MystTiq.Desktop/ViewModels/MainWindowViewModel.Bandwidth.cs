using System.Globalization;
using System.Windows.Input;
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.ViewModels;

// v0.8.18.0: the HOST tab's bandwidth card -- the server's network limits in its Engine.ini (per-player rate and network
// updates per second), MystTiq's policy for them, and what they mean for the connection's upload.
public sealed partial class MainWindowViewModel
{
    private BandwidthSnapshotDto? _bandwidth;
    private int _bandwidthModeIndex;
    private string _perPlayerMbpsText = "8";
    private string _tickRateText = "60";
    private string _uploadBudgetMbpsText = string.Empty;
    private string _bandwidthStatusText = string.Empty;
    private bool _bandwidthEdited;

    public ICommand SaveNetworkPolicyCommand { get; private set; } = null!;
    public ICommand ApplyBandwidthSuggestionCommand { get; private set; } = null!;

    public IReadOnlyList<string> BandwidthModeOptions { get; } = ["The game's own limits (default)", "My limits"];

    public int BandwidthModeIndex
    {
        get => _bandwidthModeIndex;
        set
        {
            if (value is < 0 or > 1 || value == _bandwidthModeIndex) return;
            _bandwidthModeIndex = value; _bandwidthEdited = true;
            RaisePropertyChanged(); RaisePropertyChanged(nameof(IsBandwidthCustom));
        }
    }
    public bool IsBandwidthCustom => BandwidthModeIndex == 1;
    public string PerPlayerMbpsText { get => _perPlayerMbpsText; set { if (SetField(ref _perPlayerMbpsText, value)) _bandwidthEdited = true; } }
    public string TickRateText { get => _tickRateText; set { if (SetField(ref _tickRateText, value)) _bandwidthEdited = true; } }
    public string UploadBudgetMbpsText { get => _uploadBudgetMbpsText; set { if (SetField(ref _uploadBudgetMbpsText, value)) _bandwidthEdited = true; } }
    public string BandwidthStatusText { get => _bandwidthStatusText; private set => SetField(ref _bandwidthStatusText, value); }

    public string BandwidthDefaultsText => _bandwidth is not { } b ? "—"
        : $"The game's own limits on this {b.Platform} server: {Mbps(b.GameDefaultPerPlayerMbps)} per player and {b.GameDefaultTickRate} network updates per second.";

    public string BandwidthNowText => _bandwidth is not { } b ? "—"
        : b.EngineMaxClientRate is null && b.EngineMaxInternetClientRate is null && b.EngineTickRate is null
            ? "Engine.ini sets no network limits: the game's own apply."
            : $"Engine.ini now: {Mbps(b.EffectivePerPlayerMbps)} per player and {b.EffectiveTickRate} network updates per second"
              + (b.Policy.Mode == "Custom" && b.PolicyInEngineIni ? " (MystTiq's limits)." : " (set outside MystTiq).");

    public string BandwidthWorstCaseText => _bandwidth is not { } b ? string.Empty
        : b.MaxPlayers is not { } players || b.WorstCaseUploadMbps is not { } worst
            ? "The maximum player count could not be read from PalWorldSettings.ini, so the most this server could send is not known."
            : $"With all {players} players at {Mbps(b.EffectivePerPlayerMbps)} each, the server could send up to {Mbps(worst)}."
              + (b.Policy.UploadBudgetMbps is { } up ? $" Your upload: {Mbps(up)}." : string.Empty);
    public bool BandwidthOverBudget => _bandwidth?.OverUploadBudget == true;
    public string BandwidthOverBudgetText =>
        "That is more than 80 % of your upload: a full server could fill it and everyone would lag. A lower limit per player keeps room.";
    public string BandwidthSuggestionText => _bandwidth?.SuggestedPerPlayerMbps is { } s && _bandwidth.MaxPlayers is { } p
        ? $"To fit {p} players into 80 % of your upload: {Mbps(s)} per player." : string.Empty;
    public bool HasBandwidthSuggestion => _bandwidth?.SuggestedPerPlayerMbps is not null;
    public bool BandwidthRestartNeeded => _bandwidth?.RestartNeeded == true;
    public string BandwidthRestartText => "Saved, but the server is running: it takes effect at the next start. Restart the server to apply it now.";

    private void InitializeBandwidthCommands()
    {
        SaveNetworkPolicyCommand = new AsyncCommand(SaveNetworkPolicyAsync, () => !IsBusy);
        ApplyBandwidthSuggestionCommand = new RelayCommand(() =>
        {
            if (_bandwidth?.SuggestedPerPlayerMbps is not { } s) return;
            BandwidthModeIndex = 1;
            PerPlayerMbpsText = s.ToString("0.##", CultureInfo.InvariantCulture);
        });
    }

    private void RaiseBandwidthCommandStates() => (SaveNetworkPolicyCommand as AsyncCommand)?.RaiseCanExecuteChanged();

    private void ApplyBandwidthSnapshot(BandwidthSnapshotDto snapshot)
    {
        _bandwidth = snapshot;
        if (!_bandwidthEdited)
        {
            _bandwidthModeIndex = snapshot.Policy.Mode == "Custom" ? 1 : 0;
            _perPlayerMbpsText = snapshot.Policy.PerPlayerMbps.ToString("0.##", CultureInfo.InvariantCulture);
            _tickRateText = snapshot.Policy.TickRate.ToString(CultureInfo.InvariantCulture);
            _uploadBudgetMbpsText = snapshot.Policy.UploadBudgetMbps?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
            foreach (var name in new[] { nameof(BandwidthModeIndex), nameof(IsBandwidthCustom), nameof(PerPlayerMbpsText), nameof(TickRateText), nameof(UploadBudgetMbpsText) })
                RaisePropertyChanged(name);
        }
        foreach (var name in new[] { nameof(BandwidthDefaultsText), nameof(BandwidthNowText), nameof(BandwidthWorstCaseText), nameof(BandwidthOverBudget),
                     nameof(BandwidthSuggestionText), nameof(HasBandwidthSuggestion), nameof(BandwidthRestartNeeded) })
            RaisePropertyChanged(name);
    }

    // Both "2.5" and this computer's own decimal separator are accepted.
    public static bool TryReadNumber(string text, out double value) =>
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
        double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.CurrentCulture, out value);

    private async Task SaveNetworkPolicyAsync()
    {
        if (SelectedProfile is null) return;
        if (!TryReadNumber(PerPlayerMbpsText, out var perPlayer) || perPlayer is < 0.25 or > 100)
        { BandwidthStatusText = "The limit per player must be a number from 0.25 to 100 Mbit/s."; return; }
        if (!int.TryParse(TickRateText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tick) || tick is < 10 or > 120)
        { BandwidthStatusText = "Network updates per second must be a whole number from 10 to 120."; return; }
        double? budget = null;
        if (!string.IsNullOrWhiteSpace(UploadBudgetMbpsText))
        {
            if (!TryReadNumber(UploadBudgetMbpsText, out var up) || up < 1) { BandwidthStatusText = "Your upload speed must be a number of at least 1 Mbit/s, or left empty."; return; }
            budget = up;
        }
        IsBusy = true;
        try
        {
            var result = await _api.SaveNetworkPolicyAsync(SelectedProfile,
                new NetworkPolicyDto { Mode = IsBandwidthCustom ? "Custom" : "GameDefault", PerPlayerMbps = perPlayer, TickRate = tick, UploadBudgetMbps = budget },
                BearerToken);
            BandwidthStatusText = result.Message;
            if (result.Success)
            {
                _bandwidthEdited = false;
                if (result.Snapshot is { } applied) ApplyBandwidthSnapshot(applied);
            }
        }
        catch (Exception ex) { BandwidthStatusText = ex.Message; }
        finally { IsBusy = false; }
    }

    private static string Mbps(double value) => value.ToString(value >= 10 ? "0" : "0.##", CultureInfo.InvariantCulture) + " Mbit/s";
}
