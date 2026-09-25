# v0.7.93.0 Changed Files

- `Directory.Build.props`, `src/PalworldManager/app.manifest` — version bump to 0.7.93.0
- `src/MystTiq.Core/Services/NexusModsLinks.cs` (new) — nxm/mod-id parsing, host allowlist, archive sniffing
- `src/MystTiq.Desktop/Services/NexusModsClient.cs` (new) — Nexus API v1 client and capped download
- `src/MystTiq.Desktop/Models/NexusModsDtos.cs` (new)
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` — Nexus state, commands, download-and-install pipeline
- `src/MystTiq.Desktop/MainWindow.axaml` — Nexus Mods Catalog expander on the MOD Library page
- `scripts/Testing/MystTiq.LogicHarness/Program.cs` — 5 NexusModsLinks scenarios
- `docs/architecture/v0.7.93.0-nexus-mods-catalog.md`, `release-notes/v0.7.93.0.md`,
  `release-notes/APPLY_v0.7.93.0_CHANGED_FILES.md`, `release-notes/BUILD_TEST_PLAN_v0.7.93.0.md` (new)
- `CHANGELOG.md`, `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`
- `scripts/Test-v0.7.93.0-Logic.ps1` (new)
