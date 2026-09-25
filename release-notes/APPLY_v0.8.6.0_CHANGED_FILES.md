# v0.8.6.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.6.0.
- `src/MystTiq.Desktop/Models/RibbonActionViewModel.cs`: adds `DisplayLabel` and `DisplayTitle`. `Label` and `Title`
  stay the English identity.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`:
  - adds `RibbonKey` and `LocalizeRibbon`;
  - the Ribbon is rebuilt on a language change;
  - `EstimateRibbonGroupWidth` now works from the displayed text.
- `src/MystTiq.Desktop/MainWindow.axaml`: the Ribbon buttons and group titles show the display text.
- `src/MystTiq.Desktop/MainWindow.axaml.cs`: the overflow menu shows the display text.
- `src/MystTiq.Desktop/Assets/i18n/en.json`, `de.json`, `es.json`: 76 Ribbon keys; the language note is updated.
  Each file now has 165 keys.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: Ribbon checks in every language, on every page, at two sizes.
- `scripts/Test-v0.8.6.0-Logic.ps1` (new).
- New docs:
  - `docs/architecture/v0.8.6.0-ribbon-language.md`
  - the release-notes trio (`release-notes/v0.8.6.0.md`, `release-notes/APPLY_v0.8.6.0_CHANGED_FILES.md`,
    `release-notes/BUILD_TEST_PLAN_v0.8.6.0.md`)
- Updated docs: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
