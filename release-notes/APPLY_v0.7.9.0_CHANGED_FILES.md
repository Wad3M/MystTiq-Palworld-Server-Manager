# Apply v0.7.9.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.9.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.9.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. No `MystTiq.Core` model changes — the new fields live entirely in `MystTiq.HeadlessHost` (`HeadlessMonitoringService.cs`) and `MystTiq.Desktop` (`MonitoringDtos.cs`, `MainWindowViewModel.cs`, `MainWindow.axaml`).

Do not overlay it onto v0.4.17.4 or an unknown source tree.
