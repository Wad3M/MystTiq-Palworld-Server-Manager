# v0.7.96.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.96.0
- `src/MystTiq.Desktop/Services/PalworldMapCoordinates.cs` — calibrated world-to-image mapping for the expanded map
- `src/MystTiq.Desktop/Services/MapContentsText.cs` (new) — pure "what is on the map" summary
- `src/MystTiq.Desktop/Services/LocalMapPreferencesStore.cs` — `HasSavedPreference` (first run vs deliberate clear)
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — on by default, expanded by default, Palpagos default on
  first run, `ApplyBaseLocations`, `MapPlayersStatusText`
- `src/MystTiq.Desktop/MainWindow.axaml` — card header, description, checkbox label and tooltip, status line, hint
- `scripts/Testing/MystTiq.LogicHarness/MystTiq.LogicHarness.csproj`, `Program.cs` — compiles the two map files in;
  3 new scenarios
- `docs/architecture/v0.7.96.0-live-map-players-and-bases.md`, `release-notes/v0.7.96.0.md`,
  `release-notes/APPLY_v0.7.96.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.96.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.96.0-Logic.ps1` (new)
