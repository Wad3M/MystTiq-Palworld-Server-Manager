<!-- MystTiq v1.1.0.3: file reviewed for this release (2026-10-13). -->
# v1.0.8.5 Changed Files

- `src/MystTiq.Desktop/ViewModels/BackupsViewModel.cs` (new): the Backups and Save Tools pages, with the former
  `MainWindowViewModel.SaveAccess.cs` (removed).
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: the page created; Backup All stays with the fleet actions.
- `src/MystTiq.Desktop/MainWindow.axaml`, `MainWindow.axaml.cs`: the pages bind `Backups.*`.
- Tests: `scripts/Test-v1.0.8.5-Logic.ps1` (generated from the v1.0.8.4 gate) and the ArtworkHarness reading the page.
