# Apply v0.7.40.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.40.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.40.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is `MystTiq.Desktop`-only: `MainWindow.axaml` (MOD Library: 3-column layout, new MOD DETAILS panel, Install Validated ZIP moved to top) and `ViewModels/MainWindowViewModel.cs` (new `HasSelectedMod` computed property). No product behavior changed in `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
