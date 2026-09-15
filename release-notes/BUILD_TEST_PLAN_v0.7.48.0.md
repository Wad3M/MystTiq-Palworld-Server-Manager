# v0.7.48.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.48.0 logic suite (`scripts/Test-v0.7.48.0-Logic.ps1 -RunBuild`), including the frozen v0.7.47.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `Services/ThemeColorMath.cs` (new file), `ThemeCatalog.cs`/`ThemeApplier.cs` extensions (derived color families, Inspect/Target gradient coverage), and extensive `MainWindow.axaml`/`Styles/DesignSystem.axaml` literal-to-DynamicResource conversions. Also `HeadlessComponentUpdateService.cs`'s UE4SS staleness fix (query full release list, not a hardcoded tag).
5. This release cannot be visually verified in this environment — no way to render/screenshot the native Avalonia desktop app. The build/static/runtime gates below confirm the code compiles and the resource-wiring contracts are present; they do NOT confirm the app actually looks correct in every theme/variant. Manual click-through of the theme picker (all 4 accent themes x Dark/Light) is recommended before treating this as fully verified.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
