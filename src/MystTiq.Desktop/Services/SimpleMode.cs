// MystTiq v1.1.5.0: file reviewed for this release (2026-10-22).
using MystTiq.Desktop.Models;

namespace MystTiq.Desktop.Services;

/// <summary>
/// v1.1.5.0 (roadmap V-2, owner 2026-10-19: "simple/advanced mode toggle"). Simple shows the everyday work: the Dashboard,
/// Server Setup and the Configuration page's simple settings, Players, Guilds, Bases and the Map, Backups, MOD Library and
/// MOD Sources, the Update Center and the Doctor, Settings, Notifications, the Alert Center and Automation's rules. It hides
/// the specialist pages below from the category tabs and the side menu (the HOST tab has none left), the Configuration
/// page's Advanced view and the automation rule editor. Nothing is disabled: a button that leads to a hidden page still
/// opens it, and Advanced shows everything as before.
/// </summary>
public static class SimpleMode
{
    public static readonly IReadOnlySet<NavigationPage> HiddenPages = new HashSet<NavigationPage>
    {
        NavigationPage.Console, NavigationPage.Workspace, NavigationPage.Launcher,
        NavigationPage.Inspector, NavigationPage.WorldTransactions,
        NavigationPage.ModDashboard, NavigationPage.Ue4ss,
        NavigationPage.CrashAnalyzer, NavigationPage.SaveTools, NavigationPage.DiagnosticsCenter,
        NavigationPage.Host,
        NavigationPage.ActivityAudit, NavigationPage.Security, NavigationPage.Fleet,
    };

    public static bool Hides(NavigationPage page) => HiddenPages.Contains(page);

    public const string Simple = "Simple";
    public const string Advanced = "Advanced";

    public static string Normalize(string? mode) => string.Equals(mode, Simple, StringComparison.OrdinalIgnoreCase) ? Simple : Advanced;
}
