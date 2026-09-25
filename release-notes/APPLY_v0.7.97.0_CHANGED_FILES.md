# v0.7.97.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.97.0
- `src/MystTiq.Core/Services/CrashSignatureCatalog.cs` (new) — 12 known signatures, exit-code and timestamp reading
- `src/MystTiq.HeadlessHost/CrashAnalysisBuilder.cs` (new) — findings, mod naming, new-versus-repeated, summary, plan
- `src/MystTiq.HeadlessHost/HeadlessCrashAndSaveToolsService.cs` — uses the catalog, optional finding fields, new counts
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs` — analyze route passes installed mod names in
- `src/MystTiq.Desktop/Models/CrashAndSaveToolsDtos.cs`, `ViewModels/MainWindowViewModel.cs`, `MainWindow.axaml` —
  cause/fixes/mods/evidence detail, selected finding, per-server clearing on tab switch
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 7 new scenarios
- `scripts/Test-v0.7.97.0-RouteSmoke.ps1` (new) — live crash analyzer smoke
- `docs/architecture/v0.7.97.0-crash-analyzer-known-causes.md`, `release-notes/v0.7.97.0.md`,
  `release-notes/APPLY_v0.7.97.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.97.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.97.0-Logic.ps1` (new)
