<!-- MystTiq v1.1.1.3: file reviewed for this release (2026-10-19). -->
# v1.1.1.2 Changed Files

- `src/MystTiq.HeadlessHost/HeadlessWorldClockService.cs`: `DecodedWorld` (the newest decoded copy, beside `Level.sav` or
  MystTiq's own, and where it came from); the world clock reads through it.
- `src/MystTiq.HeadlessHost/HeadlessPlayerGuildExplorerService.cs` (Players, Guilds, Bases, Map) and
  `HeadlessGameIdCatalogService.cs` (Give Item picker): read the newest copy; the explorer names it in its source line.
- `src/MystTiq.Desktop/Assets/Icons/icon-mod_sources.png` (new) and `icon-launcher.png` (replaced): the owner's art, its
  backdrop made transparent like the other navigation icons; `MainWindow.axaml`: MOD Sources uses its own icon.
- `docs/images/01-dashboard.png`, `03-players.png`, `06-backups.png`, `07-mods.png`: retaken from v1.1.1.2.
- Tests: `scripts/Test-v1.1.1.2-Logic.ps1`; a LogicHarness scenario; the ArtworkHarness counts 29 navigation icons.
