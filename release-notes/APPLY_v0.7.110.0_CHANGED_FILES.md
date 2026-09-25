# v0.7.110.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.110.0
- `src/MystTiq.Core/Services/SupervisorObserver.cs` — new `SupervisorEventKind.ManualRecovery`
- `src/MystTiq.HeadlessHost/HeadlessFleetCrashRecoveryService.cs` — watches after a give-up
  (`RunWithGiveUpWatchAsync`, `WaitForManualRecoveryAsync`, `NotifyManualRecoveryAsync`), resumes the same
  supervisor instance; `HeadlessSupervisor.cs`/`service-run`'s own contract untouched
- `src/MystTiq.HeadlessHost/CrashAlerts.cs` — `CrashAlertText.Build`'s `ManualRecovery` case,
  `CrashAlertObserver.pinnedGiveUpNotificationId` and its unpin-on-ManualRecovery
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 1 new scenario, 1 existing scenario extended
- `scripts/Test-v0.7.110.0-RouteSmoke.ps1` (new) — live give-up/manual-recovery smoke against the real Windows
  lifecycle service
- `docs/architecture/v0.7.110.0-crash-recovery-give-up-watch.md`, `release-notes/v0.7.110.0.md`,
  `release-notes/APPLY_v0.7.110.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.110.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.110.0-Logic.ps1` (new)
