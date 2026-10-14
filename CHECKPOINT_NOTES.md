<!-- MystTiq v1.1.0.2: file reviewed for this release (2026-10-13). -->
# MystTiq v1.1.0.2 Checkpoint: Player IDs, Join Check and Automatic Character Move

## What changed

**Players the game sends to a new character get their own back, automatically** (roadmap F-2, asked 2026-10-13).
Since its 1.0 update the game can give a returning Steam player a new player ID; their character stays under the old
one. Your account (67D8D355 → E290DA9A) and Melly's (A3835C7B → 1467C601) were both affected, whatever way the server
was started.
- **Every join is checked** and listed on the new **Player IDs** card (Players page).
- **A character the game moved is moved to the new ID**: once nobody is online MystTiq stops the server, takes a safety
  backup, moves the character (level, Pals, buildings, guild), checks it, archives the old file and starts the server
  again. Or press **Restart server and move character**. Anything unexpected is refused with an alert.
- **The guard learns each player's ID from the game**, so a moved player is not turned away.
- The Launcher no longer blames -log and similar options for wrong characters.

## Verification

- **Full gate** `scripts\Test-v1.1.0.2-Logic.ps1 -RunBuild -RunLong`: 413 / 413 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried (the v1.0.6.1 identity-advice check
  pinned to the move advice and the corrected Launcher note), the frozen v1.1.0.1 gate passes, the long set passes, and
  the Linux VM checks ran and passed. The first full run stopped at one ArtworkHarness check still expecting the old
  Launcher wording; with it updated the ArtworkHarness passes 838 checks, the Player IDs card in every language included.
- **F-2:** three LogicHarness scenarios pass: the move on a made-up world (rewrite, refusals, verification), the join
  check with the identity guard, and the service moving Wade from 67D8D355 to E290DA9A on a copy of the real world with
  the real converter (safety backup, 216 references, level 15 under the new ID, old and replaced files archived).
- **Static gate:** passed. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.

## For you

1. **Connect again:** your server is running through MystTiq with its window hidden; join at `192.168.1.5:8211`.
2. **Pushes:** v1.0.7.0 to v1.1.0.2 are committed locally only; they need your yes to push and tag.
3. **Live checks only you can do:** `/mysttiq-backup` from your Discord bot; a week of the level-gap rule.