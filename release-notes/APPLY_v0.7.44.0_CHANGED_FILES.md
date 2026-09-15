# Apply v0.7.44.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.44.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.44.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches all three projects: `MystTiq.Core` (`Models/ServerLifecycleModels.cs` — new `ServerInstanceInfo`/`InstanceTerminationResult` records; `Services/LinuxServerLifecycleService.cs` — new `IServerLifecycleService.FindAllInstancesAsync`/`TerminateUnmanagedInstanceAsync` interface members plus Linux implementation; `Services/WindowsServerLifecycleService.cs` — Windows implementation), `MystTiq.HeadlessHost` (`LocalManagementApiHost.cs` — new `GET /server/instances`, `POST /server/instances/{processId}/terminate` routes), and `MystTiq.Desktop` (`Models/ServerInstanceDto.cs` — new; `Services/IMystTiqApiClient.cs`/`MystTiqApiClient.cs` — new client methods; `ViewModels/MainWindowViewModel.cs` — new `AllInstances`/`SelectedInstance` state, `RefreshAllInstancesCommand`/`TerminateSelectedInstanceCommand`; `MainWindow.axaml` — new Server Doctor panel + ribbon button).

Do not overlay it onto v0.4.17.4 or an unknown source tree.
