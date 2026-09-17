# v0.7.80.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.80.0 logic suite (`scripts/Test-v0.7.80.0-Logic.ps1 -RunBuild`), including the frozen v0.7.79.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/MainWindow.axaml` (Server Setup status pill centering + glass gradients), `src/MystTiq.Desktop/Styles/DesignSystem.axaml` (`WarningGlassGradient`).
5. **Live-verified** against the real desktop app: status pills center their text and show the glassy gradient look. User-confirmed final sign-off.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
