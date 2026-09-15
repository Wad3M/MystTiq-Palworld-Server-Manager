# v0.7.20.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.20.0 logic suite (`scripts/Test-v0.7.20.0-Logic.ps1 -RunBuild`), including the frozen v0.7.19.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness (all unaffected, since this release is Desktop-only).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. `MainWindow.axaml` must build clean under Avalonia's XAML compiler — watch for the recurring `--`-inside-an-XML-comment AVLN1001 parse error (one instance was introduced and fixed while authoring this release's comments).
5. Both new PNG map assets (`Assets/Maps/palpagos.png`, `worldtree.png`) were verified to have a valid PNG signature before being wired in; the Desktop app was launched directly and produced no startup exceptions.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
