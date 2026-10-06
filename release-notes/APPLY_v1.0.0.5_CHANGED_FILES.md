<!-- MystTiq v1.0.6.0: file reviewed for this release (2026-10-06). -->
# v1.0.0.5 Changed Files

- `src/MystTiq.Desktop/Services/WorldExplorerCards.cs` (new): base cards (decoded location, base-worker Pals, owner) and
  guild rosters (by name; leader, then online, then by name).
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.WorldCards.cs` (new): the cards from the explorer read, the page
  summaries, Show on map (Map page, base markers, `ZoomToBase`), Open guild and Open base.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: the explorer read builds the cards and gives the map its bases.
- `src/MystTiq.Desktop/Models/PlayerGuildExplorerDtos.cs`: card fields on `BaseExplorerItemDto` and `GuildExplorerItemDto`,
  `GuildMemberRow`, worker display on `PalLocationDto`.
- `src/MystTiq.Desktop/MainWindow.axaml`: the Bases and Guilds pages rebuilt; the shared count cards and Evidence Model
  block removed; the warnings card shown only when there are warnings.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 19 new texts in all 12 languages.
- Tests: `scripts/Test-v1.0.0.5-Logic.ps1` (generated from the v1.0.0.4 gate, every earlier check carried) and
  ArtworkHarness checks (card and roster rules, the two pages rendered, German).
