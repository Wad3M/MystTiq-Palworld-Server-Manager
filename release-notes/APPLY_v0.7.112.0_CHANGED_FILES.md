# v0.7.112.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.112.0
- `src/MystTiq.HeadlessHost/HeadlessKitService.cs` — `GiveEntriesAsync`, shared `DeliverAsync` extracted from
  `GiveAsync` (kit behaviour unchanged)
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs` — `POST /api/v1/players/{playerId}/give` (Admin),
  `GiveItemsRequest`
- `src/MystTiq.Desktop/Services/IMystTiqApiClient.cs`, `MystTiqApiClient.cs` — `GiveItemsAsync`
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — `GiveItemSelectedPlayerCommand` made real,
  `GiveItemsText`, `GiveItemsToSelectedPlayerAsync`
- `src/MystTiq.Desktop/MainWindow.axaml` — Give items or Pals box on Player Administration; context-menu entry
  enabled
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 1 new scenario
- `scripts/Test-v0.7.112.0-RouteSmoke.ps1` (new), `scripts/Test-v0.7.112.0-Logic.ps1` (new)
- `docs/architecture/v0.7.112.0-give-item.md`, `release-notes/v0.7.112.0.md`,
  `release-notes/APPLY_v0.7.112.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.112.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
