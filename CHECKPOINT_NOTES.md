<!-- MystTiq v1.1.0.3: file reviewed for this release (2026-10-13). -->
# MystTiq v1.1.0.3 Checkpoint: Player ID Clean-up

## What changed

**A character move now leaves nothing behind**, as you asked.
- The old character file is removed once the move is checked (the safety backup taken first keeps it), instead of
  being kept in an archive folder.
- The four files v1.1.0.2 archived are removed when MystTiq starts, because its two pre-move safety backups hold them.
- MystTiq's player list is tidied: your and Melly's histories follow you to your new IDs, old entries are folded in,
  and nobody is listed under "None" any more.
- The world file needed nothing: the empty characters' storage was never kept by the game.

## Verification

- **Full gate** `scripts\Test-v1.1.0.3-Logic.ps1 -RunBuild -RunLong`: 416 / 416 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried (the v1.1.0.2 move check pinned from
  the archive to the removal), the frozen v1.1.0.2 gate passes, the long set passes, the ArtworkHarness passes, and the
  Linux VM checks ran and passed.
- **F-3:** the LogicHarness clean-up scenario passes (no "None" record, a move's merge, stale records folded into the
  current ID, an unknown account's "None" removed, an archived file a backup holds removed and one it does not kept),
  and the real-world move leaves no archive folder and merges the player list record.
- **Static gate:** passed. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.

## For you

1. **Connect:** the server runs through MystTiq with its window hidden; join at `192.168.1.5:8211`.
2. **Pushes:** v1.0.7.0 to v1.1.0.3 are committed locally only; they need your yes to push and tag.