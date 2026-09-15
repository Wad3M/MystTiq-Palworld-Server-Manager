# v0.7.56.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.56.0 logic suite (`scripts/Test-v0.7.56.0-Logic.ps1 -RunBuild`), including the frozen v0.7.55.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: four new `ThemeApplier` helper methods (`BuildGraphFill`, `BuildContextGradient`, `BuildGlassSheen`, `BuildNavGlass`) computing ~38 new/overridden resource keys across all 4 accent themes × 2 variants; `Border.dataRow`'s three states in `DesignSystem.axaml` routed through the new `DataRow*` resources instead of hardcoded hex. No `MystTiq.Core`/`MystTiq.HeadlessHost` changes — Desktop-only, purely visual.
5. This release cannot be visually verified in this environment — no way to render/screenshot the native Avalonia desktop app. The build/static gates below confirm the code compiles and the new resource keys are correctly wired; they do NOT confirm the actual rendered glass/gradient appearance in any theme or variant. Manual review (category tabs, ribbon buttons, nav sidebar hover/selected, Dashboard atmosphere overlay, Backups cards, MOD Library context card, any GraphFill chart — across all 4 accent themes and both Dark/Light) is recommended before treating this as fully verified.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
