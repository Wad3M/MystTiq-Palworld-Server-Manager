# v0.6.16.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.16.0 logic suite (`scripts/Test-v0.6.16.0-Logic.ps1 -RunBuild`), including the frozen v0.6.15.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Live World Map, against a real or realistic-substitute REST source (never fabricated as real data if synthetic):
   - Confirm `GetPlayersAsync` returns non-empty `location_x`/`location_y` for an online player, matching the raw REST response.
   - Confirm the Desktop World Map card plots a point per online player with a valid position, and excludes any player with a missing/unparseable coordinate rather than plotting a wrong default.
   - Confirm "Browse for Map Background" sets a background image and it persists across a Desktop restart; confirm "Clear Background" reverts to the plain grid; confirm a missing/invalid stored path falls back to the plain grid without error.
5. On the Desktop app: confirm the World Map card renders on the Players page, updates on the normal poll cadence, and does not appear as a new nav destination.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
