# Apply v0.6.3.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.3.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.3.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change in this release — an existing v3 `mysttiq.json` (from v0.6.2.0) loads unchanged.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
