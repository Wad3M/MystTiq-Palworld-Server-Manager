# v0.7.10.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.10.0 logic suite (`scripts/Test-v0.7.10.0-Logic.ps1 -RunBuild`), including the frozen v0.7.9.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Live check: started a real sidecar and confirmed `GET /api/v1/players/whitelist` returns the correct default, `PUT` persists a new enabled config with one entry, a subsequent `GET` reflects the saved state, and `GET /api/v1/status/poll` continues to respond correctly with the new enforcement call wired into its handler. Testing the actual auto-kick behavior requires real online players against a real Palworld server, outside what this environment can run.
5. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0): the Players page's new Whitelist card starts collapsed; enabling it without entries is possible (the UI does not block this, matching the disclosed "will kick everyone" upgrade note); Add/Remove buttons mutate the list locally before Save persists it.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
