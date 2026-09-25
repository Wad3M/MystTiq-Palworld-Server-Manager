# v0.8.0.0 Changed Files

Source of the artwork: `MystTiqPalworldServer_v0.7.110.1_Complete_Windows.zip` (user-supplied; branched from v0.7.110.0).

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.8.0.0
- `src/MystTiq.Desktop/Assets/Artwork/page-art-{home,server,world,backups,mods,tools,system}-{dark,light}.png` (new, 14)
- `src/MystTiq.Desktop/Assets/Icons/icon-*.png` (26, replaced)
- Removed: `src/MystTiq.Desktop/Assets/{dashboard-atmosphere-v3.png, page-art-home-dark.jpg, page-art-home-light.jpg,
  page-art-world-dark.jpg, page-art-world-light.jpg, server-card-art.png, world-card-art.png}`
- `src/MystTiq.Desktop/Services/ArtworkCatalog.cs` (new)
- `src/MystTiq.Desktop/MainWindow.axaml`, `ViewModels/MainWindowViewModel.cs`, `Styles/DesignSystem.axaml`,
  `Services/ThemeApplier.cs` — 3-way merged (base v0.7.110.0, theirs v0.7.110.1, ours v0.7.115.0), zero conflicts
- `src/MystTiq.Desktop/MainWindow.axaml` (after the merge) — header art fills the whole card (no card padding, art
  layers clipped to the curve, text layer inset), title block anchored to the bottom with a two-line subtitle cap;
  nav icons 50×50 in a 54px column
- `src/MystTiq.Desktop/Services/ThemeCatalog.cs`, `ArtworkCatalog.cs` — comments updated (retired image, 50px icons)
- `scripts/Testing/MystTiq.ArtworkHarness/` (new) — offline headless rendering harness
- `docs/artwork/` (new) — preview, prompts, asset list, the patch's validation notes; screenshots re-rendered from
  this merged build
- `scripts/Test-v0.8.0.0-Logic.ps1` (new)
- `docs/architecture/v0.8.0.0-artwork-refresh.md`, `release-notes/v0.8.0.0.md`,
  `release-notes/APPLY_v0.8.0.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.8.0.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
