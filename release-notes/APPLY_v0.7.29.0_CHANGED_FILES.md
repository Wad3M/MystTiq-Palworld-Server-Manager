# Apply v0.7.29.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.29.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.29.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release touches both `MystTiq.Desktop` and `MystTiq.Core`: `ViewModels/MainWindowViewModel.cs` (new `GameplayRateConfigurationNames`, `ApplySelectedConfigurationPreset`'s Official branch, `ApplyStatus`'s `DashboardSessionText`/`DashboardHealthDetail`); `Services/WindowsServerLifecycleService.cs` (new `FindProcessesWithMismatchedPath`/`NormalizedServerRoot`, `GetStatusAsync`'s not-running fallback). No product behavior changed in `MystTiq.HeadlessHost`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
