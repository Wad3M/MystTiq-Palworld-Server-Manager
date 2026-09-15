# Apply v0.7.21.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.21.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.21.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is `MystTiq.Desktop`-only: new `Services/PalworldMapCoordinates.cs`, `Services/MapPresetService.cs` (one new method), `ViewModels/MainWindowViewModel.cs` (experimental toggle + calibrated map-point branch), and `MainWindow.axaml` (new checkbox). No product behavior changed in `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
