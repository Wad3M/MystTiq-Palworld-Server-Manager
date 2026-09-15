# Apply v0.7.45.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.45.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.45.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches `MystTiq.HeadlessHost` (`HeadlessComponentUpdateService.cs` — new; `ServerProfileHost.cs` — new `ComponentUpdates` member; `LocalManagementApiHost.cs` — new `GET /update-center/components` route and service wiring) and `MystTiq.Desktop` (`Models/ComponentVersionDto.cs` — new; `Converters/ComponentStatusColorConverter.cs` — new; `Services/IMystTiqApiClient.cs`/`MystTiqApiClient.cs` — new client method; `ViewModels/MainWindowViewModel.cs` — new `CoreServerComponents`/`SaveRuntimeDependencyComponents` state and refresh logic; `MainWindow.axaml` — new Update Center component table). No change to `MystTiq.Core`.

This release makes outbound network calls (GitHub, PyPI, the official dotnet/core releases feed, and Steam's Web API) when Update Center's Refresh runs. No new credentials or configuration are required — every call is unauthenticated, read-only, and degrades gracefully to "Unavailable" on failure.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
