# v0.7.39.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.39.0 logic suite (`scripts/Test-v0.7.39.0-Logic.ps1 -RunBuild`), including the frozen v0.7.38.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite (pay particular attention to "Validated MOD install..." and "MOD ZIP traversal payload is rejected..." — this release changes real mod-install server logic), and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Server-side logic change this release (`HeadlessModManagementService.InstallZipAsync`/new `DetectModType`) plus a small XAML/ViewModel cleanup (dropdown removed) — AVLN1001 unlikely given the scope, but watch for it regardless per the recurring pattern.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
