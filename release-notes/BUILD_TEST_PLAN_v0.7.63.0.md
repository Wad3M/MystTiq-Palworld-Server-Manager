# v0.7.63.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.63.0 logic suite (`scripts/Test-v0.7.63.0-Logic.ps1 -RunBuild`), including the frozen v0.7.62.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `Converters/SemanticStatusColorConverter.cs` (new), `Converters/ComponentStatusColorConverter.cs` (rewritten to delegate to it), `ViewModels/TabSession.cs` (`StatusDotColor` → `StatusDotColorKey`), `ViewModels/MainWindowViewModel.cs` (`HealthStateColor` → `HealthStateColorKey`, `RefreshTabAccentVisuals` also re-raises it), `Models/FleetDtos.cs` (`ServerProfileSummaryDto.StatusDotColor` → `StatusDotColorKey`), `MainWindow.axaml` (four bindings routed through the new converter), `Styles/DesignSystem.axaml` (new bare `Border.statuscard` style, new `Button:focus-visible` style), `App.axaml` (legacy hardcoded-color resource/style block removed). No `MystTiq.HeadlessHost`/`MystTiq.Core` changes — Desktop-only.
5. This release cannot be visually verified in this environment — no way to render/screenshot the native Avalonia desktop app. The build/static gates below confirm the code compiles and the fixed bindings/resources are wired correctly; they do NOT confirm the affected surfaces (tab-bar status dots, Dashboard health label, Fleet server list, Update Center's component table, the sidebar's mini status card) actually render the correct color with each accent theme and Light/Dark variant selected. Manual click-through across all four accent themes and both variants is recommended before treating this as fully verified.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
