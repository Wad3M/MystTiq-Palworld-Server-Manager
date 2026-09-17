# Apply v0.7.81.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.81.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.81.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches only `MystTiq.Desktop` (`ViewModels/MainWindowViewModel.cs` — new wizard step machine, World Source choice, Clone-into-new-server, environment checklist auto-load fix, starter preset apply chain; `MainWindow.axaml`/`.axaml.cs` — wizard layout restructure; `ViewModels/TabSession.cs` — new `IsNewServerSetupFlow`/`WorldSource` per-tab fields). New test script `scripts/Test-v0.7.81.0-RouteSmoke.ps1`. No change to `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
