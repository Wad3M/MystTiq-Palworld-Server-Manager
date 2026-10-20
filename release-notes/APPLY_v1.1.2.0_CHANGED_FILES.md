<!-- MystTiq v1.1.4.0: file reviewed for this release (2026-10-20). -->
# v1.1.2.0 Changed Files

- `src/MystTiq.Core/Services/ServerStopReason.cs` (new): the reason a stop is made for, carried with the call.
- `src/MystTiq.Core/Models/ServerLifecycleModels.cs`: `PersistedServerLifecycleState.StopReason`; `ServerLifecycleSnapshot.LastStopReason`, `LastStopAt`, `WithLastStop`.
- `src/MystTiq.Core/Services/WindowsServerLifecycleService.cs`, `LinuxServerLifecycleService.cs`: the reason saved with each stop state; a restart says "Restart" unless its caller said why; a stopped status carries the last stop.
- `src/MystTiq.Core/Services/HeadlessSupervisor.cs`: reasons for a frozen-server restart and the service stopping.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: `?reason=` and the caller on `/server/stop`, `/force-stop`, `/restart`; the character move's reason; each stop written to the activity log.
- `src/MystTiq.HeadlessHost/PolicyApplyingLifecycle.cs`: `StopRecorded`.
- `src/MystTiq.HeadlessHost/HeadlessAutomationService.cs`, `HeadlessDiscordBotService.cs`, `HeadlessModSafeStartService.cs`, `Program.cs`: their stop reasons.
- `src/MystTiq.Desktop/Services/IMystTiqApiClient.cs`, `MystTiqApiClient.cs`: the optional reason on stop, force stop and restart.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`, `MainWindowViewModel.Exit.cs`: the desktop's reasons; the Last stop line; a world save when Windows shuts down.
- `src/MystTiq.Desktop/Models/ServerStatusDto.cs`: `lastStopReason`, `lastStopAt`.
- `src/MystTiq.Desktop/Services/StartWithSystem.cs` (new), `ViewModels/MainWindowViewModel.StartWithSystem.cs` (new): start at sign-in (the current user's Run key; XDG autostart on Linux).
- `src/MystTiq.Desktop/App.axaml.cs`, `MainWindow.axaml.cs`, `MainWindow.axaml`: `--tray`, the Windows shutdown close, the Settings option, the health card's tooltip.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 7 new keys in 12 languages.
- Scripts: `-n` on the non-interactive `ssh` calls in 15 VM scripts.
- Tests: `scripts/Test-v1.1.2.0-Logic.ps1`; LogicHarness (4 scenarios) and ArtworkHarness (2 checks).
