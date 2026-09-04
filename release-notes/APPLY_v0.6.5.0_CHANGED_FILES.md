# Apply v0.6.5.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.5.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.5.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change in this release — an existing v3 `mysttiq.json` loads unchanged. The existing `/api/v1/players/{playerId}/action`, `/api/v1/doctor`, `/api/v1/server/environment`, and `/api/v1/diagnostics/report` routes are unchanged in request/response shape (the action response gains an additive `providerId` field; the diagnostics report gains an additive `"Configuration"`-category finding). New routes (`/api/v1/players/moderation/providers`) are additive.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
