# v0.7.102.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.102.0
- `src/MystTiq.Desktop/Models/PalworldConfigurationDtos.cs` — slider coercion-echo guard
- `src/MystTiq.Core/Services/DiskSpaceRules.cs` (new) — the shared low-disk rule
- `src/MystTiq.HeadlessHost/AlertEpisodes.cs` (new) — episode tracker; `HeadlessAlertCenterService.cs` uses it
- `src/MystTiq.HeadlessHost/DoctorHealthRules.cs`, `HeadlessDiagnosticsService.cs`, `LocalManagementApiHost.cs` —
  Doctor uses the shared disk rule and the Alert Center's percentage
- `src/MystTiq.Core/Models/NetworkDiagnosticModels.cs`, `Services/NetworkDiagnosticsService.cs`,
  `src/MystTiq.Desktop/Models/NetworkDiagnosticDtos.cs` — NotRunning network state
- `src/MystTiq.HeadlessHost/HeadlessComponentUpdateService.cs` — newer-than-published wording
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`, `MainWindow.axaml` — Item Corruption range, Restart
  Server gating, retention box width, Automation hint
- `scripts/Testing/MystTiq.LogicHarness/MystTiq.LogicHarness.csproj`, `Program.cs` — 8 scenarios
- `scripts/Test-v0.7.102.0-RouteSmoke.ps1` (new) — live alert episodes smoke
- `docs/architecture/v0.7.102.0-deficiency-fixes.md`, `release-notes/v0.7.102.0.md`,
  `release-notes/APPLY_v0.7.102.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.102.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.102.0-Logic.ps1` (new)
