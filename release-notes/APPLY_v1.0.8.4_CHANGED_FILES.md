<!-- MystTiq v1.1.5.0: file reviewed for this release (2026-10-25). -->
# v1.0.8.4 Changed Files

- `src/MystTiq.Desktop/ViewModels/ConfigurationViewModel.cs` (new): the Configuration page, with the former
  `MainWindowViewModel.Launcher.cs` (removed).
- `src/MystTiq.Desktop/ViewModels/WorkspaceViewModel.cs` (new): the Workspace page.
- `src/MystTiq.Desktop/ViewModels/SessionContext.cs`: `IsLocalProfile`.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: the pages created and fed; the Workspace page and the wizard
  follow the Configuration page through its events.
- `src/MystTiq.Desktop/MainWindow.axaml`, `MainWindow.axaml.cs`: the pages bind `Configuration.*` and `Workspace.*`.
- Tests: `scripts/Test-v1.0.8.4-Logic.ps1` (generated from the v1.0.8.3 gate) and the ArtworkHarness reading the pages.
