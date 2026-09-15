# Apply v0.7.49.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.49.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.49.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches `MystTiq.HeadlessHost` (`HeadlessModManagementService.cs` — new UE4SS install/rollback methods, engine snapshot helpers, DTOs; `LocalManagementApiHost.cs` — four new `/ue4ss/install/*` routes and their request DTOs) and `MystTiq.Desktop` (`Services/IMystTiqApiClient.cs`/`Services/MystTiqApiClient.cs` — new install/rollback API client methods; `Models/Ue4ssReleaseDto.cs` — new preview/result/status DTOs; `ViewModels/MainWindowViewModel.cs` — new selection/preview/state properties and three new commands; `MainWindow.axaml` — UE4SS release list converted to a selectable `ListBox`, ribbon gains Preview Install/Confirm Install/Rollback). No change to `MystTiq.Core`.

This release writes into a server's UE4SS engine files when an install or rollback is explicitly confirmed by the operator (PalServer must be stopped first). It never touches mod files, `mods.txt`, or `mods.json`. A safety snapshot is taken automatically before every install.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
