// MystTiq v1.1.5.0: file reviewed for this release (2026-10-24).
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v1.1.4.0 (roadmap U-1): the Dashboard's Server update card's text: the build line and the confirmation's lines. Moved out
/// of the window's view model in v1.1.5.0 (it holds no window state), so the view model stays within its size budget.
/// </summary>
public static class ServerUpdateText
{
    /// <summary>"Build 25247047", or "25247047 → 25300000" when an update is out.</summary>
    public static string BuildLine(ServerUpdateCardDto card) =>
        card.State == "Update available" && card.Latest is { Length: > 0 } latest && latest != card.Installed
            ? string.Format(Localizer.Instance["dashboard.updates.serverNew"], card.Installed, latest)
            : string.Format(Localizer.Instance["dashboard.updates.server"], card.Installed);

    /// <summary>"Server build —": before the service's first answer, while not connected, or when it has none (O-7, 2026-10-24:
    /// the line was empty and only the card's "Checking" tag showed).</summary>
    public static string NoBuildLine => string.Format(Localizer.Instance["dashboard.updates.server"], "—");

    /// <summary>The builds, what happens to a running server and its players, the safety backup, and a note when current.</summary>
    public static IReadOnlyList<string> ConfirmationLines(ServerUpdateCardDto? card, bool serverRunning, string playersOnline)
    {
        var lines = new List<string>();
        if (card is not null)
            lines.Add(string.Format(Localizer.Instance["dashboard.serverUpdate.builds"], card.Installed, card.Latest));
        lines.Add(serverRunning
            ? string.Format(Localizer.Instance["dashboard.serverUpdate.running"], playersOnline)
            : Localizer.Instance["dashboard.serverUpdate.stopped"]);
        lines.Add(Localizer.Instance["dashboard.serverUpdate.safety"]);
        if (card?.State == "Up to date") lines.Add(Localizer.Instance["dashboard.serverUpdate.current"]);
        return lines;
    }
}
