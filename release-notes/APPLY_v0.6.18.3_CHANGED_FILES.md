# Apply v0.6.18.3 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.6.18.3 source baseline. Normal installation should use `MystTiqPalworldServer_v0.6.18.3_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. No server-side change at all — every changed file in this release is under `src/MystTiq.Desktop`. New local file: `%AppData%/MystTiq/config-presets.json` (Windows), created automatically the first time a custom preset is saved.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
