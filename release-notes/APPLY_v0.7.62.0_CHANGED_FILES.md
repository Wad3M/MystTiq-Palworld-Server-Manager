# Apply v0.7.62.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.62.0 source baseline.
Normal installation should use `MystTiqPalworldServer_v0.7.62.0_FullSource.zip` with
`Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly,
and the complete current-version gate runs before relaunch.

No configuration schema change, no new routes. Touches `MystTiq.Desktop` only
(`MainWindow.axaml` — a single `MaxWidth="620"` attribute added to the page-header panel). No
`MystTiq.Core` or `MystTiq.HeadlessHost` changes.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
