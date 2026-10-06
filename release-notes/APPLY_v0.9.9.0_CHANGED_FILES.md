<!-- MystTiq v1.0.2.0: file reviewed for this release (2026-10-05). -->
# v0.9.9.0 Changed Files

- `Directory.Build.props`, `src/MystTiq.Desktop/app.manifest`: version 0.9.9.0.
- `src/MystTiq.Desktop/Services/LocalManagementBootstrapper.cs`, `SidecarState.cs` (new): the helper the desktop started
  is recorded and reused; a helper is stopped without its process tree.
- `src/MystTiq.Core/Services/CrashSignatureCatalog.cs`: the `exit-after-join` signature and `ExitAfterJoinDetector`.
  `src/MystTiq.HeadlessHost/HeadlessCrashAndSaveToolsService.cs`, `LocalManagementApiHost.cs`: PalDefender's session
  logs are read for it.
- `src/MystTiq.HeadlessHost/HeadlessComponentUpdateService.cs`: UE4SS matched by content against the newest releases'
  downloads (`Ue4ssReleaseFiles`), cached per asset.
- `src/MystTiq.Desktop/Services/LocalManagementBootstrapper.cs` also treats a port that accepts connections as
  occupied (`PortAcceptsConnections`) and drops the record of a helper that exited.
- `src/MystTiq.HeadlessHost/HeadlessAlertCenterService.cs`: UE4SS and MOD update alerts; `PeekUe4ssStatus` in
  `HeadlessComponentUpdateService.cs`.
- `src/MystTiq.HeadlessHost/HeadlessDiagnosticsService.cs`: the `align-public-port` fix.
- `src/MystTiq.Desktop/Services/DisplayCulture.cs` (new), `Localizer.cs`: number and date formats per language.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 18 new texts and 2 reworded labels in all 12 languages.
- `scripts/Test-v0.9.9.0-RouteSmoke.ps1`, `scripts/Test-v0.9.9.0-Distribution.ps1` (new),
  `scripts/Testing/MystTiq.LogicHarness` and `MystTiq.ArtworkHarness` (new scenarios), `scripts/Test-v0.9.9.0-Logic.ps1`
  (new gate); every text file re-stamped for v0.9.9.0.
- Docs: `docs/architecture/v0.9.9.0-helper-crash-formats.md`, the release-notes trio, `CHANGELOG.md`, `README.md`,
  `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md` and its index, `docs/release/README.md`,
  `docs/release/GITHUB_PRESENTATION.md`, `RELEASE_CHECKLIST.md`, `docs/release/pre-publication-review.md`, the issue
  templates.