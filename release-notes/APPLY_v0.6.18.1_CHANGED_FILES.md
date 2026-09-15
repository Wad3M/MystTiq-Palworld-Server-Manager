# Apply v0.6.18.1 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.18.1 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.18.1_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. No new or removed routes. This is a pure bug-fix revision: three logic bugs found in a code review of v0.6.15.0–v0.6.18.0 are fixed in `HeadlessPalEditService.cs`, `HeadlessAntiCheatService.cs`, and `HeadlessDiscordBotService.cs`. Every existing route, config file shape, and default behavior for a correctly-functioning path is unchanged.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
