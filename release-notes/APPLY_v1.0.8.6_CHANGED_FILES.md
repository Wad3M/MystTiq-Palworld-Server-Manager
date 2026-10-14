<!-- MystTiq v1.0.8.7: file reviewed for this release (2026-10-12). -->
# v1.0.8.6 Changed Files

- `src/MystTiq.Desktop/ViewModels/ModsViewModel.cs` (new): the MOD pages, with the former
  `MainWindowViewModel.ModBrowser.cs` (removed).
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: the page created after the window's services (it is handed
  the Nexus client, the credential store and navigation); the Dashboard's stuck-start panel follows Safe Start through
  an event; `FormatTime` shared.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.ComponentUpdates.cs`: the Update Center reaches UE4SS through the
  page.
- `src/MystTiq.Desktop/MainWindow.axaml`, `MainWindow.axaml.cs`: the pages bind and call `Mods.*`.
- Tests: `scripts/Test-v1.0.8.6-Logic.ps1` (generated from the v1.0.8.5 gate) and the ArtworkHarness reading the page.
