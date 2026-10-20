<!-- MystTiq v1.1.1.3: file reviewed for this release (2026-10-19). -->
# v1.1.0.2 Changed Files

- `src/MystTiq.HeadlessHost/PlayerIdMove.cs` (new): the move on the decoded world and character file (checks, rewrite,
  verification).
- `src/MystTiq.HeadlessHost/HeadlessPlayerIdService.cs` (new): the join check, learned IDs, queued moves, the move as a
  transaction (safety backup, verification, archive, rollback) and running queued moves once nobody is online.
- `src/MystTiq.HeadlessHost/HeadlessIdentityGuardService.cs`: expects the learned ID, gives the move message, lets the
  new-character screen through with automatic moves on, advises the move.
- `src/MystTiq.HeadlessHost/HeadlessAntiCheatService.cs`: `ForgetLevelHistory` (a moved character no longer reads as a
  level gap).
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`, `ServerProfileHost.cs`: wiring, the poll and the routes
  `GET /players/ids`, `PUT /players/ids/settings`, `POST /players/ids/move` (Admin for changes).
- Desktop: the Player IDs card (`MainWindow.axaml`, `PlayersViewModel.cs`, `PlayerIdDtos.cs`, the API client, the role
  table), the corrected Launcher note, and their text in 12 languages.
- Tests: `scripts/Test-v1.1.0.2-Logic.ps1` and three LogicHarness scenarios.
