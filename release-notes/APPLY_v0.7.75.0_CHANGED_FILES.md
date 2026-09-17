# Apply v0.7.75.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.75.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.75.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change. Touches `MystTiq.HeadlessHost` (new `HeadlessPlayerDeletionService.cs`, `HeadlessPlayerCopyService.cs`, a new `Forget` method on `HeadlessPlayerRegistryService.cs`, `ServerProfileHost.cs`/`LocalManagementApiHost.cs` wiring, 4 new routes), and `MystTiq.Desktop` (new `Models/PlayerOperationDtos.cs`, API client methods, new dialogs `ConfirmDeletePlayerDialog`/`ConfirmCopyPlayerDialog`/`SelectPlayerDialog`, `MainWindow.axaml`/`.axaml.cs` context-menu wiring, `ViewModels/MainWindowViewModel.cs` new methods, and a new `MenuItem:disabled` style in `Styles/DesignSystem.axaml`). No change to `MystTiq.Core`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
