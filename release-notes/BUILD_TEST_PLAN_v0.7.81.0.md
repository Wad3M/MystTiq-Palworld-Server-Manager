# v0.7.81.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.81.0 logic suite (`scripts/Test-v0.7.81.0-Logic.ps1 -RunBuild`), including the frozen v0.7.80.0 checkpoint regression gate and the carried-forward smoke suites.
3. Run the new end-to-end wizard smoke test (`scripts/Test-v0.7.81.0-RouteSmoke.ps1`) against the real headless sidecar.
4. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
5. New surface this release: `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` (wizard step machine rewrite, World Source/Clone/Install steps, starter preset chain), `src/MystTiq.Desktop/MainWindow.axaml`/`.axaml.cs`, `src/MystTiq.Desktop/ViewModels/TabSession.cs`, `scripts/Test-v0.7.81.0-RouteSmoke.ps1`.
6. **Live-verified** against the real desktop app across multiple rounds: confirmed "Set Up New Server" skips the Local/Remote choice and lands on the local connect step, then World Source, then each branch. User-confirmed final sign-off.
7. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
