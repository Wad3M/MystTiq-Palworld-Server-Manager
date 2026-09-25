# v0.8.13.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.13.0.

**Names**
- `src/MystTiq.HeadlessHost/Tools/extract_game_names.py` (new) and `MystTiq.HeadlessHost.csproj`: the extractor is
  shipped beside the executable.
- `src/MystTiq.HeadlessHost/HeadlessGameNameService.cs` (new): `GameNameCatalog`, `GameNameStatus`,
  `HeadlessGameNameService`.
- `src/MystTiq.HeadlessHost/HeadlessGameIdCatalogService.cs`:
  - `Name` and `InGameFiles` on entries;
  - `WithNames`;
  - `NamesAvailable` and `NamesDetail`.
- `src/MystTiq.HeadlessHost/HeadlessPlayerGuildExplorerService.cs`: `SpeciesName` on Pal locations.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: one name service per profile, shared by both.
- `src/MystTiq.Core/Services/GameIdSearch.cs`: `Matches(id, name, query)`.
- `src/MystTiq.Desktop/Models/KitDtos.cs`, `Models/PlayerGuildExplorerDtos.cs`, `Services/PalMapLayout.cs`,
  `ViewModels/MainWindowViewModel.cs`, `MainWindow.axaml`: names in the picker (display, search, status) and on the map.

**Icons**
- `src/MystTiq.Desktop/Assets/RibbonIcons/`: 10 new PNGs.
- `src/MystTiq.Desktop/Services/RibbonIcons.cs`: 18 labels mapped.
- `docs/ribbon-icons/images/README.md`: batch 4 rows.

**Tests**
- `scripts/Testing/MystTiq.LogicHarness/Program.cs`: 2 "Game names:" scenarios.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`:
  - 40 images and 50 labels;
  - the Dashboard is all images;
  - every page has no button without an image.
- New:
  - `scripts/Testing/make_test_pak.py`
  - `scripts/Test-v0.8.13.0-RouteSmoke.ps1`
  - `scripts/Test-v0.8.13.0-Logic.ps1`
  - `scripts/Test-v0.8.13.0-LinuxIsolated.ps1` and `scripts/Test-v0.8.13.0-LinuxIsolated.sh`

**Docs**
- New: `docs/architecture/v0.8.13.0-game-names.md` and the release-notes trio.
- Updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
