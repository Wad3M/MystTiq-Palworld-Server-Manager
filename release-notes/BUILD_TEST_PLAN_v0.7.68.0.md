# v0.7.68.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.68.0 logic suite (`scripts/Test-v0.7.68.0-Logic.ps1 -RunBuild`), including the frozen v0.7.67.0 checkpoint regression gate and the carried-forward v0.5.1.5/v0.7.12.0/v0.7.15.0/v0.7.17.0/v0.7.64.0 smoke suites and the expanded `scripts/Testing/MystTiq.LogicHarness` (now 19 scenarios).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Core/Services/WindowsServerLifecycleService.cs` and `src/MystTiq.Core/Services/LinuxServerLifecycleService.cs` (`StopAsync` tries RCON `Shutdown` first, additive to the existing fallback chain), `scripts/Testing/MystTiq.LogicHarness/Program.cs` (2 new scenarios with a real Source RCON protocol stub server).
5. **Not verified end-to-end against a real PalServer** — the RCON wire protocol itself is proven live via the new stub-server scenarios (exact command text transmitted and parsed correctly), but confirming a real, running Palworld dedicated server accepts and acts on this exact command needs a disposable live server with RCON enabled, which this session did not have without risking the production instance.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion, except the disclosed live-PalServer verification gap noted in step 5.
