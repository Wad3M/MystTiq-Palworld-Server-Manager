# v0.6.5.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.5.0 logic suite (`scripts/Test-v0.6.5.0-Logic.ps1 -RunBuild`), including the frozen v0.6.4.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. On a real isolated instance (never the production save/config), using a scratch copy of a real `PalWorldSettings.ini`:
   - `GET /api/v1/players/moderation/providers` with REST and RCON both enabled: confirm both report `Healthy`.
   - Disable REST in the scratch ini (`RESTAPIEnabled=False`); confirm the providers route reports REST `Unavailable`, and that a `kick`/`ban` action still attempts RCON rather than failing immediately.
   - With no live PalServer process running (both ports closed): confirm `kick`/`ban` returns an honest `Supported=true/Success=false` result (HTTP 409) naming the real underlying network error, not an HTTP 500 with an empty body.
   - Set `RCONPort` equal to `RESTAPIPort` in the scratch ini; confirm `POST /api/v1/diagnostics/configuration-port-conflicts/recheck` flips to Fail with evidence naming both colliding settings; revert and confirm it returns to Pass.
   - Confirm `whisper`/`promote`/`give-item` are byte-for-byte unchanged (still HTTP 422 with the same message).
5. On the Desktop app: open the Players page and confirm the new provider-health status line renders without errors; open Doctor and confirm the new "Port Conflicts" finding appears in the unified list.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
