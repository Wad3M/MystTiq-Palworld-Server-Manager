# Apply v0.7.64.0 Changed Files

The Changed Files ZIP is an audit/convenience package for the promoted v0.7.64.0 source baseline. Normal installation should use `MystTiqPalworldServer_v0.7.64.0_FullSource.zip` with `Update-FromDownloads.ps1` so the running artifact app closes, the staged source replaces cleanly, and the complete current-version gate runs before relaunch.

No configuration schema change and no wire-contract-breaking change — existing automation rules with already-valid values are unaffected; only invalid input is newly rejected. Touches `MystTiq.Core` (`Services/ConsoleLogRotation.cs` new, `Services/WindowsServerLifecycleService.cs`), `MystTiq.HeadlessHost` (`HeadlessAutomationService.cs`, `HeadlessConsoleLogWriter.cs`, `LocalManagementApiHost.cs`, `HeadlessEnvironmentChecklistService.cs`), and `MystTiq.Desktop` (`MainWindow.axaml`, text only). Also touches `scripts/Testing/MystTiq.LogicHarness/Program.cs` and adds `scripts/Test-v0.7.64.0-RouteSmoke.ps1`.

Do not overlay it onto v0.4.17.4 or an unknown source tree.
