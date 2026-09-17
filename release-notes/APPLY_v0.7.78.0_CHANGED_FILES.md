# Apply v0.7.78.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.78.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.78.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches `MystTiq.HeadlessHost` (`HeadlessModManagementService.cs` — per-mod update-availability and installed-version fields on `HeadlessModItem`, computed during `GetInventoryAsync`; new persisted UE4SS runtime-verification marker used by `ResolveUe4ss`'s `HealthState`) and `MystTiq.Desktop` (`ViewModels/MainWindowViewModel.cs` — MOD Library auto-scan trigger, new ribbon "MOD Maintenance" group, `ModInstallPackage` removed, `IsModHealthGood`/`IsModHealthDegraded`; `MainWindow.axaml`/`MainWindow.axaml.cs` — MOD Library layout restructure, right-click context menu, drag-and-drop ZIP install, MOD Dashboard hero banner and recolored cards; `Models/ModManagementDtos.cs` — new `UpdateAvailable`/`UpdateHint`/`InstalledVersion` fields; `Styles/DesignSystem.axaml` — `Border.statuscard` padding fix, app-wide). No change to `MystTiq.Core`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
