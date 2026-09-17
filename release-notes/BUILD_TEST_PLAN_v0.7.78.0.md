# v0.7.78.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.78.0 logic suite (`scripts/Test-v0.7.78.0-Logic.ps1 -RunBuild`), including the frozen v0.7.77.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.HeadlessHost/HeadlessModManagementService.cs` (`HeadlessModItem` gains `UpdateAvailable`/`UpdateHint`/`InstalledVersion`, computed in `GetInventoryAsync`; `Ue4ssRuntimeVerification` marker and `ResolveUe4ss`'s revised `HealthState`), `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` (auto-scan trigger, MOD Maintenance ribbon group, `ModInstallPackage` removed, `IsModHealthGood`/`IsModHealthDegraded`), `src/MystTiq.Desktop/MainWindow.axaml`/`.axaml.cs` (MOD Library layout, right-click menu, drag-and-drop, MOD Dashboard redesign), `src/MystTiq.Desktop/Models/ModManagementDtos.cs`, `src/MystTiq.Desktop/Styles/DesignSystem.axaml` (`Border.statuscard` padding).
5. **Live-verified across 5 build → relaunch → check rounds** against the real desktop app: MOD Library auto-load, layout restructure, ribbon/right-click regroup, drag-and-drop ZIP install with the simplified card, per-mod version/update badges, MOD Dashboard's hero banner/recolored cards/compact list/renamed card, UE4SS card padding. User-confirmed final sign-off.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
