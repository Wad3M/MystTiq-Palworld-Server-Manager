# v0.8.7.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.7.0.
- `src/MystTiq.Desktop/Services/Localizer.cs`: new `TrFormatExtension` (a translated template plus a bound value)
  and its `Fill` helper.
- `src/MystTiq.Desktop/MainWindow.axaml`: Dashboard labels, buttons and tooltips use `{services:Tr}`, and its six
  templates use `{services:TrFormat}`. The CPU/MEMORY card now uses `Auto,Auto,*` columns.
- `src/MystTiq.Desktop/Assets/i18n/en.json`, `de.json`, `es.json`: 43 Dashboard keys added, 208 keys each.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: Dashboard language checks, including the minimum-size check
  that no label is split or cut off.
- `scripts/Test-v0.8.7.0-Logic.ps1` (new).
- New docs: `docs/architecture/v0.8.7.0-dashboard-language.md`, `release-notes/v0.8.7.0.md`,
  `release-notes/APPLY_v0.8.7.0_CHANGED_FILES.md` and `release-notes/BUILD_TEST_PLAN_v0.8.7.0.md`.
- Updated docs: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
