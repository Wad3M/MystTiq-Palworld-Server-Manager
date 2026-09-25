# v0.7.98.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.98.0
- `src/MystTiq.HeadlessHost/DoctorHealthRules.cs` (new) — pure disk, backup, admin access, memory and crash rules
- `src/MystTiq.HeadlessHost/HeadlessDiagnosticsService.cs` — gathers inputs, adds the four finding groups
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs` — passes the backup and crash services in
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 5 new scenarios
- `scripts/Test-v0.7.98.0-RouteSmoke.ps1` (new) — live Doctor smoke
- `docs/architecture/v0.7.98.0-doctor-disk-backups-security-memory-crashes.md`, `release-notes/v0.7.98.0.md`,
  `release-notes/APPLY_v0.7.98.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.98.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.98.0-Logic.ps1` (new)
