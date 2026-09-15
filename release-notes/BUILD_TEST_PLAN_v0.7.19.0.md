# v0.7.19.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.19.0 logic suite (`scripts/Test-v0.7.19.0-Logic.ps1 -RunBuild`), including the frozen v0.7.18.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness (all unaffected, since this release is Desktop-only).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. `MainWindow.axaml` and `Styles/IconGeometries.axaml` must build clean under Avalonia's XAML compiler — watch for the recurring `--`-inside-an-XML-comment AVLN1001 parse error (two instances were introduced and fixed while authoring this release's comments).
5. All 25 new PNG icon files under `Assets/Icons/` were verified to have a valid PNG signature before being wired in; all 7 old `StreamGeometry` icon resources were confirmed to have zero remaining references before removal.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
