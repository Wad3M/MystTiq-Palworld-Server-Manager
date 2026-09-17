# v0.7.85.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.85.0 logic suite (`scripts/Test-v0.7.85.0-Logic.ps1 -RunBuild`), including the frozen v0.7.84.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs` (`PrepareForWorldImportAsync`, wizard step skip for Import, `AnalyzeWorldArchiveAsync`/`ApplyWorldTransactionAsync` bug fix), `src/MystTiq.Desktop/MainWindow.axaml` (embedded Import step content).
5. **Live-verified end-to-end against a real server**: started a genuinely fresh install for the first time, confirmed real world generation via `GET /world/explorer`, built a real test archive, ran it through `/world/import/analyze` and `/world/import/apply` against that live server — completed successfully with a real safety backup created. This is the same mechanism the wizard's new "Prepare for Import" + embedded Analyze/Apply controls drive.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
