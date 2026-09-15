# Apply v0.7.7.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.7.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.7.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. This is the one release in the current sequence with a server-side change: `MystTiq.HeadlessHost` (`HeadlessBackupService.cs`, `LocalManagementApiHost.cs`, `HeadlessWorldTransactionService.cs`, `HeadlessModManagementService.cs`). No `MystTiq.Core` model/contract changes and no `MystTiq.Desktop` changes.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
