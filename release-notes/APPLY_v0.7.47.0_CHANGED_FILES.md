# Apply v0.7.47.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.47.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.47.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches `MystTiq.HeadlessHost` (`HeadlessUe4ssReleaseCatalogService.cs` — new; `LocalManagementApiHost.cs` — new `GET /api/v1/ue4ss/releases` fleet-level route) and `MystTiq.Desktop` (`Models/Ue4ssReleaseDto.cs` — new; `Services/IMystTiqApiClient.cs`/`MystTiqApiClient.cs` — new client method; `ViewModels/MainWindowViewModel.cs` — new release-catalog state, wired into the existing "Refresh Runtime" command; `MainWindow.axaml` — UE4SS page's release source card replaces its permanent stub with a real list). No change to `MystTiq.Core`.

This release makes an outbound network call to GitHub when the UE4SS page's "Refresh Runtime" is used. No new credentials or configuration are required — the call is unauthenticated, read-only, and degrades gracefully to an empty release list on failure.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
