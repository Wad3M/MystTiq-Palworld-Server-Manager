# v0.7.9.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.9.0 logic suite (`scripts/Test-v0.7.9.0-Logic.ps1 -RunBuild`), including the frozen v0.7.8.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Live check: started a real sidecar and confirmed `GET /api/v1/metrics` includes `serverFps`/`serverFrameTimeMs` in its response, correctly `null` (not a crash) both with no `PalWorldSettings.ini` present and with `RESTAPIEnabled=True` pointed at an unreachable port. A real Palworld dedicated server with the REST API actually serving `/v1/api/metrics` would be needed to verify the positive-path values, outside what this environment can run.
5. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0): the Monitoring page's Activity & Audit stat row shows 5 cards (CPU/RAM/Threads/Server FPS/Frame Time) without visual crowding; both new cards show "—" when the Palworld REST API is unavailable.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
