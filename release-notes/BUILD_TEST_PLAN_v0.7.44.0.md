# v0.7.44.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.44.0 logic suite (`scripts/Test-v0.7.44.0-Logic.ps1 -RunBuild`), including the frozen v0.7.43.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, and the carried-forward v0.7.12.0/v0.7.15.0/v0.7.17.0 route/CLI smoke scripts and whitelist harness.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `IServerLifecycleService.FindAllInstancesAsync`/`TerminateUnmanagedInstanceAsync` (both platforms), `GET /server/instances`, `POST /server/instances/{processId}/terminate`, Desktop DTOs/client methods, and the new Server Doctor panel.
5. Watch for a real, unrelated PalServer process colliding with the isolated runtime-smoke suite's sidecar again (hit during both the v0.7.42.0 and v0.7.43.0 gates) — if it recurs, identify via `tasklist`/`Get-CimInstance Win32_Process` (`ParentProcessId` in particular) before taking any action, and never kill a real/live server process without explicit per-instance user authorization.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
