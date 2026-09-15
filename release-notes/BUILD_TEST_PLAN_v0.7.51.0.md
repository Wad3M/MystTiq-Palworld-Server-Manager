# v0.7.51.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.51.0 logic suite (`scripts/Test-v0.7.51.0-Logic.ps1 -RunBuild`), including the frozen v0.7.50.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `Models/ConnectionProfile.cs` (`AccentColorKey`), `Services/ThemeCatalog.cs` (`TabIdentityColorNames`), `ViewModels/TabSession.cs` (`AccentBrush`/`RefreshAccentVisual`), `ViewModels/MainWindowViewModel.cs` (`ResolveAccentColorKey`/`RefreshTabAccentVisuals`, `BuildProfileFromTab` no longer static), `MainWindow.axaml` (tab template column layout). No `MystTiq.HeadlessHost`/`MystTiq.Core` changes — Desktop-only.
5. This release cannot be visually verified in this environment — no way to render/screenshot the native Avalonia desktop app. The build/static gates below confirm the code compiles and the resource-wiring contracts are present; they do NOT confirm the tab strip actually looks correct with multiple tabs open. Manual click-through with 3+ tabs open is recommended before treating this as fully verified.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
