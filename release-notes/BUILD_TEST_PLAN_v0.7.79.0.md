# v0.7.79.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.79.0 logic suite (`scripts/Test-v0.7.79.0-Logic.ps1 -RunBuild`), including the frozen v0.7.78.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/Models/DiagnosticFindingDtos.cs` (state/recommendation computed properties), `src/MystTiq.Desktop/MainWindow.axaml` (Server Doctor finding row redesign), `src/MystTiq.Desktop/Styles/DesignSystem.axaml` (`TextBlock.badgeText`).
5. **Live-verified** across two rounds against the real desktop app: compacted color-coded rows, then the badge legibility (drop shadow) follow-up. User-confirmed final sign-off.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
