// MystTiq v1.0.6.1: file reviewed for this release (2026-10-06).
namespace MystTiq.Desktop.Models;

public enum NavigationPage
{
    Dashboard,
    ServerSetup, Configuration, Backups, Console, Workspace,
    Inspector, WorldTransactions, Players, Bases, Guilds,
    ModDashboard, ModLibrary, Ue4ss,
    UpdateCenter, Doctor, CrashAnalyzer, SaveTools, DiagnosticsCenter,
    Settings, ActivityAudit, Notifications,
    Automation, Security, AlertCenter, Fleet,
    // v0.7.99.0: appended, not inserted next to Guilds, so a page saved by number in an older tab
    // session still means the same page.
    Map,
    // v0.8.17.0: the HOST tab, appended for the same reason.
    Host,
    // v1.0.0.1: launcher diagnostics page; appended so saved numeric page values remain compatible.
    Launcher
}
