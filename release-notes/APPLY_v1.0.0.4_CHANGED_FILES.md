<!-- MystTiq v1.0.2.0: file reviewed for this release (2026-10-05). -->
# v1.0.0.4 Changed Files

## The world's day (2026-10-05)

- `src/MystTiq.HeadlessHost/HeadlessWorldClockService.cs` (new): reads the day from the decoded `Level.sav.json` with whether
  it is current (`WorldClock.IsCurrent`), re-decodes a copy of `Level.sav` in the background when it is newer (at most every
  two minutes per world), decodes the restored world after a restore, and reads a backup ZIP's own `Level.sav`.
- `src/MystTiq.HeadlessHost/HeadlessWorldExplorerService.cs`: both world views read the clock through it (the old reader
  is gone) and report `WorldClockCurrent` / `WorldClockAsOfUtc`.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`, `ServerProfileHost.cs`: wiring; `/status/poll` starts the re-decode.

## Restore (2026-10-05)

- `src/MystTiq.HeadlessHost/HeadlessBackupService.cs`: the save folder is moved aside with ten tries half a second apart,
  then the holder is named (`FileLockers`); a PalServer from the server's folder started outside MystTiq is refused; the
  restored world's day is reported against the backup's; restores are logged; each backup's day is read in the background
  (`world-days.json`) and listed (`WorldDayNumber`, `WorldTimeText`).
- `src/MystTiq.HeadlessHost/FileLockers.cs` (new): the Windows Restart Manager, to name the program holding a file.

## Desktop

- `Models/BackupConfigurationDtos.cs`, `Models/WorldExplorerDtos.cs`, `ViewModels/MainWindowViewModel.cs`, `MainWindow.axaml`:
  the World day column in the backups list, and the Dashboard's wording when the day is from an older save.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 11 new texts in all 12 languages (including "Safety backup: {0}", so a restore
  message translates sentence by sentence).

## Tests

- `scripts/Test-v1.0.0.4-Logic.ps1` (generated from the v1.0.0.3 gate, every earlier check carried),
  `scripts/Test-v1.0.0.4-RouteSmoke.ps1` (real backups restored into an isolated server), a logic-harness scenario and
  ArtworkHarness checks.
