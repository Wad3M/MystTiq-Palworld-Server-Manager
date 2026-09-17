# v0.7.87.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.87.0 logic suite (`scripts/Test-v0.7.87.0-Logic.ps1 -RunBuild`), including the frozen v0.7.86.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` (navigation guard, continuation methods), `src/MystTiq.Desktop/MainWindow.axaml` (Advanced Settings dirty highlighting), `src/MystTiq.Desktop/MainWindow.axaml.cs` (dialog wiring), `src/MystTiq.Desktop/Views/ConfirmSaveDiscardDialog.axaml(.cs)` (new dialog).
5. No backend/`MystTiq.HeadlessHost` changes this release — entirely client-side Desktop behavior.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
