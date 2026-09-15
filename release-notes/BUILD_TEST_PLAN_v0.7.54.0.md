# v0.7.54.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.54.0 logic suite (`scripts/Test-v0.7.54.0-Logic.ps1 -RunBuild`), including the frozen v0.7.53.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: four new JPEG assets under `MystTiq.Desktop/Assets/` (`page-art-home-dark.jpg`, `page-art-home-light.jpg`, `page-art-world-dark.jpg`, `page-art-world-light.jpg`), four new computed bools on `ViewModels/MainWindowViewModel.cs`, four new `Image` elements in `MainWindow.axaml`. No `MystTiq.HeadlessHost`/`MystTiq.Core` changes.
5. This release cannot be visually verified in this environment — no way to render/screenshot the native Avalonia desktop app. The build/static gates below confirm the code compiles and the new assets are correctly picked up by the Avalonia resource pipeline; they do NOT confirm the artwork actually looks correct composited behind live page-header text. Manual review (Home and World pages, both Dark and Light) is recommended before treating this as fully verified.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
