# v0.6.6.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.6.0 logic suite (`scripts/Test-v0.6.6.0-Logic.ps1 -RunBuild`), including the frozen v0.6.5.0 checkpoint regression gate and the v0.5.1.5 runtime smoke suite.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. On a real isolated instance (never the production save/config), using a mock Palworld REST `/v1/api/players` endpoint standing in for a real dedicated server:
   - Poll `/status/poll` with no players online; confirm `GET /api/v1/players/registry` and `/players/registry/events` are both empty.
   - Report one online player, poll; confirm a registry record appears with `TotalSessions=1`, correct `SteamId`/`UserId`, and exactly one `"Join"` event.
   - Poll again with the same player still online; confirm no duplicate Join, `TotalSessions` unchanged, `TotalPlaytimeMinutes` accrued by roughly the real elapsed gap.
   - Remove the player and poll; confirm `CurrentlyOnline=false` and a `"Leave"` event.
   - Report the player online again and poll; confirm `TotalSessions` increments to 2 while `FirstSeenUtc` and cumulative playtime are preserved.
   - Confirm `GET /api/v1/world/players-guilds` still serializes cleanly with the new `abandonedBaseIds` field.
5. On the Desktop app: select a player on the Players page and confirm the new registry summary line renders without errors.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
