<!-- MystTiq v1.0.8.6: file reviewed for this release (2026-10-12). -->
# v1.0.7.4 Changed Files

- `src/MystTiq.HeadlessHost/SecretProtector.cs` (new): DPAPI (machine scope) for saved secrets on Windows, owner-only
  files on Linux, the one-time plain copy. The project references `System.Security.Cryptography.ProtectedData`.
- `src/MystTiq.HeadlessHost/HeadlessNotificationRoutingService.cs`: channel secrets encrypted and moved over; failed sends
  logged by host; `GetChannelsView` (no SMTP password); a save without a password keeps the stored one.
- `src/MystTiq.HeadlessHost/HeadlessDiscordBotService.cs`: the bot token encrypted and moved over.
- `src/MystTiq.HeadlessHost/HeadlessDiagnosticsService.cs`: the `security-plain-secrets-copy` finding and its Fix;
  `src/MystTiq.Desktop/Models/DiagnosticFindingDtos.cs`: its button and confirmation texts (12 languages in
  `Assets/i18n`).
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: `GET /notifications/channels` needs Admin and returns the view.
- `src/MystTiq.Core/Services/ArchiveLimits.cs` (new), used by `HeadlessModManagementService.cs` (MOD installs, snapshot
  restores, UE4SS), `HeadlessBackupService.cs` (restores) and `src/MystTiq.Desktop/Services/MystTiqSelfUpdate.cs`.
- `src/MystTiq.Core/Services/SecretRedaction.cs` (new), used by `WindowsServerLifecycleService.cs` (the Arguments line)
  and `HeadlessIdentityGuardService.cs` (the launch advice).
- Tests: `scripts/Test-v1.0.7.4-Logic.ps1` (generated from the v1.0.7.3 gate, every earlier check carried) and three
  LogicHarness scenarios (secrets in logs, secrets at rest, archive limits).
