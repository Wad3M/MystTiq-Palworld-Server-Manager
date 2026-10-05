<!-- MystTiq v1.0.0.2: file reviewed for this release (2026-10-04). -->
# MystTiq v1.0.0.2 Checkpoint: Unique Player Names

You asked whether player names could be made unique so duplicates can't be used, and chose case-insensitive matching.
This version builds on v1.0.0.1, which hasn't been pushed yet either.

## What it does

- **Each name belongs to one account:** the first account seen using it, or the account you reserve it for. Upper and
  lower case count as the same name (`Wade` = `wade` = `WADE`). Spaces at the ends and doubled spaces are ignored too.
  Look-alike characters (`W4de`, a Cyrillic "а") still count as different names, as you chose.
- **A player who joins with someone else's name** is kicked within one poll (about 5 seconds) and told the name is taken.
  You get a Warning notification and an activity entry, and the Players card lists them under "Turned away recently".
  You can switch the kick off and only be told.
- **Your existing players keep their names.** The first time v1.0.0.2 runs, each name in the player list goes to the
  account that used it first. On this machine that gives Wade, WadeeRROR to Wade's account and Melly, M3llyM to Melly's.
  There are no conflicts.
- **Players > Unique player names** (admins): on/off, kick or report only, reserve a name for a Steam ID, block a name
  for everyone (leave the Steam ID empty), release a name, and save.

## Fixed on the way

- **Save buttons stuck greyed out.** While checking the new card live, **Save names** and **Save Whitelist** were greyed out
  and did nothing. That has been the case for a while, not just in this version: when MystTiq connected while busy, every admin-only button was
  disabled and only some were enabled again afterwards. Now all of them are. The harness reproduces it and fails without
  the fix.

## Limits

- Palworld only reports a name once the player is in the world, so the turned-away player already has a character with
  that name. Delete it on Players and they can make one with another name.
- Like the whitelist and the identity guard, names are only checked while MystTiq is polling the server (the desktop is
  open, also from the tray, or the service runs).

## Verification

- **Full gate** `scripts\Test-v1.0.0.2-Logic.ps1 -RunBuild`: 263 / 263 passed. The 4 Linux VM checks were skipped because 192.168.1.122 could not be reached. Every v1.0.0.1 check is carried, and the frozen v1.0.0.1 gate passes on its own checkpoint.
- **Static gate:** 215 / 215. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **Unique-names smoke** `Test-v1.0.0.2-RouteSmoke.ps1`: 3 / 3. On first use the known players own their names (the earlier account wins, case ignored); a saved list is made consistent; it survives a restart.
- **Logic harness:** the unique-names scenario passes. **ArtworkHarness:** 758 checks pass, including the card, German text, and Save buttons re-enabled after a busy sign-in (this check fails without the fix).
- **Live, on this machine's real data:** the published v1.0.0.2 desktop showed the card with M3llyM, Melly → Melly's account and Wade, WadeeRROR → Wade's. I reserved a test blocked name ("zz Test Block") and saved it (the file showed 5 names), then released it and saved again (back to the 4). Before the fix, Save names did nothing.
- **Not verified live:** a second Steam account joining with a taken name. That needs a second player.

## For you

- **Live check:** start the main server through v1.0.0.2, open Players > Unique player names and check the list. A second
  Steam account joining as `wade` would be turned away.
- **Still open from v1.0.0.1:** apply **Like double-click** on Server > Launcher for the main server and save, then have
  a player join.
- **Pushing:** neither v1.0.0.1 nor v1.0.0.2 has been pushed or tagged. Say when.
