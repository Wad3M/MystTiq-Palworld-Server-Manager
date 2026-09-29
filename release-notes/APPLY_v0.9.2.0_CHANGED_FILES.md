<!-- MystTiq v0.9.8.0: file reviewed for this release (2026-09-29). -->
# v0.9.2.0 Changed Files

- `Directory.Build.props`, `src/MystTiq.Desktop/app.manifest`: version 0.9.2.0.
- `src/MystTiq.HeadlessHost/HeadlessGameNameService.cs`: names per display language (`Languages`, `NormalizeLanguage`,
  `Get(language)`, `CachePathFor`), one cache and state per language, English fallback; exit code 4 reported as a
  missing table.
- `src/MystTiq.HeadlessHost/Tools/extract_game_names.py`: `--lang ja` reads the base tables (the game's source language).
- `src/MystTiq.HeadlessHost/HeadlessGameIdCatalogService.cs`, `HeadlessPlayerGuildExplorerService.cs`,
  `LocalManagementApiHost.cs`: the language passed through; `?lang=` on `/players/give/catalog` and
  `/world/players-guilds`.
- `src/MystTiq.Desktop/Services/MystTiqApiClient.cs`: sends the display language on both routes.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: reads the Give Item picker again when the language changes.
- `src/MystTiq.Desktop/Services/MessageCatalog.cs`: sentence-by-sentence translation of composed texts.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 40 more messages (2,151 keys each).
- `scripts/Testing/MystTiq.LogicHarness/Program.cs`: the per-language name scenario.
- `scripts/Testing/MystTiq.ArtworkHarness/Program.cs`: composed-text checks.
- `scripts/Test-v0.9.2.0-Logic.ps1` (new gate); every text file re-stamped for v0.9.2.0.
- Docs: `docs/architecture/v0.9.2.0-game-names-in-your-language.md`, the release-notes trio, `CHANGELOG.md`,
  `README.md`, `docs/index.html`, `docs/roadmap/PRODUCT_ROADMAP.md`, `docs/i18n/UI_TEXT_INVENTORY.md` and `.csv`,
  `docs/release/README.md`, `RELEASE_CHECKLIST.md`, `docs/release/pre-publication-review.md`.
