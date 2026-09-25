using System.Windows.Input;
using MystTiq.Desktop.Models;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v0.8.19.0: Ribbon buttons follow the signed-in role, like the cards (v0.8.14.0). Each button that changes something is
// mapped to the role its route needs on the server (where every route now declares one): Operator runs the server,
// Admin changes its configuration and mods. A button the role cannot use is disabled with the role in its tooltip. Local,
// token-less use keeps every button, as before.
public sealed partial class MainWindowViewModel
{
    // Built each time: it is small, and a cached copy made before every command exists would miss some.
    private Dictionary<ICommand, string> RibbonRoles => BuildRibbonRoles();

    private Dictionary<ICommand, string> BuildRibbonRoles()
    {
        var roles = new Dictionary<ICommand, string>(ReferenceEqualityComparer.Instance);
        void Need(string role, params ICommand?[] commands) { foreach (var c in commands) if (c is not null) roles[c] = role; }
        Need("Operator", StartCommand, StopCommand, RestartCommand, ForceStopServerCommand, CreateBackupCommand, RestartFromDiagnosticsCommand,
            BeginModSafeStartCommand, AnalyzeCrashesCommand, RunSaveToolsSelfTestCommand, VerifyAllBackupsCommand, NotificationSelfTestCommand,
            MarkAllNotificationsReadCommand, InstallMissingEnvironmentCommand);
        Need("Admin", SavePalworldConfigurationCommand, EnableAllModsCommand, DisableAllModsCommand, RepairModsCommand,
            ApplyUe4ssInstallCommand, PreviewUe4ssInstallCommand, RollbackUe4ssInstallCommand, RepairFirewallCommand);
        // v0.8.23.0: every other command the role table lists (MainWindowViewModel.CommandRoles.cs), so a Ribbon button
        // says which role it needs whichever command it runs.
        foreach (var (command, role) in BuildCommandRoles()) roles.TryAdd(command, role);
        return roles;
    }

    // The role a Ribbon action needs, or null when any role may use it (reading, local views).
    public string? RibbonRequiredRole(RibbonActionViewModel action) =>
        action.Command is { } command && RibbonRoles.TryGetValue(command, out var role) ? role : null;

    private List<RibbonGroupViewModel> GateRibbonByRole(List<RibbonGroupViewModel> groups)
    {
        var signedIn = CurrentPrincipal is not null;
        return groups.Select(g => g with
        {
            Actions = g.Actions.Select(a =>
            {
                var role = RibbonRequiredRole(a);
                return role is null ? a : a with
                {
                    RequiredRole = role,
                    RoleAllowed = RoleAccess.Allows(signedIn, CurrentPrincipal?.Role, RoleAccess.Rank(role)),
                };
            }).ToArray()
        }).ToList();
    }
}
