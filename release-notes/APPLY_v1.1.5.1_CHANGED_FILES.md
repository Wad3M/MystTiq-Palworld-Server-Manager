<!-- MystTiq v1.1.5.1: file reviewed for this release (2026-10-26). -->
# v1.1.5.1 Changed Files

- `src/MystTiq.HeadlessHost/SafePath.cs` (new): `ChildOf` (a name's full path, refused when it leaves its folder) and
  `IsPlayerId` (32 hexadecimal characters).
- `src/MystTiq.HeadlessHost/HeadlessModManagementService.cs`: the MOD type must be PAK, UE4SS or NATIVE (`IsKnownType`,
  `NormalizeType`); Rollback, the description source and the description refuse any other; the snapshot and
  description-cache paths, the PAK files and the UE4SS folder are built with `SafePath.ChildOf`.
- `src/MystTiq.HeadlessHost/HeadlessCharacterMigrationService.cs`: the disposition step (Keep, Archive, Delete) refuses a
  source ID that is not a player ID; its save, companion and archive paths are built with `SafePath.ChildOf`.
- `scripts/Testing/MystTiq.LogicHarness/Program.cs`: the "Safe paths" scenario.
- `scripts/Test-v1.1.5.1-Logic.ps1` (new gate), `Directory.Build.props`, `src/MystTiq.Desktop/app.manifest`: version 1.1.5.1.
- Documentation: `release-notes/v1.1.5.1.md`, this file, `release-notes/BUILD_TEST_PLAN_v1.1.5.1.md`,
  `docs/architecture/v1.1.5.1-safe-file-paths.md`, `CHANGELOG.md`, `README.md`, `docs/index.html`,
  `docs/release/README.md`, `docs/roadmap/PRODUCT_ROADMAP.md`, `CHECKPOINT_NOTES.md`; every text file's review stamp.
