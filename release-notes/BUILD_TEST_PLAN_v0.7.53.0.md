# v0.7.53.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.53.0 logic suite (`scripts/Test-v0.7.53.0-Logic.ps1 -RunBuild`), including the frozen v0.7.52.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `MainWindow.axaml` Dashboard section only (padding/font-size values on the two stat-card grids and the World Pulse strip). No `MystTiq.HeadlessHost`/`MystTiq.Core`/ViewModel changes — pure XAML density pass.
5. This release cannot be visually verified in this environment — no way to render/screenshot the native Avalonia desktop app. The build/static gates below confirm the code compiles and the expected values are present; they do NOT confirm the Dashboard actually looks correct or appropriately dense on screen. Manual visual review is recommended before treating this as fully verified.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
