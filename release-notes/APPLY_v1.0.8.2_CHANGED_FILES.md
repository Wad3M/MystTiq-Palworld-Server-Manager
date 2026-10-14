<!-- MystTiq v1.0.8.7: file reviewed for this release (2026-10-12). -->
# v1.0.8.2 Changed Files

- `src/MystTiq.Desktop/ViewModels/PlayersViewModel.cs` (new): the Players page, from `MainWindowViewModel.cs` (the player
  directory, moderation, lists, the kits and Give Item sections) and the former `MainWindowViewModel.NameGuard.cs`,
  `.PalBox.cs` and `.Inventory.cs` (removed).
- `src/MystTiq.Desktop/ViewModels/SessionContext.cs`: `BuildProfile()`.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: the Players page created and fed (its refresh stays here, as
  part of the whole status refresh); `MainWindowViewModel.CommandRoles.cs`: the table names `Players.*`, and the busy
  state re-evaluates the pages' commands too.
- `src/MystTiq.Desktop/MainWindow.axaml`, `MainWindow.axaml.cs`: the Players page binds and calls `Players.*`.
- Tests: `scripts/Test-v1.0.8.2-Logic.ps1` (generated from the v1.0.8.1 gate; the carried contracts read the window's code
  wherever it now lives) and the ArtworkHarness reading `vm.Players`.
