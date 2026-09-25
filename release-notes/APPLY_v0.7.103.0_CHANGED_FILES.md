# v0.7.103.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.103.0
- `src/MystTiq.HeadlessHost/DoctorHealthRules.cs` — `ScheduledBackups` rule and its records
- `src/MystTiq.HeadlessHost/HeadlessDiagnosticsService.cs` — the finding, the `create-backup-rule` fix (Admin only,
  idempotent under a lock)
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs` — passes Automation in, tells the fix whether the caller is an admin
- `src/MystTiq.Desktop/Models/DiagnosticFindingDtos.cs`, `MainWindow.axaml` — fix buttons labelled by action
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 2 scenarios
- `scripts/Test-v0.7.103.0-RouteSmoke.ps1` (new) — live schedule and fix smoke
- `docs/architecture/v0.7.103.0-doctor-scheduled-backups.md`, `release-notes/v0.7.103.0.md`,
  `release-notes/APPLY_v0.7.103.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.103.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.103.0-Logic.ps1` (new)
