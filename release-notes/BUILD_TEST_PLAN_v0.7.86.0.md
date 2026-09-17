# v0.7.86.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.86.0 logic suite (`scripts/Test-v0.7.86.0-Logic.ps1 -RunBuild`), including the frozen v0.7.85.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` (`IsWizardStepMods`, step renumbering), `src/MystTiq.Desktop/MainWindow.axaml` (MODs step content).
5. Reuses `ScanWorkshopModsCommand`/`ImportSelectedWorkshopModCommand`, already live and proven on the MOD Library page.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
