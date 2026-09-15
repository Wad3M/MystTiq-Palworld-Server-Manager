# v0.7.8.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.8.0 logic suite (`scripts/Test-v0.7.8.0-Logic.ps1 -RunBuild`), including the frozen v0.7.7.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Live route check: started a real sidecar and hit all four new endpoints plus `unban` via `/players/{id}/action`. All returned structured, graceful failures ("RCON is disabled in PalWorldSettings.ini") rather than 404/500s, confirming they're correctly wired end-to-end; the built RCON command text matched exactly (`BanList`, `Save`, `TeleportToMe <id>`, `TeleportToPlayer <id>`), and `unban` correctly resolved to `providerId: "rcon"`, confirming it routes through the RCON moderation provider and not the REST-only admin provider. A real Palworld dedicated server with RCON enabled would be needed to verify the actual in-game effect of each command, which is outside what this environment can run.
5. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0): the Unban button is enabled for a selected player regardless of online status; the Teleport buttons are enabled only for an online player; the Ban List card starts collapsed and loads on first expand; the Dashboard's SAVE button is present alongside SCAN/CLEANUP; switching tabs while on the Players page immediately refreshes the new tab's player directory instead of showing the previous tab's.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
