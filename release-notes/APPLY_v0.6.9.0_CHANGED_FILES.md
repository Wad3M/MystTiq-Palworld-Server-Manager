# Apply v0.6.9.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.9.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.9.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change in this release — an existing v3 `mysttiq.json` loads unchanged. `POST`/`PUT /api/v1/automation/rules` accept an additive `"IdleEmpty"` trigger kind with an additive `idleThresholdMinutes` field; existing trigger kinds and every other route are unchanged.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
