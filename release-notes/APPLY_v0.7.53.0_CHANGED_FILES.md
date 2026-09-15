# Apply v0.7.53.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.53.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.53.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches only `MystTiq.Desktop` (`MainWindow.axaml`, Dashboard section only). No change to `MystTiq.Core` or `MystTiq.HeadlessHost`, and no ViewModel changes.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
