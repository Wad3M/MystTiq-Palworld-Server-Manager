# v0.7.69.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.69.0 logic suite (`scripts/Test-v0.7.69.0-Logic.ps1 -RunBuild`), including the frozen v0.7.68.0 checkpoint regression gate and the carried-forward v0.5.1.5/v0.7.12.0/v0.7.15.0/v0.7.17.0/v0.7.64.0 smoke suites and the expanded `scripts/Testing/MystTiq.LogicHarness` (now 20 scenarios).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Core/Services/WindowsServerLifecycleService.cs` (`StopAsync`'s "nothing to stop" branch now writes a fresh Stopped/StopRequested state, matching Linux), `scripts/Testing/MystTiq.LogicHarness/Program.cs` (1 new scenario using a real `WindowsServerLifecycleService` instance and a new `EmptySessionInspector` test double).
5. Verified with a real class instance in the logic harness (not a stand-in); no live-hardware crash reproduction needed since the fix is entirely in the state-store transition, which the harness exercises directly through the real `StopAsync` code path.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
