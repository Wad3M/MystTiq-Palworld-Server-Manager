# v0.7.109.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.109.0
- `src/MystTiq.Desktop/Services/MapLabelLayout.cs` — new `ComputeMarkerOffsets`, `CoincidentRadius`,
  `SpreadRadius`
- `src/MystTiq.Desktop/Models/WorldMapDtos.cs` — `PlayerMapPointDto`/`BaseMapPointDto` gain
  `MarkerOffsetX`/`MarkerOffsetY`, folded into the existing `MarkerMargin`
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — `ApplyMapLabelDeoverlap` runs marker spread first,
  then computes label offsets against the resulting positions
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 1 scenario
- `docs/architecture/v0.7.109.0-map-marker-spread.md`, `release-notes/v0.7.109.0.md`,
  `release-notes/APPLY_v0.7.109.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.109.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.109.0-Logic.ps1` (new) — no new route smoke this version (Desktop-only change, no XAML
  binding even changed)
