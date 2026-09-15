# v0.7.12.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.12.0 logic suite (`scripts/Test-v0.7.12.0-Logic.ps1 -RunBuild`), including the frozen v0.7.11.0 checkpoint regression gate, the v0.5.1.5 runtime smoke suite, the new `scripts/Test-v0.7.12.0-RouteSmoke.ps1`, and the new whitelist enforcement harness (`dotnet run` in `scripts/Testing/MystTiq.LogicHarness/`).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Both new tools were run standalone before being wired into the pipeline: the whitelist harness's 6 scenarios all passed against the real `HeadlessWhitelistService`; the route-smoke script's 8 checks all passed after fixing a response-body-reading bug found on its first run (PowerShell 7's `Invoke-RestMethod` throws on non-2xx and needs `$_.ErrorDetails.Message`, not the PowerShell 5.1-era `GetResponseStream()` API).
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
