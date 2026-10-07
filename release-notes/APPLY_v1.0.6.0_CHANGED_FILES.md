<!-- MystTiq v1.0.6.1: file reviewed for this release (2026-10-06). -->
# v1.0.6.0 Changed Files

- `src/MystTiq.HeadlessHost/PalBoxEdits.cs` (new): reading a player's Pal box from the decoded world; adding a copy of a
  boxed Pal, or removing a boxed Pal with its slot and guild entry; the verification.
- `src/MystTiq.HeadlessHost/HeadlessPalBoxEditService.cs` (new): the guarded preview and apply.
- `src/MystTiq.HeadlessHost/ServerProfileHost.cs`, `LocalManagementApiHost.cs`: `GET players/{playerId}/palbox`,
  `POST players/palbox/preview`, `POST players/palbox/apply` (Admin).
- `src/MystTiq.Core/Services/ModArchivePlanner.cs`: LogicMods PAKs and PAKs with scripts install (where each part goes).
- `src/MystTiq.HeadlessHost/HeadlessModManagementService.cs`: the two new layouts (UE4SS required, both destinations
  checked first, a rollback snapshot for each part, the BPModLoaderMod note).
- `src/MystTiq.HeadlessHost/FileRetry.cs` (new): a short retry for a held file. Used by the inventory and Pal box edits'
  final replace and by the restore's cleanup, which now names files belonging to administrators.
- `src/MystTiq.Desktop`:
  - `Models/PalBoxDtos.cs` and `ViewModels/MainWindowViewModel.PalBox.cs` (new);
  - the Players page's Pal box card;
  - its confirmation in `MainWindow.axaml.cs`;
  - the API client;
  - 28 texts in all 12 languages (two of them reworded).
- `scripts/Test-v1.0.6.0-RouteSmoke.ps1` (new): the Pal box on a copy of a real world.
- `scripts/Test-v1.0.6.0-ModLayoutSmoke.ps1` (new): the MOD layouts through the service, replacing the v1.0.5.0 MOD smoke
  in the gate.
- `scripts/Test-v1.0.6.0-LinuxUserSystemd.ps1/.sh` (new): the Linux service as a per-user systemd unit on the VM; needs no
  sudo and never reboots the VM.
- Tests: `scripts/Test-v1.0.6.0-Logic.ps1` (generated from the v1.0.5.0 gate, every earlier check carried), LogicHarness
  scenarios for the Pal box and the layouts, and an ArtworkHarness check for the card.
