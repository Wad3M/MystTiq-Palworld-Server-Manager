# Apply v0.7.51.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.51.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.51.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change (the new `ConnectionProfile.AccentColorKey` field is optional with a backward-compatible default). Touches only `MystTiq.Desktop` (`Models/ConnectionProfile.cs`, `Services/ThemeCatalog.cs`, `ViewModels/TabSession.cs`, `ViewModels/MainWindowViewModel.cs`, `MainWindow.axaml`). No change to `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
