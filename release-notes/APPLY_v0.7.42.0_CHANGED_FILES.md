# Apply v0.7.42.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.42.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.42.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is `MystTiq.Desktop`-only: `MainWindow.axaml` (13 pages' header action buttons removed, two redundant/duplicate buttons removed outright), `ViewModels/MainWindowViewModel.cs` (`BuildRibbonGroupsForActivePage` extended with 13 new per-page ribbon groups), and `MainWindow.axaml.cs` (3 new `NativeDialogAction` dispatch cases for the relocated Export buttons). No product behavior changed in `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
