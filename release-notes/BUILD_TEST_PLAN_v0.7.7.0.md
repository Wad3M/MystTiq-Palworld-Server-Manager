# v0.7.7.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.7.0 logic suite (`scripts/Test-v0.7.7.0-Logic.ps1 -RunBuild`), including the frozen v0.7.6.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite — this release genuinely exercises the changed backup/restore and lifecycle-restart code paths, not just confirms them unaffected.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Live concurrency check (`scripts/Test-v0.7.7.0-ConcurrencyCheck.ps1`): starts a real sidecar, creates a real backup, then fires `POST /server/start` and `POST /backups/{file}/restore` at nearly the same instant via an in-process `HttpClient` + `Task.WhenAll` (not separate job processes, to keep the race genuinely tight) and asserts exactly one is rejected with a 409 lock-conflict response. Run twice to confirm the assertion holds regardless of which request wins the race — both runs correctly rejected the backup restore with "Another world-mutating operation is in progress", proving the new `world-mutation` lock actually enforces mutual exclusion, not just that the code compiles.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
