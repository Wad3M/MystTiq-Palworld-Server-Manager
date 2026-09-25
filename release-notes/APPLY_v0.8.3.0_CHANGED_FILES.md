# v0.8.3.0 Changed Files

- `Directory.Build.props` and `src/PalworldManager/app.manifest`: version bump to 0.8.3.0.
- `src/MystTiq.HeadlessHost/SaveGameIdReader.cs` (new): the streaming reader for the world save's item and Pal ids.
- `src/MystTiq.HeadlessHost/HeadlessGameIdCatalogService.cs` (new): the catalogue, which merges the three sources and
  caches the parse.
- `src/MystTiq.HeadlessHost/HeadlessKitService.cs`: remembers delivered manual gives (`give-history.json`).
- `src/MystTiq.HeadlessHost/ServerProfileHost.cs` and `LocalManagementApiHost.cs`: `GameIds` per profile, and
  `GET /players/give/catalog` (Operator).
- `src/MystTiq.Core/Services/GameIdSearch.cs` (new).
- `src/MystTiq.Core/Services/KitEntryText.cs`: `Append` and `TryParseAmount`.
- `src/MystTiq.Desktop/Models/KitDtos.cs`: catalogue DTOs.
- `src/MystTiq.Desktop/Services/IMystTiqApiClient.cs` and `MystTiqApiClient.cs`: `GetGameIdCatalogAsync`.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: the picker, which is cleared on a tab switch.
- `src/MystTiq.Desktop/MainWindow.axaml`: the "Find an item or Pal" card, plus a tip in the kit editor.
- `scripts/Testing/MystTiq.LogicHarness/Program.cs`: 5 "Give Item picker" scenarios.
- `scripts/Test-v0.8.3.0-RouteSmoke.ps1` and `scripts/Test-v0.8.3.0-Logic.ps1` (new).
- New docs:
  - `docs/architecture/v0.8.3.0-give-item-picker.md`
  - `release-notes/v0.8.3.0.md`
  - `release-notes/APPLY_v0.8.3.0_CHANGED_FILES.md`
  - `release-notes/BUILD_TEST_PLAN_v0.8.3.0.md`
- Updated docs: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
