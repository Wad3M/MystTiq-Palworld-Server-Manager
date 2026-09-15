# v0.7.32.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.32.0 logic suite (`scripts/Test-v0.7.32.0-Logic.ps1 -RunBuild`), including the frozen v0.7.31.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Substantial XAML changes this release (nav pane, Server Setup, Backups, MOD Library, Server Doctor) — watch for the recurring AVLN1001 `--` comment parse error; four instances were hit and fixed during this release's own implementation, the highest count in a single version so far.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
