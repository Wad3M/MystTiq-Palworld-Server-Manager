# v0.7.0.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.0.0 logic suite (`scripts/Test-v0.7.0.0-Logic.ps1 -RunBuild`), including the frozen v0.6.19.1 checkpoint regression gate and the v0.5.1.5 runtime smoke suite (expected fully unaffected — this release is Desktop-only).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0 — more consequential here than any prior milestone since this is an inherently visual feature): `ThemeCatalog`/`ThemeApplier`/`LocalThemePreferencesStore` exist with the expected shape; every gradient-stop/structural/accent/semantic resource key referenced by `DesignSystem.axaml` has a corresponding catalog entry for all 4 themes x 2 variants; the Default+Dark catalog values match today's hardcoded baseline exactly; the Settings page's Appearance card and its bindings are present; `IconGeometries.axaml`'s 8 icons are defined and referenced from `MainWindow.axaml`.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion. Real interactive/visual confirmation (opening Settings, switching accent themes and light/dark, confirming contrast and icon appearance in the running app) is the user's to do once built.
