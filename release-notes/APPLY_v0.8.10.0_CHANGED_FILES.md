# v0.8.10.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest`: version bump to 0.8.10.0.
- `src/MystTiq.Desktop/Assets/RibbonIcons/*.png`: all 30 files replaced by the user's originals, resized to 512x512.
- `src/MystTiq.Desktop/Services/RibbonIcons.cs`:
  - Backup and Doctor share the Create and Run Doctor images.
  - `ImageKeys` lists each image once.
  - New `ImageLabels`.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: checks for the 32 labels, the Backup/Doctor images, and the
  Dashboard and German vector/image mix.
- New: `scripts/Test-v0.8.10.0-Logic.ps1`.
- Docs:
  - New: `docs/architecture/v0.8.10.0-ribbon-icon-originals.md`, `release-notes/v0.8.10.0.md`,
    `release-notes/APPLY_v0.8.10.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.8.10.0.md`.
  - Rewritten: `docs/ribbon-icons/images/README.md`, with shipped and original hashes.
  - Updated: `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`.
