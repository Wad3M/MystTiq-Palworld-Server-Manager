# v0.7.2.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.2.0 logic suite (`scripts/Test-v0.7.2.0-Logic.ps1 -RunBuild`), including the frozen v0.7.1.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. **Live endpoint verification (this release's real server-side change)**: start the built `mysttiq-server.exe` sidecar against a scratch config, occupy a real TCP port with a plain listener, call `GET /api/v1/diagnostics/port-check?port=<occupied>&protocol=TCP` and confirm `inUse=true` with the real process name/PID, then call it again with an unused port and confirm `inUse=false`. Already performed once during development; re-run if anything in `PortAvailabilityService`/the endpoint changes.
5. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0): typing an in-use port into the Setup card's Game Port/REST Port fields shows the amber warning; typing a free port clears it.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
