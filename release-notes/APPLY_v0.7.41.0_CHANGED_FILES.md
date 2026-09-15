# Apply v0.7.41.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.41.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.41.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release touches `MystTiq.HeadlessHost` (`HeadlessModManagementService.cs`: new `CheckModUpdateAsync` and supporting helpers; `LocalManagementApiHost.cs`: new `GET /mods/{type}/{package}/check-update` route) and `MystTiq.Desktop` (`Models/ModManagementDtos.cs`: new `ModUpdateCheckResultDto`; `Services/IMystTiqApiClient.cs`/`MystTiqApiClient.cs`: new `CheckModUpdateAsync`; `ViewModels/MainWindowViewModel.cs`: new commands/properties; `MainWindow.axaml`: new UPDATE section in MOD Library's details panel). No existing routes, install logic, or data changed — this is purely additive.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
