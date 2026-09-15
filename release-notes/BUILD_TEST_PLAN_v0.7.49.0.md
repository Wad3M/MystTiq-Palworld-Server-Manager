# v0.7.49.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.49.0 logic suite (`scripts/Test-v0.7.49.0-Logic.ps1 -RunBuild`), including the frozen v0.7.48.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `HeadlessModManagementService`'s `PreviewUe4ssInstallAsync`/`ApplyUe4ssInstallAsync`/`RollbackUe4ssInstallAsync` plus their engine-snapshot helpers; four new `LocalManagementApiHost` routes under `/ue4ss/install/*`; Desktop `MystTiqApiClient` methods and DTOs (`Ue4ssInstallPreviewDto`/`Ue4ssInstallResultDto`/`Ue4ssInstallStatusDto`); `MainWindowViewModel`'s `SelectedUe4ssRelease`/`Ue4ssInstallToken`/`Ue4ssInstallState`/`Ue4ssRollbackAvailable` plus three new commands; `MainWindow.axaml`'s UE4SS release list converted from `ItemsControl` to `ListBox` with selection.
5. **Exercised live before this release notes file was written**: a throwaway console harness ran the real preview/apply/rollback code path against real GitHub release data from both catalog sources (not mocked) — 4 scenarios, 21 assertions, all passed. See the architecture doc's "Live verification performed" section for exactly what was covered. Not yet run against a real, already-populated Palworld server install.
6. This release's UI surface cannot be visually verified in this environment — no way to render/screenshot the native Avalonia desktop app. The build/static/runtime gates below confirm the code compiles and the resource-wiring contracts are present; they do NOT confirm the ribbon buttons/status text actually look correct. Manual click-through of Preview Install / Confirm Install / Rollback is recommended before treating this as fully verified.
7. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
