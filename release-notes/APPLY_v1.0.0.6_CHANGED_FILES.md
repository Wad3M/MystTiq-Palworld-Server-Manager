<!-- MystTiq v1.0.4.0: file reviewed for this release (2026-10-05). -->
# v1.0.0.6 Changed Files

- `src/MystTiq.Desktop/Services/ButtonIntents.cs` (new): the intent table (danger, open, verify, apply, info, caution,
  plain) by English label, and `ButtonIntent.Label` for buttons whose label changes at runtime.
- `src/MystTiq.Desktop/Services/StatusTags.cs` (new): the tag kinds (ok, warn, fail, off, info) by English status, and
  `StatusTag.Status`.
- `src/MystTiq.Desktop/Styles/DesignSystem.axaml`: the button looks renamed to intents (`primary` to `apply`, `success` to
  `verify`, `warning` to `caution`, `inspectAction` to `info`, `targetAction` to `open`); `Button.plain`, `Button.marker`,
  `Button.listItem` and `Border.tag` with its five kinds added.
- `src/MystTiq.Desktop/MainWindow.axaml` and `src/MystTiq.Desktop/Views/*.axaml`: every button has one intent or a
  structural class (303 buttons); 21 status tags use `Border.tag`; map markers lose their per-button colours.
- `src/MystTiq.Desktop/Views/ConfirmOperationDialog.axaml(.cs)`: the confirm button's intent is set through `ButtonIntents.Set`.
- `src/MystTiq.Desktop/Models/EnvironmentChecklistDtos.cs`, `DiagnosticFindingDtos.cs`: look-only properties removed.
- Removed: `src/MystTiq.Desktop/Converters/ComponentStatusColorConverter.cs` (the Update Center's status colours are tags now).
- Tests: `scripts/Test-v1.0.0.6-Logic.ps1` (generated from the v1.0.0.5 gate, every earlier check carried; new contracts
  read every button and tag in the XAML) and ArtworkHarness checks (the table, every page's buttons, the colours, the tags).
