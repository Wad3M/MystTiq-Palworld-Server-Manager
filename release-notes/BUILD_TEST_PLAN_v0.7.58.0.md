# v0.7.58.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.58.0 logic suite (`scripts/Test-v0.7.58.0-Logic.ps1 -RunBuild`), including the frozen v0.7.57.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: a `ScrollViewer` around the MOD DESCRIPTION panel's description text, a new `OpenModDescriptionSource_Click` handler in `MainWindow.axaml.cs`, and a new `MainWindowViewModel.ShowNoModDescriptionMatchMessage` computed property. No `MystTiq.Core`/`MystTiq.HeadlessHost` changes, no route/DTO changes.
5. This release cannot be visually verified in this environment — no way to render/screenshot the native Avalonia desktop app. The build/static gates below confirm the code compiles and the new bindings/handler are correctly wired; they do NOT confirm the scroll behavior, button layout, or message timing look correct on screen. Manual review (select a MOD with a long Workshop description, confirm it scrolls; click Open on a fetched Source URL, confirm it opens in the default browser; select a fresh MOD and confirm the "no match" message does NOT show before clicking Fetch) is recommended before treating this as fully verified.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
