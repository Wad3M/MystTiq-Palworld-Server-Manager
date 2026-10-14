<!-- MystTiq v1.0.9.2: file reviewed for this release (2026-10-12). -->
# v1.0.8.8 Changed Files

- `src/MystTiq.Desktop/ViewModels/DiagnosticsViewModel.cs` (new): the Update Center, Server Doctor, the Crash Analyzer and
  the Diagnostics Center, with the former `MainWindowViewModel.ComponentUpdates.cs` and
  `MainWindowViewModel.ConsoleCapture.cs` (removed).
- `src/MystTiq.Desktop/ViewModels/SessionContext.cs`: `ServerIsRunning`.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: the page created and handed the server-files and pip updates,
  the instance list and the UE4SS page; a notification's repair link calls `Diagnostics.FocusFinding`.
- `src/MystTiq.Desktop/MainWindow.axaml`, `MainWindow.axaml.cs`: the pages bind `Diagnostics.*`.
- Tests: `scripts/Test-v1.0.8.8-Logic.ps1` (generated from the v1.0.8.7 gate) and the ArtworkHarness reading the page.
