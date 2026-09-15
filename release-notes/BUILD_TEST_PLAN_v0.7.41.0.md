# v0.7.41.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.41.0 logic suite (`scripts/Test-v0.7.41.0-Logic.ps1 -RunBuild`), including the frozen v0.7.40.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite (pay particular attention to the MOD install/traversal checks — this release adds a new server-side MOD route), and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New server-side logic this release (`HeadlessModManagementService.CheckModUpdateAsync`/`FindMatchingWorkshopItem`/`GetInstalledModLastWriteUtc`, a new `check-update` route) plus client wiring (new DTO, API client method, ViewModel commands, MOD DETAILS panel UI) — one AVLN1001 `--` comment error was hit and fixed during this release's own implementation.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
