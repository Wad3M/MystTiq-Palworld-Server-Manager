# Apply v0.7.8.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.8.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.8.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Server-side changes are limited to four new thin RCON-wrapper routes in `MystTiq.HeadlessHost` (`RconPlayerModerationProvider.cs`, `LocalManagementApiHost.cs`) — no `MystTiq.Core` model changes. The remainder of the changes are `MystTiq.Desktop`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
