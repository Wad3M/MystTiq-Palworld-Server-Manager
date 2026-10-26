<!-- MystTiq v1.1.6.0: file reviewed for this release (2026-10-26). -->
# v1.0.7.2 Changed Files

- `src/MystTiq.Core/Services/StartupProgress.cs` (new), `Models/ServerLifecycleModels.cs` (the start's step on the status),
  `WindowsServerLifecycleService.cs` and `LinuxServerLifecycleService.cs` (the steps, carried by status reads, named on failure).
- `src/MystTiq.HeadlessHost/HeadlessBackupService.cs`: the scan's progress, unreadable archives, cancel, batched world-day
  saves; `LocalManagementApiHost.cs`: `POST /backups/scan/cancel`.
- Desktop: `Models/ServerStatusDto.cs` and `MainWindow.axaml` (the step on the Dashboard); `Models/BackupConfigurationDtos.cs`,
  `Services/MystTiqApiClient.cs`, `ViewModels/MainWindowViewModel.cs` and the Backups page (the scan card, Cancel, progress
  read while Verify All runs); `ViewModels/MainWindowViewModel.PageWait.cs` (new) and the page-wait banner;
  `Services/ConnectionCause.cs` (new), `ViewModels/MainWindowViewModel.ConnectionCause.cs` (new), `ViewModels/TabSession.cs`,
  `Services/StatusTags.cs` and the connection cause line.
- 26 texts in all 12 languages.
- Tests: `scripts/Test-v1.0.7.2-Logic.ps1` (generated from the v1.0.7.1 gate, every earlier check carried),
  `scripts/Test-v1.0.7.2-RouteSmoke.ps1`, LogicHarness scenarios and ArtworkHarness checks.
