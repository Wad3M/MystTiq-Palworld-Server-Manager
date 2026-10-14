<!-- MystTiq v1.1.1.0: file reviewed for this release (2026-10-14). -->
# v1.0.9.2 Changed Files

- `src/MystTiq.HeadlessHost/ModLoadOrder.cs` (new): reading and reordering `mods.txt` (only the MOD lines move).
- `src/MystTiq.HeadlessHost/HeadlessModManagementService.cs`: the load order, saving a new one, profile export and import
  (preview, then apply), each change with the server stopped and the previous `mods.txt` kept.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: `GET`/`POST /mods/load-order`, `GET /mods/profile`,
  `POST /mods/profile/import`.
- Desktop: the client calls, `ModsViewModel` (load order list, moves, save; profile preview and apply), the MOD
  Library card, the export and import file dialogs, the role table (saving the order and applying a profile need Admin),
  and the card's text in all 12 languages.
- Tests: `scripts/Test-v1.0.9.2-Logic.ps1`, `scripts/Test-v1.0.9.2-RouteSmoke.ps1`, three LogicHarness scenarios.
