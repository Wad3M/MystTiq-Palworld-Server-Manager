<!-- MystTiq v1.1.4.0: file reviewed for this release (2026-10-20). -->
# v1.0.9.3 Changed Files

- `src/MystTiq.HeadlessHost/LevelGapScan.cs` (new): what an honest player can gain (per-band levels per hour at the
  server's experience rate), the gap check, the per-player history, reading `ExpRate` from `PalWorldSettings.ini`.
- `src/MystTiq.Core/Models/AntiCheatModels.cs`: `LevelGapRule` (on, 3 slack levels) in the rule set.
- `src/MystTiq.HeadlessHost/HeadlessAntiCheatService.cs`: each online player's level change recorded with the registry's
  playtime (`level-history.json`), a gap reported as a flag-only finding; the Pal scan skipped when no Pal service is given.
- `src/MystTiq.HeadlessHost/LocalManagementApiHost.cs`: the anti-cheat service is given the registry's playtime and the
  experience rate.
- Desktop: the rule's row on the Anti-Cheat card and its text in 12 languages.
- Tests: `scripts/Test-v1.0.9.3-Logic.ps1` and two LogicHarness scenarios.
