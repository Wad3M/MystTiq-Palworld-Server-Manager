# v0.7.15.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.15.0 logic suite (`scripts/Test-v0.7.15.0-Logic.ps1 -RunBuild`), including the frozen v0.7.14.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, the v0.7.12.0 route smoke script and whitelist harness (carried forward unchanged), and the new `scripts/Test-v0.7.15.0-RouteSmoke.ps1`.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Both new tools were run standalone before being wired into the pipeline: the new route-smoke script's 3 checks all passed against a real isolated sidecar (temp-ban routes reachable/graceful, no phantom entry persisted on a failed ban, `/history`'s new FPS fields correctly null with zero samples).
5. `MainWindow.axaml` must build clean under Avalonia's XAML compiler — watch for the recurring `--`-inside-an-XML-comment AVLN1001 parse error (one instance was introduced and fixed while authoring this release's comments).
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
