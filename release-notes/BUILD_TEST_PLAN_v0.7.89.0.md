# v0.7.89.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.89.0 logic suite (`scripts/Test-v0.7.89.0-Logic.ps1 -RunBuild`), including the frozen v0.7.88.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` (lifecycle loopback fix, dashboard stale-data fix, wizard install auto-refresh, MODs auto-scan), `src/MystTiq.Desktop/ViewModels/TabSession.cs` (tab label loopback fix), `src/MystTiq.Desktop/MainWindow.axaml` (progress relabel, install output display, MOD ZIP button).
5. Live-verified against a real second local server (registered and started during this session's own live feedback loop): confirmed via direct REST calls that the backend correctly starts/reports the server, isolating each fix to the client-side interpretation bugs described in the architecture doc.
6. No backend/`MystTiq.HeadlessHost` changes this release — all seven bugs were client-side.
7. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
