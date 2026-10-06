// MystTiq v1.0.6.0: file reviewed for this release (2026-10-06).
using System.Windows.Input;
using MystTiq.Desktop.Services;

namespace MystTiq.Desktop.ViewModels;

// v0.8.23.0: every command follows the signed-in role, not only the Ribbon (v0.8.19.0) and a few page buttons. Each
// command that reaches a route needing more than Viewer is listed with that role. The list is derived from the code
// (scripts/Testing/Get-MystTiqCommandRoles.ps1 follows each command to the routes it calls) and the gate fails when the
// two differ. A listed command cannot run below its role, so every button bound to it, on a page or the Ribbon, is
// disabled. Local, token-less use keeps every command, as before.
public sealed partial class MainWindowViewModel
{
    private Dictionary<ICommand, string> BuildCommandRoles()
    {
        var roles = new Dictionary<ICommand, string>(ReferenceEqualityComparer.Instance);
        void Need(string role, params ICommand?[] commands) { foreach (var c in commands) if (c is not null) roles[c] = role; }
        Need("Operator", _loadGameIdsCommand, AddPlayerWarningCommand, AnalyzeCrashesCommand,
            ApplyBackupRetentionCommand, BackupAllCommand, BeginModSafeStartCommand, BeginStuckTestLoadCommand, CancelModSafeStartCommand,
            CreateBackupCommand, DeleteBackupCommand, DismissNotificationCommand, DoctorAllCommand,
            EnvironmentActionCommand, ForceStopServerCommand, InstallMissingEnvironmentCommand,
            InstallPalworldServerFromWizardCommand, MarkAllNotificationsReadCommand, NotificationSelfTestCommand,
            PrepareForWorldImportCommand, PreviewBackupRetentionCommand, RecheckDiagnosticCommand,
            RefreshAllInstancesCommand, RefreshSaveToolsCommand, RefreshTeleportCommand, RefreshTemporaryBansCommand,
            RefreshWhitelistCommand, RepairUpnpMappingCommand, RestartCommand, RestartFromDiagnosticsCommand,
            RestoreBackupCommand, RunAutomationRuleNowCommand, RunSaveToolsSelfTestCommand, SavePlayerNotesCommand,
            SaveWorldNowCommand, StartCommand, StopCommand, TerminateSelectedInstanceCommand, ToggleKitsCommand,
            ToggleNotificationPinCommand, ToggleNotificationReadCommand, ToggleTemporaryBansCommand,
            ToggleWhitelistCommand, UpdateAllCommand, UpdatePalworldServerCommand, VerifyAllBackupsCommand,
            VerifySelectedBackupCommand);
        Need("Admin", ApplyBaseRecoveryCommand, ApplyBaseTransferCommand, ApplyCharacterMigrationCommand,
            ApplyGuildOwnershipCommand, ApplyPalEditCommand, ApplyStarterPresetCommand, ApplyUe4ssInstallCommand,
            ApplyWorldTransactionCommand, BanSelectedPlayerCommand, CancelTemporaryBanCommand,
            CaptureTeleportPositionCommand, CreateAutomationRuleCommand, CreateDefaultServerSettingsCommand,
            CreateTemporaryBanCommand, DeleteAutomationRuleCommand, DeleteSelectedModCommand, DisableAllModsCommand,
            DisableSelectedModCommand, EnableAllModsCommand, EnableSelectedModCommand, FinishInstallAndAdvanceCommand,
            GiveItemSelectedPlayerCommand, ImportSelectedWorkshopModCommand, InstallPalworldServerWithExtrasCommand,
            KickSelectedPlayerCommand, PreviewBaseRecoveryCommand, PreviewBaseTransferCommand,
            PreviewCharacterMigrationCommand, PreviewGuildOwnershipCommand, PreviewPalEditCommand,
            PreviewUe4ssInstallCommand, PromoteSelectedPlayerCommand, RconSendCommand, RefreshDiscordBotConfigCommand,
            RepairFirewallCommand, RepairModsCommand, RepairSelectedModCommand, RollbackSelectedModCommand,
            RollbackUe4ssInstallCommand, SaveAlertRulesCommand, SaveAntiCheatRulesCommand, SaveConfigurationCommand,
            SaveDiscordBotConfigCommand, SaveNetworkPolicyCommand, SavePalworldConfigurationCommand,
            SaveResourcePolicyCommand, SaveNameGuardCommand, SaveTeleportCommand, SaveWhitelistCommand, SendPlayerToTeleportPointCommand,
            SendTestNotificationCommand, SetSelectedModDescriptionSourceCommand, TeleportPlayerToMeCommand,
            TeleportToPlayerCommand, ToggleAutomationRuleEnabledCommand, UnbanSelectedPlayerCommand, UpdateComponentCommand,
            UpdateSelectedModCommand, WhisperSelectedPlayerCommand);
        Need("Owner", CloneIntoNewServerCommand, CloneWorldCommand, ContinueFromInstallDirectoryCommand,
            CreatePrincipalCommand, CreateUserCommand, DeleteUserCommand, ResetUserPasswordCommand,
            RevokePrincipalCommand, ToggleUserEnabledCommand);
        return roles;
    }

    private List<IRoleGatedCommand> _roleGatedCommands = [];

    // Called once every command exists (end of the constructor).
    private void InstallCommandRoleGates()
    {
        RoleHint.Install();
        _roleGatedCommands = [];
        foreach (var (command, role) in BuildCommandRoles())
        {
            if (command is not IRoleGatedCommand gated) continue;
            var minimum = RoleAccess.Rank(role);
            gated.RoleGate = () => RoleAccess.Allows(CurrentPrincipal is not null, CurrentPrincipal?.Role, minimum);
            gated.RequiredRole = role;
            gated.SignedInRole = () => CurrentPrincipal?.Role;
            _roleGatedCommands.Add(gated);
        }
    }

    // v1.0.0.2: every async command re-evaluates when the busy state changes. The list in RaiseIsBusyDependents missed some:
    // Save Whitelist and Save names were disabled while their card loaded (busy) and stayed disabled afterwards. Collected
    // once every command exists (InstallCommandRoleGates has run).
    private AsyncCommand[]? _asyncCommands;
    private void RaiseAllAsyncCommandStates()
    {
        if (_roleGatedCommands.Count == 0) return;
        _asyncCommands ??= typeof(MainWindowViewModel)
            .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Where(p => p.PropertyType == typeof(ICommand) && p.GetIndexParameters().Length == 0)
            .Select(p => p.GetValue(this)).OfType<AsyncCommand>().Distinct().ToArray();
        foreach (var command in _asyncCommands) command.RaiseCanExecuteChanged();
    }

    // The signed-in role changed: every gated button re-evaluates.
    private void RaiseCommandRoleGatesChanged()
    {
        foreach (var command in _roleGatedCommands) command.RaiseCanExecuteChanged();
        RoleHint.RefreshAll();
    }
}