// MystTiq v1.0.8.2: file reviewed for this release (2026-10-12).
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
        Need("Operator", Players._loadGameIdsCommand, Players.AddPlayerWarningCommand, AnalyzeCrashesCommand,
            ApplyBackupRetentionCommand, BackupAllCommand, BeginModSafeStartCommand, BeginStuckTestLoadCommand, CancelBackupScanCommand, CancelModSafeStartCommand,
            CreateBackupCommand, DeleteBackupCommand, DismissNotificationCommand, DoctorAllCommand,
            EnvironmentActionCommand, FixDiagnosticCommand, ForceStopServerCommand, InstallMissingEnvironmentCommand,
            InstallPalworldServerFromWizardCommand, MarkAllNotificationsReadCommand, NotificationSelfTestCommand,
            PrepareForWorldImportCommand, PreviewBackupRetentionCommand, RecheckDiagnosticCommand,
            RefreshAllInstancesCommand, RefreshSaveToolsCommand, RefreshTeleportCommand, Players.RefreshTemporaryBansCommand,
            Players.RefreshWhitelistCommand, RepairUpnpMappingCommand, RestartCommand, RestartFromDiagnosticsCommand,
            RestoreBackupCommand, RunAutomationRuleNowCommand, RunSaveToolsSelfTestCommand, Players.SavePlayerNotesCommand,
            SaveWorldNowCommand, StartCommand, StopCommand, TerminateSelectedInstanceCommand, Players.ToggleKitsCommand,
            ToggleNotificationPinCommand, ToggleNotificationReadCommand, Players.ToggleTemporaryBansCommand,
            Players.ToggleWhitelistCommand, UpdateAllCommand, UpdatePalworldServerCommand, VerifyAllBackupsCommand,
            VerifySelectedBackupCommand);
        Need("Admin", ApplyBaseRecoveryCommand, ApplyBaseTransferCommand, ApplyCharacterMigrationCommand,
            ApplyGuildOwnershipCommand, ApplyPalEditCommand, ApplyStarterPresetCommand, ApplyUe4ssInstallCommand,
            ApplyWorldTransactionCommand, Players.BanSelectedPlayerCommand, Players.CancelTemporaryBanCommand,
            CaptureTeleportPositionCommand, CreateAutomationRuleCommand, CreateDefaultServerSettingsCommand,
            Players.CreateTemporaryBanCommand, DeleteAutomationRuleCommand, DeleteSelectedModCommand, DisableAllModsCommand,
            DisableSelectedModCommand, EnableAllModsCommand, EnableSelectedModCommand, FinishInstallAndAdvanceCommand,
            Players.GiveItemSelectedPlayerCommand, ImportSelectedWorkshopModCommand, InstallPalworldServerWithExtrasCommand,
            Players.KickSelectedPlayerCommand, MuteAlertsCommand, PauseDeliveryCommand, PreviewBaseRecoveryCommand, PreviewBaseTransferCommand,
            PreviewCharacterMigrationCommand, PreviewGuildOwnershipCommand, PreviewPalEditCommand,
            PreviewUe4ssInstallCommand, Players.PromoteSelectedPlayerCommand, RconSendCommand, RefreshDiscordBotConfigCommand,
            RepairFirewallCommand, RepairModsCommand, RepairSelectedModCommand, RollbackSelectedModCommand,
            RollbackUe4ssInstallCommand, SaveAlertRulesCommand, SaveAntiCheatRulesCommand, SaveConfigurationCommand,
            SaveDiscordBotConfigCommand, Host.SaveNetworkPolicyCommand, SavePalworldConfigurationCommand,
            Host.SaveResourcePolicyCommand, Players.SaveNameGuardCommand, SaveTeleportCommand, Players.SaveWhitelistCommand, SendPlayerToTeleportPointCommand,
            SendTestNotificationCommand, SetSelectedModDescriptionSourceCommand, TeleportPlayerToMeCommand,
            TeleportToPlayerCommand, ToggleAutomationRuleEnabledCommand, Players.UnbanSelectedPlayerCommand, UpdateComponentCommand,
            UpdateSelectedModCommand, Players.WhisperSelectedPlayerCommand);
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
    // once every command exists (InstallCommandRoleGates has run). v1.0.8.2 (roadmap C-2): the page ViewModels' commands
    // too -- moved out of this class, Save names and Save Whitelist stayed disabled after a busy spell again.
    private AsyncCommand[]? _asyncCommands;
    private void RaiseAllAsyncCommandStates()
    {
        if (_roleGatedCommands.Count == 0) return;
        _asyncCommands ??= new object[] { this }.Concat(_pages)
            .SelectMany(owner => owner.GetType()
                .GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                .Where(p => p.PropertyType == typeof(ICommand) && p.GetIndexParameters().Length == 0)
                .Select(p => p.GetValue(owner)))
            .OfType<AsyncCommand>().Distinct().ToArray();
        foreach (var command in _asyncCommands) command.RaiseCanExecuteChanged();
    }

    // The signed-in role changed: every gated button re-evaluates.
    private void RaiseCommandRoleGatesChanged()
    {
        foreach (var command in _roleGatedCommands) command.RaiseCanExecuteChanged();
        RoleHint.RefreshAll();
    }
}