# Apply v0.7.77.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.77.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.77.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches `MystTiq.HeadlessHost` (`HeadlessModManagementService.cs` — Workshop-runtime detection fix, hash-based UE4SS version detection, new `RepairModAsync`; `LocalManagementApiHost.cs` — new repair route) and `MystTiq.Desktop` (`Services/MystTiqApiClient.cs`/`IMystTiqApiClient.cs`, `ViewModels/MainWindowViewModel.cs`, `MainWindow.axaml` — new Repair/Re-install Selected button). No change to `MystTiq.Core`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
