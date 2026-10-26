// MystTiq v1.1.5.1: file reviewed for this release (2026-10-26).
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v1.1.5.0 (roadmap V-2, owner 2026-10-19: "simple/advanced mode toggle"; 2026-10-23: "It should hide a bunch of the
/// functions and features to help the user just setup, easy config of the server and start it"). Simple keeps only what
/// setting up, configuring and starting a server needs: the Dashboard (with Start and Stop, the update card and a short
/// guide), Server Setup, the Configuration page's simple settings, Backups and Settings. Every other page is hidden from
/// the category tabs and the side menu (the World, MODs, Tools and HOST tabs have none left), as are the Ribbon's specialist
/// buttons and the Dashboard's monitoring panels. Nothing is disabled: a button that leads to a hidden page still opens it,
/// and Advanced shows everything as before.
/// </summary>
public static class SimpleMode
{
    /// <summary>The pages Simple shows; every other page is hidden.</summary>
    public static readonly IReadOnlySet<NavigationPage> ShownPages = new HashSet<NavigationPage>
    {
        NavigationPage.Dashboard, NavigationPage.ServerSetup, NavigationPage.Configuration, NavigationPage.Backups, NavigationPage.Settings,
    };

    public static readonly IReadOnlySet<NavigationPage> HiddenPages =
        Enum.GetValues<NavigationPage>().Where(page => !ShownPages.Contains(page)).ToHashSet();

    /// <summary>Ribbon buttons Simple leaves out on every page (the pages they open are hidden, or they are for experts).</summary>
    public static readonly IReadOnlySet<string> HiddenRibbonLabels = new HashSet<string> { "Console", "Doctor", "Force Stop" };

    public static bool Hides(NavigationPage page) => HiddenPages.Contains(page);

    /// <summary>The Ribbon Simple shows: without its hidden buttons, and without a group left empty.</summary>
    public static List<RibbonGroupViewModel> FilterRibbon(List<RibbonGroupViewModel> groups, bool simple) =>
        !simple ? groups : groups
            .Select(g => g with { Actions = g.Actions.Where(a => !HiddenRibbonLabels.Contains(a.Label)).ToArray() })
            .Where(g => g.Actions.Count > 0)
            .ToList();

    public const string Simple = "Simple";
    public const string Advanced = "Advanced";

    public static string Normalize(string? mode) => string.Equals(mode, Simple, StringComparison.OrdinalIgnoreCase) ? Simple : Advanced;
}
