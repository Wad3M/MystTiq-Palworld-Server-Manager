<!-- MystTiq v1.0.9.3: file reviewed for this release (2026-10-12). -->
# MystTiq v1.0.9.3 Checkpoint: Level-Gap Scans

## What changed

**A new anti-cheat rule: level gain against playtime.** If a player gains far more levels than their time on your
server allows (for example, level 10 to 45 in nine minutes), MystTiq tells you, with the numbers.

- **It only reports.** It never kicks or bans; you check the player and decide.
- **It is generous on purpose.** An honest player levelling fast stays well inside it, and it takes your server's
  experience rate into account.
- **It compares from when MystTiq first saw a player.** A veteran who was already high-level is not flagged.
- **It is on by default.** Switch it off on the Anti-Cheat card (Diagnostics Center) with the other rules.

**Checked against your real world.** I decoded copies of your 17 daily backups (July 31 to October 8). The biggest real
gain, 6 levels in 17 hours, used 12% of what the rule allows. Nobody in your world is above level 15 yet, so I'll
confirm the higher levels once the rule has watched your server for a while.

## Verification

- **Full gate** `scripts\Test-v1.0.9.3-Logic.ps1 -RunBuild -RunLong`: 404 / 404 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.9.2 gate passes,
  the long set passes, and the Linux VM checks ran and passed.
- **Static gate:** 339 / 339. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **B-2:** the two LogicHarness scenarios pass (the bands, the gap check, the history rules and reading ExpRate; the rule in
  the anti-cheat service: recorded across a restart, reported with its numbers and a notice, no kick, nothing once off).
- **Calibration:** copies of the real world's 17 daily backups (2026-07-31 to 2026-10-08) decoded with the app's own
  converter; at experience rate 1 the largest real gain (6 levels in 17 hours) used 12% of the allowance.
- **ArtworkHarness** (12 languages, the rule's row) and **LogicHarness** pass.

## For you

1. **The join problem:** the backups show wrong-ID characters back to the world's first day (Melly's account has three
   characters: 84544311 from July 31, A3835C7B from August 25 and 1467C601 from August 11). That points at the game or
   account side rather than MystTiq. The double-click test will still settle it.
2. **The 24-hour runs** (v1.0.7.5) finish around 03:00 UTC on 2026-10-13; I'll collect them then.
3. **Next:** v1.1.0.0, the documentation pass and the new accepted baseline.
4. **Push:** v1.0.7.0 to v1.0.9.3 are ready to push and tag when you say so.
