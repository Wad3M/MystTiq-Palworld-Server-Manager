# v0.8.1.0 Changed Files

Source of the icons: `MystTiq_Palworld_Ribbon_Icons.zip` (user-supplied).

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.8.1.0
- `src/MystTiq.Desktop/Services/RibbonIcons.cs` (new) — the ten path strings, label/glyph mapping, cached geometries
- `src/MystTiq.Desktop/Models/RibbonActionViewModel.cs` — `VectorIconKey`, `VectorIcon`, `HasVectorIcon`
- `src/MystTiq.Desktop/MainWindow.axaml` — Ribbon icon tile shows the vector icon or the glyph
- `src/MystTiq.Desktop/Styles/DesignSystem.axaml` — `Path.ribbonIcon` and its five theme colour classes
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs` — 42 Ribbon icon checks and dark/light Ribbon renders
- `docs/ribbon-icons/` (new) — the supplied package: SVGs, icons.json, PalRibbonIcons.axaml, preview, README
- `scripts/Test-v0.8.1.0-Logic.ps1` (new)
- `docs/architecture/v0.8.1.0-ribbon-icons.md`, `release-notes/v0.8.1.0.md`,
  `release-notes/APPLY_v0.8.1.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.8.1.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
