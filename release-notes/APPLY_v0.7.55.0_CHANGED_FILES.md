# Apply v0.7.55.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.55.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.55.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches `MystTiq.HeadlessHost` (`HeadlessModManagementService.cs`, `LocalManagementApiHost.cs`) and `MystTiq.Desktop` (`Models/ModManagementDtos.cs`, `Services/IMystTiqApiClient.cs`, `Services/MystTiqApiClient.cs`, `ViewModels/MainWindowViewModel.cs`, `MainWindow.axaml`). No change to `MystTiq.Core`.

This release adds outbound HTTP calls to Steam's (`api.steampowered.com`) and GitHub's (`api.github.com`) public REST APIs, made only when the user clicks Fetch/Refresh on a MOD's description — never automatically. Fetched descriptions are cached under `{ManagerRuntimeRoot}/mod-descriptions/`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
