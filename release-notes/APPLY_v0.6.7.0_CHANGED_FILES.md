# Apply v0.6.7.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.7.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.7.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change in this release — an existing v3 `mysttiq.json` loads unchanged. Existing routes (`/api/v1/guilds/ownership/preview`, `/api/v1/guilds/ownership/apply`, `/api/v1/diagnostics/report`) are unchanged in request shape; `ownership/preview`/`apply` accept an additive `operationType` value, and the diagnostics report gains additive `"Identity"`-category findings.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
