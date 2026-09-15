# Apply v0.7.43.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.43.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.43.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is `MystTiq.Desktop`-only: `MainWindow.axaml` (MOD Dashboard's new read-only Installed MODs list + details panel, footer elapsed-time column, nav icons doubled to 56×56), `ViewModels/MainWindowViewModel.cs` (`BusyElapsedText`/`_busyElapsedTimer`/server-name capture in `RaiseIsBusyDependents`), and `Styles/DesignSystem.axaml` (nav row height 58→72). No product behavior changed in `MystTiq.Core` or `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
