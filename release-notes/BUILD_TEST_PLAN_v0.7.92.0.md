# v0.7.92.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and `scripts/Test-v0.7.92.0-Logic.ps1 -RunBuild`, including the frozen v0.7.91.0 checkpoint regression gate and the carried-forward smoke suites.
3. New surface: `HeadlessPlayerGuildExplorerService` (base coordinates), `MainWindowViewModel`/`MainWindow.axaml` (map markers, click-to-select).
4. Live check after the final rebuild: `GET /api/v1/world/players-guilds` returns `baseLocations` for the real Default Server.
5. Create FullSource ZIP and verify SHA-256 manifest.

Any failure blocks promotion.
