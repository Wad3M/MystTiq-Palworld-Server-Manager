<!-- MystTiq v1.0.0.4: file reviewed for this release (2026-10-05). -->
# v1.0.0.3 Changed Files

## NATIVE MODs (2026-10-05)

- `src/MystTiq.HeadlessHost/NativeModCatalog.cs` (new): the pure rules: PalDefender's loaders (`d3d9.dll` with
  `d3d9_config.json`, or `version.dll`), the UE4SS loaders (`dwmapi.dll`, `xinput1_3.dll`), the switched-off forms
  (`.mysttiq-disabled`, `.myst-disabled`, `.disabled`, `.disabled-test`, `.off`, `.bak-disabled`) and which copy to restore.
- `src/MystTiq.HeadlessHost/HeadlessModManagementService.cs`: NATIVE items in the inventory (version, loaded this run from
  the log's time), `ToggleNative`, routing in enable/disable and enable/disable all, UE4SS MODs marked when the loader is
  off, refusals for delete/rollback/repair, and a PalDefender ZIP refused with where its files go.
- `src/MystTiq.HeadlessHost/HeadlessEnvironmentChecklistService.cs`: the UE4SS check recognises every switched-off form.

## MOD drop zone (2026-10-05)

- `src/MystTiq.Desktop/MainWindow.axaml`: the drop zone has a transparent background so all of it takes the drop.
- `src/MystTiq.Desktop/MainWindow.axaml.cs`: drag-enter handled; the events are marked handled.

## Other

- `src/MystTiq.Desktop/Assets/i18n/*.json`: 17 new texts in all 12 languages.
- Tests: `scripts/Test-v1.0.0.3-Logic.ps1` (generated from the v1.0.0.2 gate, every earlier check carried),
  `scripts/Test-v1.0.0.3-RouteSmoke.ps1`, a logic-harness scenario, and an ArtworkHarness drop on the zone's empty corner
  (it fails without the fix).
