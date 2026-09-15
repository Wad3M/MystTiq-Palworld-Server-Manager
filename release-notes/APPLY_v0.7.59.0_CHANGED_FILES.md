# Apply v0.7.59.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.59.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.59.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches `MystTiq.HeadlessHost` (new `HeadlessModSafeStartService.cs`, `ServerProfileHost.cs`, `LocalManagementApiHost.cs`) and `MystTiq.Desktop` (`Models/ModManagementDtos.cs`, `Services/IMystTiqApiClient.cs`, `Services/MystTiqApiClient.cs`, `ViewModels/MainWindowViewModel.cs`, `MainWindow.axaml`). No change to `MystTiq.Core`.

**This version adds real live-server behavior**: the new Safe-Start Diagnostic feature, once triggered by a user, repeatedly stops and starts the actual PalServer process to test MODs one at a time. It only runs on explicit user action (a button click) — never automatically.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
