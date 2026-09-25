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
            ApplyBackupRetentionCommand, BackupAllCommand, BeginModSafeStartCommand, CancelModSafeStartCommand,
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
            SaveResourcePolicyCommand, SaveTeleportCommand, SaveWhitelistCommand, SendPlayerToTeleportPointCommand,
            SendTestNotificationCommand, SetSelectedModDescriptionSourceCommand, TeleportPlayerToMeCommand,
            TeleportToPlayerCommand, ToggleAutomationRuleEnabledCommand, UnbanSelectedPlayerCommand, UpdatePipCommand,
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
        _roleGatedCommands = [];
        foreach (var (command, role) in BuildCommandRoles())
        {
            if (command is not IRoleGatedCommand gated) continue;
            var minimum = RoleAccess.Rank(role);
            gated.RoleGate = () => RoleAccess.Allows(CurrentPrincipal is not null, CurrentPrincipal?.Role, minimum);
            _roleGatedCommands.Add(gated);
        }
    }

    // The signed-in role changed: every gated button re-evaluates.
    private void RaiseCommandRoleGatesChanged()
    {
        foreach (var command in _roleGatedCommands) command.RaiseCanExecuteChanged();
    }
}