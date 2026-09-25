# v0.8.11.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.11.0.
- `src/MystTiq.HeadlessHost/SavePalLocationReader.cs` (new): `SavedPalLocation`, `SavedPalLocations`,
  `SavePalLocationReader`.
- `src/MystTiq.HeadlessHost/HeadlessPlayerGuildExplorerService.cs`:
  - the Pals are read inside the existing parse;
  - they are named by owner and guild;
  - new `HeadlessPalLocation` and `HeadlessPalSummary`;
  - optional `PalLocations` and `PalSummary` on the snapshot.
- `src/MystTiq.Desktop/Models/PlayerGuildExplorerDtos.cs`: `PalLocationDto`, `PalSummaryDto`, and the snapshot fields.
- `src/MystTiq.Desktop/Models/WorldMapDtos.cs`: `PalMapPointDto`.
- `src/MystTiq.Desktop/Services/PalMapLayout.cs` (new): clustering, the base badge, and the tooltip and status text.
- `src/MystTiq.Desktop/Services/MapLabelLayout.cs`: optional `obstacles` for `ComputeLabelOffsets`.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`:
  - Pals state and `ShowPalsOnMap`;
  - `RebuildPalMapPoints`, run after bases;
  - `ZoomToPalGroupCommand`, which zooms to 10×;
  - labels avoid the Pal markers;
  - the Pals are cleared when the tab changes server.
- `src/MystTiq.Desktop/MainWindow.axaml`:
  - the Pals marker layer, between bases and players;
  - the checkbox, status line and "Pals on the map" card;
  - an updated map description.
- `scripts/Testing/MystTiq.LogicHarness`: links `PalMapLayout.cs`, and adds 2 "Pal positions:" scenarios.
- New scripts: `scripts/Test-v0.8.11.0-RouteSmoke.ps1`, `scripts/Test-v0.8.11.0-Logic.ps1`.
- Docs:
  - new: `docs/architecture/v0.8.11.0-pal-positions.md` and the release-notes trio;
  - updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
