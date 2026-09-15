# Apply v0.7.39.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.39.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.39.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release touches `MystTiq.HeadlessHost` (`HeadlessModManagementService.cs`: MOD ZIP install now auto-detects PAK vs. UE4SS from the archive's own contents) and `MystTiq.Desktop` (`MainWindow.axaml`/`ViewModels/MainWindowViewModel.cs`: the manual install-type dropdown is removed). Existing installed MODs, snapshots, and inventory are unaffected — this only changes how a *new* ZIP install decides its type.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
