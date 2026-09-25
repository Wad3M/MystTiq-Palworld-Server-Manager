# v0.7.104.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.104.0
- `src/MystTiq.HeadlessHost/AlertEpisodes.cs` — `EpisodeState.AlertNotificationId`, `SetAlertNotificationId`,
  `Observe` gained an `out string? recoveredNotificationId` overload (original signature unchanged), `Close` now
  returns the id instead of `void`
- `src/MystTiq.HeadlessHost/HeadlessNotificationService.cs` — `Create` gained an `out string id` overload
  (original signature unchanged)
- `src/MystTiq.HeadlessHost/HeadlessAlertCenterService.cs` — `Track` records/unpins by id, every disabled-rule
  `Close` branch unpins too, new `Unpin` helper
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 2 scenarios
- `scripts/Test-v0.7.104.0-RouteSmoke.ps1` (new) — live unpin-on-recovery and unpin-on-disable smoke
- `docs/architecture/v0.7.104.0-alert-unpin-on-recovery.md`, `release-notes/v0.7.104.0.md`,
  `release-notes/APPLY_v0.7.104.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.104.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.104.0-Logic.ps1` (new)
