# Apply v0.7.31.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.31.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.31.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release is `MystTiq.Desktop`-only: `ViewModels/TabSession.cs` (new `ServerRoot`/`DuplicateInstallWarning`/`HasDuplicateInstallWarning`); `ViewModels/MainWindowViewModel.cs` (new `RecomputeDuplicateInstallWarnings`, called from `RefreshDashboardBackendAsync` and `CloseTab`); `MainWindow.axaml` (Dashboard gains a warning banner). No product behavior changed in `MystTiq.Core` or `MystTiq.HeadlessHost` — `ServerDistributionStatusDto.ServerRoot` was already being transmitted.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
