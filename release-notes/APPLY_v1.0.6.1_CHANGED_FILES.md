<!-- MystTiq v1.0.6.1: file reviewed for this release (2026-10-06). -->
# v1.0.6.1 Changed Files

- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.ServiceVersion.cs` (new): an older local service is noticed on connect;
  Update Service To This Version.
- `src/MystTiq.Desktop/Services/LocalManagementBootstrapper.cs`: `StopOutdatedLocalServicesAsync` (older MystTiq services on
  this PC, never the app's own; one running as another account is named, not stopped).
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`: the version check after connecting.
- `src/MystTiq.Desktop/MainWindow.axaml` and `.axaml.cs`: the Dashboard banner and its confirmation.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.Launcher.cs`: presets no longer add -log, -stdout,
  -FullStdOutLogOutput or -abslog; the warning.
- `src/MystTiq.HeadlessHost/HeadlessIdentityGuardService.cs`, `LocalManagementApiHost.cs`: the alert names the launch options
  (`IdentityGuard.LaunchAdvice`).
- `scripts/Validate-Release.ps1`: a script named for its own release (`Test-v1.0.6.0-…`) may name that release; the first fourth-number release after it reported those as stale versions.
- 10 texts in all 12 languages.
- Tests: `scripts/Test-v1.0.6.1-Logic.ps1` (generated from the v1.0.6.0 gate, every earlier check carried), a LogicHarness
  scenario and ArtworkHarness checks.
