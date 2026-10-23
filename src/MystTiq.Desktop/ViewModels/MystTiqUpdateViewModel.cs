// MystTiq v1.1.5.0: file reviewed for this release (2026-10-23).
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

/// <summary>
/// v1.1.5.0 (owner 2026-10-23: "the server update button should be referring to the MystTiq version to update"; chosen:
/// both in one card). The Dashboard's update card's MystTiq line: this app's version against the latest published release
/// (read by the service with the card), and its Update, which runs the Update Center's own MystTiq update (the setup for an
/// installed copy, the portable ZIP for a portable one, the ZIP otherwise).
/// </summary>
public sealed class MystTiqUpdateViewModel(Func<string, Task<string>> update) : ViewModelBase
{
    private string _state = "Checking";
    private string _lineText = string.Empty;
    private string _detail = string.Empty;
    private string? _latest;
    private bool _running;

    /// <summary>The tag (English, coloured by StatusTags).</summary>
    public string State { get => _state; private set { if (SetField(ref _state, value)) RaisePropertyChanged(nameof(CanUpdate)); } }
    public string LineText { get => _lineText; private set => SetField(ref _lineText, value); }
    public string Detail { get => _detail; private set => SetField(ref _detail, value); }
    public string? Latest => _latest;
    public bool CanUpdate => !_running && State == "Update available";

    /// <summary>This app's version, without the leading v.</summary>
    public static string ThisVersion => typeof(MystTiqUpdateViewModel).Assembly.GetName().Version?.ToString(4) ?? "0.0.0.0";

    public void Apply(ServerUpdateCardDto card)
    {
        _latest = card.MystTiqLatest;
        Detail = card.MystTiqDetail ?? string.Empty;
        State = StateFor(ThisVersion, card.MystTiqLatest);
        LineText = LineFor(ThisVersion, card.MystTiqLatest, State);
    }

    /// <summary>Up to date, Update available (a newer published release than this app), or Unavailable (no answer).</summary>
    public static string StateFor(string thisVersion, string? latest) =>
        Version.TryParse(latest, out var newest) && Version.TryParse(thisVersion, out var mine)
            ? newest > mine ? "Update available" : "Up to date"
            : "Unavailable";

    public static string LineFor(string thisVersion, string? latest, string state) =>
        state == "Update available" && latest is { Length: > 0 }
            ? string.Format(Localizer.Instance["dashboard.updates.mysttiqNew"], thisVersion, latest)
            : string.Format(Localizer.Instance["dashboard.updates.mysttiq"], thisVersion);

    /// <summary>Runs the update (after the window's confirmation); returns what happened, for the LIFECYCLE line.</summary>
    public async Task<string> UpdateAsync()
    {
        if (!CanUpdate || _latest is not { } latest) return Localizer.Instance["dashboard.updates.mysttiqCurrent"];
        _running = true;
        RaisePropertyChanged(nameof(CanUpdate));
        try { return await update(latest); }
        finally
        {
            _running = false;
            RaisePropertyChanged(nameof(CanUpdate));
        }
    }
}
