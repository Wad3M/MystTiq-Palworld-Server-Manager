<!-- MystTiq v1.0.1.0: file reviewed for this release (2026-10-05). -->
# v1.0.0.2 Changed Files

## Unique player names (2026-10-04)

- `src/MystTiq.HeadlessHost/HeadlessNameGuardService.cs` (new): the claims (`players/name-guard.json`), the players turned
  away (`players/name-guard-events.json`), and the pure rules (`NameGuard.Key`, `Judge`, `Seed`, `Normalize`). Runs on the
  `/status/poll` cadence after the identity guard; on first use the names come from the player registry.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`, `ServerProfileHost.cs`: wiring, the poll, and
  `GET /players/name-guard` (Viewer) / `PUT /players/name-guard` (Admin).
- Desktop: `Models/NameGuardDtos.cs`, `Services/NameGuardText.cs`, `ViewModels/MainWindowViewModel.NameGuard.cs`, the
  commands in `MainWindowViewModel.cs` and `MainWindowViewModel.CommandRoles.cs` (Save is Admin), API client calls, and the
  Players page's Unique player names card in `MainWindow.axaml`.
- `src/MystTiq.Desktop/ViewModels/MainWindowViewModel.CommandRoles.cs` (`RaiseAllAsyncCommandStates`) and
  `MainWindowViewModel.cs` (`RaiseIsBusyDependents` calls it): gated buttons re-enabled when the app stops being busy.
- `src/MystTiq.Desktop/Assets/i18n/*.json`: 32 new texts in all 12 languages.
- Tests: `scripts/Test-v1.0.0.2-Logic.ps1` (generated from the v1.0.0.1 gate, every earlier check carried),
  `scripts/Test-v1.0.0.2-RouteSmoke.ps1`, a logic-harness scenario and ArtworkHarness checks.
