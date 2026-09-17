# Apply v0.7.80.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.80.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.80.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches only `MystTiq.Desktop` (`MainWindow.axaml` — Server Setup's status pill centering and glass-gradient fills; `Styles/DesignSystem.axaml` — new `WarningGlassGradient` resource). No change to `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
