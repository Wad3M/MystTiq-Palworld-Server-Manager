<!-- MystTiq v1.1.4.0: file reviewed for this release (2026-10-20). -->
# v1.0.8.3 Changed Files

- `src/MystTiq.Desktop/ViewModels/MapViewModel.cs` (new): the Map page, with teleport points and the teleport-to-player
  commands.
- `src/MystTiq.Desktop/ViewModels/WorldViewModel.cs` (new): the Inspector and Pal editor, Bases, Guilds, character
  migration and world transactions.
- `src/MystTiq.Desktop/ViewModels/SessionContext.cs`: `IsConnected` and `BusyReason`.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: the pages created and fed; a tab switch calls
  `Map.ResetForNewServer()`.
- `src/MystTiq.Desktop/MainWindow.axaml`, `MainWindow.axaml.cs`: the pages bind `Map.*` and `World.*`.
- `scripts/Testing/Get-MystTiqCommandRoles.ps1`: follows code-behind calls through a page (`vm.World.X(...)`).
- Tests: `scripts/Test-v1.0.8.3-Logic.ps1` (generated from the v1.0.8.2 gate) and the ArtworkHarness reading the pages.
