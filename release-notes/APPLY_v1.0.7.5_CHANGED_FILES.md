<!-- MystTiq v1.1.1.1: file reviewed for this release (2026-10-18). -->
# v1.0.7.5 Changed Files

- `src/MystTiq.Core/Services/ServerInstallIntegrity.cs` (new): the update marker and Steam's manifest state.
- `src/MystTiq.HeadlessHost/HeadlessServerDistributionService.cs`: the marker around SteamCMD; the status's
  `IncompleteReason` (and `serverExecutableExists` false while incomplete).
- `src/MystTiq.Core/Services/WindowsServerLifecycleService.cs`, `LinuxServerLifecycleService.cs`: a half-updated server
  is not started.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: the update routes run to their end when the client disconnects.
- `src/MystTiq.HeadlessHost/HeadlessBackupService.cs`: a held save file is waited for (10 tries), then named.
- `src/MystTiq.Desktop/Models/ServerDistributionDtos.cs`, `ViewModels/MainWindowViewModel.cs`: "Incomplete" and "Update
  required" (12 languages in `Assets/i18n`).
- Tests: `scripts/Test-v1.0.7.5-Logic.ps1` (generated from the v1.0.7.4 gate, every earlier check carried, `-RunLong`),
  `scripts/Test-v1.0.7.5-LongSet.ps1`, `scripts/Measure-MystTiqIdleSoak.ps1` and `.sh`,
  `scripts/Testing/Send-MystTiqConsoleStop.ps1`, `scripts/Testing/FakeSteamCmd`, a LogicHarness scenario and an
  ArtworkHarness check (rapid tab switching).
