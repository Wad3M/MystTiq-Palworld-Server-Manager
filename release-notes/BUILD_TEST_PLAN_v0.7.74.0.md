# v0.7.74.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.74.0 logic suite (`scripts/Test-v0.7.74.0-Logic.ps1 -RunBuild`), including the frozen v0.7.73.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/App.axaml.cs` (public `SafeExitAsync`/`ForceExitAsync`), `src/MystTiq.Desktop/Views/ConfirmMinimizeToTrayDialog.axaml`/`.axaml.cs`, `src/MystTiq.Desktop/MainWindow.axaml.cs`, `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` (startup exception isolation).
5. Verified: `dotnet build src/MystTiq.Desktop/MystTiq.Desktop.csproj` clean. Live-verified the app launches and connects against the real local production backend. No GUI click-testing of the new dialog buttons specifically was possible in this environment (no GUI-automation capability) — code-reviewed against the already-proven `MainWindow_Closing`/tray-menu shutdown paths, not new, unproven logic.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
