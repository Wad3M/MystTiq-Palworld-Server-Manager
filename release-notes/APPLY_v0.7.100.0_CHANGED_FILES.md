# v0.7.100.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.100.0
- `src/MystTiq.HeadlessHost/SavePlayerLocationReader.cs` (new) — last-known player positions from the decoded save
- `src/MystTiq.HeadlessHost/HeadlessPlayerGuildExplorerService.cs` — reads them and returns `playerLocations`
- `src/MystTiq.Desktop/Services/MapViewport.cs` (new) — pure zoom and pan state
- `src/MystTiq.Desktop/Services/MapContentsText.cs` — says how many offline players are drawn
- `src/MystTiq.Desktop/Models/PlayerGuildExplorerDtos.cs`, `Models/WorldMapDtos.cs` — player locations; marker
  online state, on-screen and unzoomed positions
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — viewport, zoom/pan/click-to-zoom, offline markers,
  reset on tab switch
- `src/MystTiq.Desktop/MainWindow.axaml`, `MainWindow.axaml.cs` — zoomable surface, wheel and drag handlers,
  clickable markers and base rows, online/offline markers, zoom controls
- `scripts/Testing/MystTiq.LogicHarness/MystTiq.LogicHarness.csproj`, `Program.cs` — viewport, reader and status
  text scenarios
- `scripts/Test-v0.7.100.0-RouteSmoke.ps1` (new) — live player locations smoke
- `docs/architecture/v0.7.100.0-map-zoom-and-player-positions.md`, `release-notes/v0.7.100.0.md`,
  `release-notes/APPLY_v0.7.100.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.100.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.100.0-Logic.ps1` (new)
