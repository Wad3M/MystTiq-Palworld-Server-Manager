# Apply v0.7.26.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.26.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.26.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is `MystTiq.Desktop`-only: `MainWindow.axaml` (Server Setup/Configuration/Console pages, ribbon button template's new `NativeDialogAction` dispatch); `MainWindow.axaml.cs` (`RibbonAction_OnClick`, `InvokeRibbonAction`); `Models/RibbonActionViewModel.cs` (new `NativeDialogAction` field); `ViewModels/MainWindowViewModel.cs` (`BuildRibbonGroupsForActivePage` extended with per-page groups, `LoadPalworldConfigurationCommand` removed). No product behavior changed in `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
