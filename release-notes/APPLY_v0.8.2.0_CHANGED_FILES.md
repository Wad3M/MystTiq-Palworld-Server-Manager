# v0.8.2.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.2.0.
- `src/MystTiq.HeadlessHost/Program.cs`: both `service-run` branches.
  - Build the lifecycle with the per-profile factory, so it uses the configured game port.
  - Take the service profile's crash recovery over from the API host.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: `TakeOverCrashRecovery`; no second crash-recovery loop for
  an externally supervised profile.
- `src/MystTiq.HeadlessHost/ServerProfileHost.cs`: `CrashAlerts` and `RecoveryState` exposed per profile.
- `src/MystTiq.Core/Services/HeadlessSupervisor.cs`
  - Records its own give-up.
  - Announces the recovery on a start after one.
- `src/MystTiq.HeadlessHost/HeadlessFleetCrashRecoveryService.cs`: duplicate give-up record removed.
- `src/MystTiq.HeadlessHost/CrashAlerts.cs`: a new DOWN notice unpins the previous one.
- `scripts/Testing/FakePalServer/` (new): the stand-in PalServer used by the smoke.
- `scripts/Testing/MystTiq.LogicHarness/Program.cs`: two "Service mode" scenarios.
- `scripts/Test-v0.8.2.0-RouteSmoke.ps1` (new).
- `scripts/Test-v0.8.2.0-Logic.ps1` (new).
- New docs: `docs/architecture/v0.8.2.0-service-mode-fixes.md`, `release-notes/v0.8.2.0.md`,
  `release-notes/APPLY_v0.8.2.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.8.2.0.md`.
- Updated docs: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`. These also
  record that v0.8.1.0 is the accepted baseline.
