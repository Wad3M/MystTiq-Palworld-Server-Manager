# Apply v0.7.30.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.30.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.30.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This release touches `MystTiq.Desktop` and `MystTiq.HeadlessHost`: `ViewModels/MainWindowViewModel.cs` (new `PopulateBackupItems`, three call sites updated; `DoctorSummary` appends `OverallHealthDetail`); `HeadlessModManagementService.cs` (new `IsMeaningfulVersion`, `DetectUe4ssVersion` updated). No product behavior changed in `MystTiq.Core` or `PalworldManager`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
