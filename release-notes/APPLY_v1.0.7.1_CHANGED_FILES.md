<!-- MystTiq v1.1.4.0: file reviewed for this release (2026-10-20). -->
# v1.0.7.1 Changed Files

- `src/MystTiq.HeadlessHost/OperationHold.cs` (new): holds the coordinator's locks for a piece of work, or says why not.
- `HeadlessBackupService.cs`: scheduled and manual backups hold `world-mutation` (not safety backups); a refusal is
  marked as a conflict.
- `HeadlessServerDistributionService.cs`, `HeadlessModManagementService.cs` (every MOD change), `LocalManagementApiHost.cs`
  (PalDefender update, console capture install and removal, the new constructor arguments): hold `lifecycle`.
- `HeadlessModSafeStartService.cs`: its MOD switches say the server is already held.
- `HeadlessAutomationService.cs`: waits up to 10 minutes for a conflicting operation.
- `HeadlessNotificationRoutingService.cs`: the bounded send queue, two senders, repeat combining, drops and waits recorded,
  the queue's state; `HeadlessNotificationService.cs`: `combineRepeats` (off for the test notification).
- Desktop: `Models/AlertCenterDtos.cs` (the queue), `MainWindow.axaml` (the queue line in Alert Center).
- 2 texts in all 12 languages.
- Tests: `scripts/Test-v1.0.7.1-Logic.ps1` (generated from the v1.0.7.0 gate, every earlier check carried),
  `scripts/Test-v1.0.7.1-RouteSmoke.ps1`, LogicHarness scenarios and an ArtworkHarness check.
