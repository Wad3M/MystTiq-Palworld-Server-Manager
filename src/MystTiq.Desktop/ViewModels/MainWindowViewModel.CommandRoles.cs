// MystTiq v1.1.6.0: file reviewed for this release (2026-10-26).
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
        Need("Operator", Players._loadGameIdsCommand, Players.AddPlayerWarningCommand, Diagnostics.AnalyzeCrashesCommand,
            Backups.ApplyBackupRetentionCommand, Fleet.BackupAllCommand, Mods.BeginModSafeStartCommand, BeginStuckTestLoadCommand, Backups.CancelBackupScanCommand, Mods.CancelModSafeStartCommand,
            Backups.CreateBackupCommand, Backups.DeleteBackupCommand, Alerts.DismissNotificationCommand, Fleet.DoctorAllCommand,
            EnvironmentActionCommand, Diagnostics.FixDiagnosticCommand, ForceStopServerCommand, InstallMissingEnvironmentCommand,
            InstallPalworldServerFromWizardCommand, Alerts.MarkAllNotificationsReadCommand, Alerts.NotificationSelfTestCommand,
            PrepareForWorldImportCommand, Backups.PreviewBackupRetentionCommand, Diagnostics.RecheckDiagnosticCommand,
            RefreshAllInstancesCommand, Backups.RefreshSaveToolsCommand, Map.RefreshTeleportCommand, Players.RefreshTemporaryBansCommand,
            Players.RefreshWhitelistCommand, Diagnostics.RepairUpnpMappingCommand, RestartCommand, Diagnostics.RestartFromDiagnosticsCommand,
            Backups.RestoreBackupCommand, Alerts.RunAutomationRuleNowCommand, Backups.RunSaveToolsSelfTestCommand, Players.SavePlayerNotesCommand,
            SaveWorldNowCommand, StartCommand, StopCommand, TerminateSelectedInstanceCommand, Players.ToggleKitsCommand,
            Alerts.ToggleNotificationPinCommand, Alerts.ToggleNotificationReadCommand, Players.ToggleTemporaryBansCommand,
            Players.ToggleWhitelistCommand, Fleet.UpdateAllCommand, UpdatePalworldServerCommand, Backups.VerifyAllBackupsCommand,
            Backups.VerifySelectedBackupCommand);
        Need("Admin", Mods.SaveLoadOrderCommand, Mods.ApplyProfileImportCommand, World.ApplyBaseRecoveryCommand, World.ApplyBaseTransferCommand, World.ApplyCharacterMigrationCommand,
            World.ApplyGuildOwnershipCommand, World.ApplyPalEditCommand, Configuration.ApplyStarterPresetCommand, Mods.ApplyUe4ssInstallCommand,
            World.ApplyWorldTransactionCommand, Players.BanSelectedPlayerCommand, Players.CancelTemporaryBanCommand,
            Map.CaptureTeleportPositionCommand, Alerts.CreateAutomationRuleCommand, CreateDefaultServerSettingsCommand,
            Players.CreateTemporaryBanCommand, Alerts.DeleteAutomationRuleCommand, Mods.DeleteSelectedModCommand, Mods.DisableAllModsCommand,
            Mods.DisableSelectedModCommand, Mods.EnableAllModsCommand, Mods.EnableSelectedModCommand, FinishInstallAndAdvanceCommand,
            Players.GiveItemSelectedPlayerCommand, Mods.ImportSelectedWorkshopModCommand, InstallPalworldServerWithExtrasCommand,
            Players.KickSelectedPlayerCommand, Alerts.MuteAlertsCommand, Alerts.PauseDeliveryCommand, World.PreviewBaseRecoveryCommand, World.PreviewBaseTransferCommand,
            World.PreviewCharacterMigrationCommand, World.PreviewGuildOwnershipCommand, World.PreviewPalEditCommand,
            Mods.PreviewUe4ssInstallCommand, Players.PromoteSelectedPlayerCommand, RconSendCommand, Alerts.RefreshDiscordBotConfigCommand,
            Diagnostics.RepairFirewallCommand, Mods.RepairModsCommand, Mods.RepairSelectedModCommand, Mods.RollbackSelectedModCommand,
            Mods.RollbackUe4ssInstallCommand, Alerts.SaveAlertRulesCommand, Diagnostics.SaveAntiCheatRulesCommand, Configuration.SaveConfigurationCommand,
            Alerts.SaveDiscordBotConfigCommand, Host.SaveNetworkPolicyCommand, Configuration.SavePalworldConfigurationCommand,
            Host.SaveResourcePolicyCommand, Players.SaveNameGuardCommand, Players.SavePlayerIdsCommand, Players.MoveCharacterCommand, Players.LinkPlayerIdCommand, Map.SaveTeleportCommand, Players.SaveWhitelistCommand, Map.SendPlayerToTeleportPointCommand,
            Alerts.SendTestNotificationCommand, Mods.SetSelectedModDescriptionSourceCommand, Map.TeleportPlayerToMeCommand,
            Map.TeleportToPlayerCommand, Alerts.ToggleAutomationRuleEnabledCommand, Players.UnbanSelectedPlayerCommand, Diagnostics.UpdateComponentCommand,
            Mods.UpdateSelectedModCommand, Players.WhisperSelectedPlayerCommand);
        Need("Owner", CloneIntoNewServerCommand, Fleet.CloneWorldCommand, ContinueFromInstallDirectoryCommand,
            Security.CreatePrincipalCommand, Security.CreateUserCommand, Security.DeleteUserCommand, Security.ResetUserPasswordCommand,
            Security.RevokePrincipalCommand, Security.ToggleUserEnabledCommand);
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