# v0.7.92.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.92.0
- `src/MystTiq.HeadlessHost/HeadlessPlayerGuildExplorerService.cs` — base coordinate extraction,
  `HeadlessBaseLocation`, `BaseLocations` on the snapshot
- `src/MystTiq.Desktop/Models/WorldMapDtos.cs` — `PlayerId` on `PlayerMapPointDto`, new `BaseMapPointDto`
- `src/MystTiq.Desktop/Models/PlayerGuildExplorerDtos.cs` — `BaseLocationDto`, `BaseLocations`
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — base markers, Refresh Bases, click-to-select,
  tab-switch clearing
- `src/MystTiq.Desktop/MainWindow.axaml` — base marker layer, clickable player dots, teleport action row
- `docs/architecture/v0.7.92.0-world-map-base-markers.md`, `release-notes/v0.7.92.0.md`,
  `release-notes/APPLY_v0.7.92.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.92.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.92.0-Logic.ps1` (new)
