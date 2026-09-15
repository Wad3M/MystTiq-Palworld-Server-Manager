# v0.7.55.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.55.0 logic suite (`scripts/Test-v0.7.55.0-Logic.ps1 -RunBuild`), including the frozen v0.7.54.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `HeadlessModManagementService.GetModDescriptionAsync`/`SetModDescriptionSourceAsync` (new outbound HTTP calls to Steam's and GitHub's public REST APIs, on-demand only), two new routes under `/mods/{type}/{package}/description`, `ModDescriptionResultDto`/`ModDescriptionSourceRequestDto` on the client, a new DESCRIPTION section in MOD Library's MOD DETAILS panel. No `MystTiq.Core` changes.
5. This release cannot be visually verified in this environment — no way to render/screenshot the native Avalonia desktop app, and no way to exercise a live Steam/GitHub fetch against a real MOD from this environment either. The build/static gates below confirm the code compiles, the new routes are registered, and the cache/fetch logic's static shape is correct; they do NOT confirm an actual Steam Workshop or GitHub fetch succeeds against live data. Manual review (select a Workshop-sourced MOD, click Fetch, confirm a real description appears; set a GitHub Source URL on a non-Workshop MOD, confirm the same) is recommended before treating this as fully verified.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
