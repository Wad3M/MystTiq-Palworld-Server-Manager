<!-- MystTiq v1.0.0.4: file reviewed for this release (2026-10-05). -->
# MystTiq v1.0.0.4 Checkpoint: Restores You Can Check by the Day

You reported that backup and restore didn't work correctly, and asked me to verify it using the in-game day after a restore.

## What I found, by day

I read the day straight from each `Level.sav` (not from MystTiq's copy):

| | Day |
|---|---|
| Your live world (`Level.sav`, Oct 4) | **Day 248 21:08** |
| What MystTiq's Dashboard showed | Day 210 16:49, from a copy decoded on Oct 1 |
| Your 36 backups, Aug 25 → Oct 1 | each holds its own correct day, 109 → 211 |

The backups themselves are good. Two things made restore look broken:

1. **Every restore on Oct 1 failed, because Windows wouldn't let MystTiq replace the save folder.** Your `SaveGames`
   folder (and the decoded copy in it) belong to the **Administrators** group: something that once ran as administrator
   created it. MystTiq runs as you without administrator rights, so Windows lets it read and add files there but not
   move or replace the folder. The error said "Access to the path …\SaveGames is denied", which looked like a file in
   use. Nothing was changed, and none of it reached the activity log.
2. **The day shown was stale.** The Dashboard read it from a decoded copy MystTiq only refreshed after its own world
   edits, and couldn't refresh anyway for the same permission reason. Every backup since Oct 1 carries that old copy.

(On Oct 1 the world also went from Day 211 at 07:57 to Day 173 at 10:24. That looks like the Sep 2 backup copied in by
hand, since no MystTiq restore succeeded.)

## What's fixed

- **Backups page → Fix Save Folder Access:** an amber card appears when restores can't replace the save folder. The
  button gives your Windows account change rights on the server's `Pal\Saved` folder. Windows shows its administrator
  prompt first. **You** click it; I didn't change any permissions.
- **Restores report the day:** "Backup restored: … The world is now Day 211 18:48, as in the backup." It's read from the
  restored `Level.sav` and compared with the backup's day.
- **Each backup shows its world day** in the Backups list. These are already read for all 36 of yours.
- **The Dashboard's day is current:** re-read whenever the world has been saved. If the copy next to the world can't be
  written, MystTiq keeps its own. Until then it says it's showing an older save instead of "exact".
- **Restore waits** up to 5 seconds for a briefly held file, names a program that keeps one open, refuses while a
  PalServer started outside MystTiq is running, and logs every outcome.

## Verification

- **Full gate** `scripts\Test-v1.0.0.4-Logic.ps1 -RunBuild`: 276 / 276 passed. The 4 Linux VM checks were skipped because 192.168.1.122 could not be reached. Every v1.0.0.3 check is carried, and the frozen v1.0.0.3 gate passes on its own checkpoint.
- **Static gate:** 226 / 226. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **Restore smoke** `Test-v1.0.0.4-RouteSmoke.ps1`: 7 / 7, run on copies of your real backups in an isolated server:
  1. every backup's day is read;
  2. a restore gives the backup's day and the Dashboard shows it as current;
  3. a held file is named, with nothing changed, and a brief hold is waited out;
  4. a PalServer started outside MystTiq blocks a restore;
  5. a save folder MystTiq may not replace (your permissions, reproduced) is reported and blocks the restore;
  6. a newer save is read again through MystTiq's own copy when the one beside it is read-only;
  7. the activity log has every outcome.
- **Logic harness:** the world-clock scenario passes. **ArtworkHarness:** 764 checks pass, including the World day column, the German restore message and the fix command's exact text.
- **Live on your data (read-only):** the Dashboard shows **Day 248 • 21:08**, matching what I read straight from `Level.sav`. The Backups list shows each backup's day (for example Day 211 • 18:48 for Oct 1 14:58), and the amber **Fix Save Folder Access** card shows `C:\GameServers\Palworld\Server\Pal\Saved`.
- **Not done by me:** clicking Fix Save Folder Access (it changes Windows permissions; that's yours), a restore of your live world, and checking the day in the game.

## For you

1. Open **Backups**, click **Fix Save Folder Access**, and say Yes to Windows. The card should go away.
2. Restore a backup (or try it on a cloned server first). MystTiq reports the day; start the server and check that day in
   the game.
3. The Dashboard should now show Day 248 (or later), not Day 210.

- **Next:** v1.0.0.5 (separate-looking Bases and Guilds), then v1.0.1.0 (an Update button on every Update Center row,
  and the first signed release).
- **Pushing:** v1.0.0.3 and v1.0.0.4 haven't been pushed. Say when.
