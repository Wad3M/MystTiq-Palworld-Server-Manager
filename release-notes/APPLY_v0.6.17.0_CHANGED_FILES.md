# Apply v0.6.17.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.17.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.17.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change to `mysttiq.json` in this release. New per-profile file: `discord-bot.json` (created empty/disabled automatically on first run, alongside the existing `channels.json`/`templates.json`). New routes: `GET`/`PUT /notifications/discord-bot` (both additive, Admin-gated). Every existing route is unchanged.

`MystTiq.HeadlessHost` now references `Discord.Net.WebSocket` (new package dependency) for the Discord bot's gateway connection. The bot only connects when explicitly enabled with a bot token configured; it is fully inert (no new network activity) on an unconfigured or upgraded install.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
