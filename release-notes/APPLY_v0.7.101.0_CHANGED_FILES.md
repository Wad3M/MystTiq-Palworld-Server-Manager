# v0.7.101.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.101.0
- `src/MystTiq.Core/Services/SupervisorObserver.cs` (new) — recovery events and the observer interface
- `src/MystTiq.Core/Services/HeadlessSupervisor.cs` — reports crash, recovery, failed restart and giving up
- `src/MystTiq.HeadlessHost/CrashAlerts.cs` (new) — the alert text and the observer that sends the notification
- `src/MystTiq.HeadlessHost/HeadlessFleetCrashRecoveryService.cs`, `LocalManagementApiHost.cs` — every profile's
  recovery is composed with the observer
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — scripted server, recording observer, 4 scenarios
- `scripts/Test-v0.7.101.0-RouteSmoke.ps1` (new) — live crash alerts smoke
- `docs/architecture/v0.7.101.0-crash-alerts.md`, `release-notes/v0.7.101.0.md`,
  `release-notes/APPLY_v0.7.101.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.101.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.101.0-Logic.ps1` (new)
