# Apply v0.7.20.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.20.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.20.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is `MystTiq.Desktop`-only: new `Services/MapPresetService.cs`, `Services/LocalMapPreferencesStore.cs` (one new public method), `ViewModels/MainWindowViewModel.cs` (preset command/state), `MainWindow.axaml` (World Map card restructuring), and two new PNG files under `Assets/Maps/`. No product behavior changed in `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
