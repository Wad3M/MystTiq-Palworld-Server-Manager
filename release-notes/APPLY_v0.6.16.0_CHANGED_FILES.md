# Apply v0.6.16.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.16.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.16.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change in this release — an existing v3 `mysttiq.json` loads unchanged. No new server routes — player position data rides the existing player-status poll path. The map background preference is a new Desktop-local file (`%AppData%\MystTiq\map-preferences.json`) only; the server has no knowledge of it.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
