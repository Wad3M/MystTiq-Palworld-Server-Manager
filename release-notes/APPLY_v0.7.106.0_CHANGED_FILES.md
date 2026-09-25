# v0.7.106.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.106.0
- `src/MystTiq.Desktop/Services/MapLabelLayout.cs` (new) — pure marker label collision/stagger pass
- `src/MystTiq.Desktop/Models/WorldMapDtos.cs` — `PlayerMapPointDto`/`BaseMapPointDto` gain `LabelOffsetY` and
  `LabelMargin`
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — `RebuildPlayerMapPoints` restructured to a single
  exit, new `ApplyMapLabelDeoverlap()` runs after every rebuild (every zoom/pan)
- `src/MystTiq.Desktop/MainWindow.axaml` — marker label `TextBlock`s bind the new `LabelMargin`
- `scripts/Testing/MystTiq.LogicHarness/MystTiq.LogicHarness.csproj` — compiles `MapLabelLayout.cs` directly
  (not `WorldMapDtos.cs`, which needs an Avalonia reference this harness does not have)
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 1 scenario
- `docs/architecture/v0.7.106.0-map-label-deoverlap.md`, `release-notes/v0.7.106.0.md`,
  `release-notes/APPLY_v0.7.106.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.106.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.106.0-Logic.ps1` (new) — no new route smoke this version (Desktop-only change)
