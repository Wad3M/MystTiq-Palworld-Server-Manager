<!-- MystTiq v1.0.6.1: file reviewed for this release (2026-10-06). -->
# v1.0.4.0 Changed Files

- `src/MystTiq.HeadlessHost/InventoryEdits.cs` (new): reading a player's main inventory from the decoded world, and
  removing or adding one plain stack.
- `src/MystTiq.HeadlessHost/HeadlessInventoryEditService.cs` (new): the guarded preview and apply.
- `src/MystTiq.HeadlessHost/ServerProfileHost.cs`, `LocalManagementApiHost.cs`: `GET players/{playerId}/inventory`,
  `POST players/inventory/preview`, `POST players/inventory/apply` (Admin).
- `src/MystTiq.Desktop`: `Models/InventoryDtos.cs`, `ViewModels/MainWindowViewModel.Inventory.cs` (new), the Players
  page's inventory card, the confirmation in `MainWindow.axaml.cs`, the API client, 15 new texts in all 12 languages.
- `src/MystTiq.Core/Models/ServerLifecycleModels.cs`: `ServerLifecycleSnapshot.ServerMayBeRunning` (a live process or an
  open game port); the 23 "stop PalServer first" guards in `src/MystTiq.HeadlessHost` use it instead of the last-known
  process id.
- `scripts/Test-v1.0.4.0-RouteSmoke.ps1` (new): the edits on a copy of a real world.
- Tests: `scripts/Test-v1.0.4.0-Logic.ps1` (generated from the v1.0.3.0 gate, every earlier check carried), a LogicHarness
  scenario and an ArtworkHarness check.
