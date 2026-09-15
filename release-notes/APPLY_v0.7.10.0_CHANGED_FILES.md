# Apply v0.7.10.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.10.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.10.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change beyond a new, off-by-default `whitelist.json` file created under each server profile's runtime data directory on first use. New `MystTiq.Core` model (`WhitelistModels.cs`) and new `MystTiq.HeadlessHost` service (`HeadlessWhitelistService.cs`) plus two new REST routes. Remaining changes are `MystTiq.Desktop`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
