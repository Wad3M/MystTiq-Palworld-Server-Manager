<!-- MystTiq v1.1.6.0: file reviewed for this release (2026-10-26). -->
# v1.1.1.0 Changed Files

- MOD Sources: `src/MystTiq.Desktop/Models/NavigationPage.cs` (`ModSources` appended), `MainWindow.axaml` (the nav entry;
  the Steam Workshop card, the MOD browser and the Nexus Mods catalog moved to the new page; MOD Library's top row is the
  summary and the ZIP install), `ViewModels/MainWindowViewModel.cs` (`IsModSourcesPage`, the Workshop scan on arrival,
  Refresh, the Ribbon group), `ViewModels/ModsViewModel.cs` (Nexus links open MOD Sources), `Services/ArtworkCatalog.cs`.
- Player checks: `src/MystTiq.HeadlessHost/HeadlessPlayerWatchService.cs` (new), `LocalManagementApiHost.cs`
  (`RunPlayerChecksAsync`, shared by the poll and the service's timer; `POST /players/ids/link`), `ServerProfileHost.cs`.
- `HeadlessIdentityGuardService.cs`: the new-character notice; the start-option advice removed.
- `HeadlessPlayerIdService.cs`: the player list as a source for an account's character; `Link`.
- Desktop: the Player IDs link form (`PlayersViewModel.cs`, `MystTiqApiClient.cs`, the role table, `ButtonIntents.cs`),
  the Dashboard's same-PC note, the Launcher without Like double-click (`ConfigurationViewModel.cs`).
- `src/MystTiq.Desktop/Services/ThemeApplier.cs`: an unchanged theme is not applied again (faster tab switching).
- `Assets/i18n/*.json`: 15 new texts in 12 languages; the removed preset's texts dropped.
- Packaging: `scripts/Build-DockerImage.ps1` (new), `.github/workflows/release.yml` (the Linux download and the Docker image
  on every release), `scripts/New-MystTiqCheckpoint.ps1` (new).
- Tests: `scripts/Test-v1.1.1.0-Logic.ps1`; LogicHarness and ArtworkHarness scenarios; SSH keep-alive in the Linux checks.
