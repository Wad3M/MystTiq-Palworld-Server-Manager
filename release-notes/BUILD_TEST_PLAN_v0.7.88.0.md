# v0.7.88.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.88.0 logic suite (`scripts/Test-v0.7.88.0-Logic.ps1 -RunBuild`), including the frozen v0.7.87.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/Models/ServerStatusDto.cs` (`IsProcessLive`), `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` (wizard short-circuit scope, dashboard status fix, clone source list, install directory editability), `src/MystTiq.Desktop/MainWindow.axaml(.cs)` (Install Directory field, Game Port highlight).
5. Live-queried the real running sidecar's status endpoint to confirm the stale-PID scenario before and after the fix, independently confirming the stale PID no longer exists as an OS process.
6. No backend/`MystTiq.HeadlessHost` changes this release — all five bugs were client-side interpretation/filtering issues.
7. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
