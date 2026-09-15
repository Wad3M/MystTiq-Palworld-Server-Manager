# v0.7.14.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.14.0 logic suite (`scripts/Test-v0.7.14.0-Logic.ps1 -RunBuild`), including the frozen v0.7.13.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, the v0.7.12.0 route smoke script, and the whitelist enforcement harness — all carried forward unchanged, since this release is Desktop-only (styles/markup).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. `MainWindow.axaml` and `DesignSystem.axaml` must build clean under Avalonia's XAML compiler — this release added several new XAML comments; watch for the recurring `--`-inside-an-XML-comment AVLN1001 parse error (4 instances were introduced and fixed while authoring this release's comments).
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
