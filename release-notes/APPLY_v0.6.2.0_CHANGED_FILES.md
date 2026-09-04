# Apply v0.6.2.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.2.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.2.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

An existing single-server `mysttiq.json` (schema v2) migrates automatically to schema v3 on first load — the single `server` block becomes one `"default"` entry in `servers`, and every existing unprefixed API route keeps working unchanged.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
