<!-- MystTiq v1.1.2.0: file reviewed for this release (2026-10-19). -->
# v1.0.8.9 Changed Files

- `src/MystTiq.Desktop/ViewModels/SecurityViewModel.cs` (new): the Security page (principals, user accounts, the browser
  view's address, the former `MainWindowViewModel.BrowserView.cs`, removed).
- `src/MystTiq.Desktop/ViewModels/FleetViewModel.cs` (new): the Fleet page (servers, Clone World and its restart, the
  fleet actions).
- `src/MystTiq.Desktop/ViewModels/SessionContext.cs`: `PageViewModel.Raise`, so a page's own bindings hear a change the
  window makes.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: the pages created; the Automation, Workspace and browser view
  raises go to their pages.
- `src/MystTiq.Desktop/MainWindow.axaml`: the pages bind `Security.*` and `Fleet.*`.
- Tests: `scripts/Test-v1.0.8.9-Logic.ps1` (generated from the v1.0.8.8 gate) and an ArtworkHarness check that the
  window's changes reach the pages.
