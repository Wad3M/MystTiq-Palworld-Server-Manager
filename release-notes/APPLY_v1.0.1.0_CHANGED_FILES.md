<!-- MystTiq v1.0.2.0: file reviewed for this release (2026-10-05). -->
# v1.0.1.0 Changed Files

- `src/MystTiq.Desktop/Models/ComponentVersionDto.cs`: `UpdateMethod`, `CanUpdate`, `UpdateHint`, `ShowsOpen` for every
  row; `CanUpdateInPlace` (pip only) removed; the PlM/Oodle row's page fixed.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.ComponentUpdates.cs` (new): `UpdateComponentCommand` and what each
  row's Update does; the UE4SS preview step shared with the install-with-extras flow.
- `src/MystTiq.Desktop/Services/MystTiqSelfUpdate.cs` (new): MystTiq's download, checksum check and unpack into a new folder.
- `src/MystTiq.Desktop/Services/IMystTiqApiClient.cs`, `MystTiqApiClient.cs`: the PalDefender and Save Tools updates.
- `src/MystTiq.Desktop/MainWindow.axaml`: Update, Open and the reason on every row of both lists; the last outcome above them.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.cs`, `MainWindowViewModel.CommandRoles.cs`: `UpdatePipCommand`
  folded into `UpdateComponentCommand` (Admin).
- `src/MystTiq.HeadlessHost/PalDefenderUpdater.cs` (new): the PalDefender update.
- `src/MystTiq.HeadlessHost/HeadlessComponentUpdateService.cs`: `UpdatePalDefenderAsync`, `UpdateSaveToolsAsync`; row texts.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: `POST update-center/components/paldefender/update` (refused
  while PalServer runs) and `save-tools/update`.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 25 new texts in all 12 languages.
- Tests: `scripts/Test-v1.0.1.0-Logic.ps1` (generated from the v1.0.0.6 gate, every earlier check carried), a
  LogicHarness scenario (PalDefender) and ArtworkHarness checks (every row, the reasons, MystTiq's download).
