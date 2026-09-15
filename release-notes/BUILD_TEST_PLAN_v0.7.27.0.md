# v0.7.27.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.27.0 logic suite (`scripts/Test-v0.7.27.0-Logic.ps1 -RunBuild`), including the frozen v0.7.26.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness (all unaffected, since this release is `MystTiq.Desktop`-only).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Changed XAML this release (Workspace, Backups, MOD Dashboard/Library, Server Doctor pages) — watch for the recurring AVLN1001 `--` comment parse error, though none was hit this time.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
