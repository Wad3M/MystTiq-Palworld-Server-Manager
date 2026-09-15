# v0.7.42.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.42.0 logic suite (`scripts/Test-v0.7.42.0-Logic.ps1 -RunBuild`), including the frozen v0.7.41.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Wide-reaching but mechanical XAML/ViewModel/code-behind change this release (13 pages' header buttons relocated to new ribbon groups) — built clean on the first attempt with no AVLN1001 recurrence.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
