<!-- MystTiq v1.0.9.1: file reviewed for this release (2026-10-12). -->
# v1.0.8.7 Changed Files

- `src/MystTiq.Desktop/ViewModels/AlertsViewModel.cs` (new): the Notifications, Alert Center, Automation and Activity pages,
  delivery and the Discord bot, with the former `MainWindowViewModel.Delivery.cs` (removed).
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: the page created; a notification's repair link stays (it
  focuses Server Doctor); the Players page reaches the activity refresh through the page.
- `src/MystTiq.Desktop/MainWindow.axaml`, `MainWindow.axaml.cs`: the pages bind `Alerts.*`.
- Tests: `scripts/Test-v1.0.8.7-Logic.ps1` (generated from the v1.0.8.6 gate) and the ArtworkHarness reading the page.
