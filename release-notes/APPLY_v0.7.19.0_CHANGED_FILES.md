# Apply v0.7.19.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.19.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.19.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is `MystTiq.Desktop`-only: `MainWindow.axaml` (all 25 nav destinations now use the new icon set), `Styles/IconGeometries.axaml` (7 now-unused `StreamGeometry` resources removed), and 25 new PNG files under `Assets/Icons/`. No product behavior changed in `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
