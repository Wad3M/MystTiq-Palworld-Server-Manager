<!-- MystTiq v1.0.7.2: file reviewed for this release (2026-10-11). -->
# v1.0.7.0 Changed Files

- `src/MystTiq.HeadlessHost/SavedFiles.cs` (new): reading and saving MystTiq's lists (last good copy, unreadable copy
  kept, refusal, repair).
- Every list moved onto it: `HeadlessUserAccountService`, `HeadlessRbacService`, `HeadlessAutomationService`,
  `HeadlessKitService`, `HeadlessAlertCenterService`, `HeadlessAntiCheatService`, `HeadlessNotificationRoutingService`,
  `HeadlessDiscordBotService`, `HeadlessIdentityGuardService`, `HeadlessNameGuardService`, `HeadlessWhitelistService`,
  `HeadlessTemporaryBanService`, `HeadlessTeleportService`, `HeadlessPlayerMetadataService`,
  `HeadlessPlayerRegistryService`, `HeadlessResourcePolicyService`.
- `HeadlessDiagnosticsService.cs`: the Saved data findings and their repair; `LocalManagementApiHost.cs`: the alert with
  its link, HTTP 409 for a refused change, the background tracker at shutdown; `HeadlessNotificationService.cs`: a
  notification's link.
- `src/MystTiq.HeadlessHost/BackgroundWork.cs` (new) and every former `Task.Run` site; `Program.cs`: last-chance handlers.
- `src/MystTiq.HeadlessHost/FileRetry.cs`: only a held file is retried; `src/MystTiq.Core/Services/ServerLifecycleStateStore.cs`:
  a lasting failure says what to check.
- Desktop: `Services/CrashRecorder.cs` (new), `ViewModels/MainWindowViewModel.Errors.cs` (new, `RunUi` and the error banner),
  `Program.cs`, `App.axaml.cs`, `Services/MystTiqApiClient.cs` (a refused change shows its reason), `Models/NotificationDtos.cs`,
  `Models/DiagnosticFindingDtos.cs`, `MainWindow.axaml` (the banner, Open Repair, the FROM ALERT tag).
- 14 texts in all 12 languages.
- Tests: `scripts/Test-v1.0.7.0-Logic.ps1` (generated from the v1.0.6.1 gate, every earlier check carried),
  `scripts/Test-v1.0.7.0-RouteSmoke.ps1`, LogicHarness scenarios and ArtworkHarness checks.
