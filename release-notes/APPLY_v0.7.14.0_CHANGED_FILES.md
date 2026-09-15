# Apply v0.7.14.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.14.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.14.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is `MystTiq.Desktop`-only: `MainWindow.axaml` (16 new `AutomationProperties.Name` additions, tab-close button resize 20px→28px, window-chrome tooltips) and `Styles/DesignSystem.axaml` (5 new `:focus-visible` style overrides). No product behavior changed in `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
