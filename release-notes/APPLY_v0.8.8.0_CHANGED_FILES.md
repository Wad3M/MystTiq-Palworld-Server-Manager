# v0.8.8.0 Changed Files

The icons come from three user-supplied packages: `palworld_ribbon_icons_batch_1.zip`, `_batch_2.zip` and `_batch_3.zip`.

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.8.0.
- `src/MystTiq.Desktop/Assets/RibbonIcons/*.png` (new): the 30 supplied images, unchanged.
- `src/MystTiq.Desktop/Services/RibbonIcons.cs`: `ImageKeyFor`, `Image`, `ImageKeys` and `ImageDecodeWidth`, plus the
  30-entry map from label to image.
- `src/MystTiq.Desktop/Models/RibbonActionViewModel.cs`: `ImageIconKey`, `ImageIcon`, `HasImageIcon` and `ShowGlyph`.
  The image icon now takes precedence over the vector icon.
- `src/MystTiq.Desktop/MainWindow.axaml`: the Ribbon icon tile shows the image, and the glyph now uses `ShowGlyph`.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: checks for the image icons across 13 pages.
- `docs/ribbon-icons/images/README.md` (new): the package record, with a SHA-256 hash per file.
- `scripts/Test-v0.8.8.0-Logic.ps1` (new).
- New docs:
  - `docs/architecture/v0.8.8.0-ribbon-image-icons.md`;
  - `release-notes/v0.8.8.0.md`;
  - `release-notes/APPLY_v0.8.8.0_CHANGED_FILES.md`;
  - `release-notes/BUILD_TEST_PLAN_v0.8.8.0.md`.
- Updated docs: `CHANGELOG.md`, `README.md`, `docs/index.html` and `docs/roadmap/PRODUCT_ROADMAP.md`.
