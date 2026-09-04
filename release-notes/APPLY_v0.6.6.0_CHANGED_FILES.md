# Apply v0.6.6.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.6.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.6.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change in this release — an existing v3 `mysttiq.json` loads unchanged. Existing routes (`/api/v1/status/poll`, `/api/v1/world/players-guilds`, `/api/v1/players/{playerId}/action`, `/api/v1/players/moderation/providers`) are unchanged in request shape; `/world/players-guilds`'s response gains an additive `abandonedBaseIds` field. New routes (`/api/v1/players/registry`, `/api/v1/players/registry/events`) are additive.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
