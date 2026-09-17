# Apply v0.7.74.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.74.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.74.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change and no wire-contract change. Touches `MystTiq.Desktop` only: `App.axaml.cs` (SafeExit/ForceExit pulled into public `SafeExitAsync`/`ForceExitAsync`), `Views/ConfirmMinimizeToTrayDialog.axaml`/`.axaml.cs` (two new buttons/result values), `MainWindow.axaml.cs` (`MainWindow_Closing` handles the two new results), `ViewModels/MainWindowViewModel.cs` (startup continuation's two phases now run in independent try/catch blocks). No change to `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
