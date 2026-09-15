# v0.7.59.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.59.0 logic suite (`scripts/Test-v0.7.59.0-Logic.ps1 -RunBuild`), including the frozen v0.7.58.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `MystTiq.HeadlessHost.HeadlessModSafeStartService` (new file), `ServerProfileHost.ModSafeStart`, three new routes (`POST /mods/safe-start`, `GET /mods/safe-start/status`, `POST /mods/safe-start/cancel`). Desktop: `SafeStartStatusDto`/`SafeStartModResultDto`, three new `IMystTiqApiClient` methods, `MainWindowViewModel` polling/command wiring, a new MOD Library progress card. No changes to `MystTiq.Core`.
5. This release cannot be visually verified in this environment — no way to render/screenshot the native Avalonia desktop app, and no way to run the diagnostic against a real, healthy PalServer either (this machine's own production server currently can't launch cleanly — a separate, unresolved issue from v0.7.57.0's own investigation). The build/static gates below confirm the code compiles and the new routes/DTOs/bindings are correctly wired; they do NOT confirm the actual bisection loop behaves correctly against a real running server with real MODs. Manual review (run the diagnostic against a real modded server, ideally with one MOD deliberately broken, confirm it's correctly identified and disabled) is strongly recommended before treating this as fully verified — more so than most versions this session, given the live-server risk involved in using it.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
