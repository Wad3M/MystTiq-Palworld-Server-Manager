# v0.7.84.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.84.0 logic suite (`scripts/Test-v0.7.84.0-Logic.ps1 -RunBuild`), including the frozen v0.7.83.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` (`CloneWorldNeedsRestart`/`RestartAfterCloneCommand`/`RestartAfterCloneAsync`), `src/MystTiq.Desktop/MainWindow.axaml` (Restart button).
5. **Live-verified**: the Clone World workflow card itself was already live-verified in an earlier session; the new restart button reuses `RestartOwnedSidecarAsync`, already proven end-to-end in v0.7.83.0's own live verification against a real second-server clone.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
