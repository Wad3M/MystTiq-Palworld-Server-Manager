<!-- MystTiq v1.0.7.5: file reviewed for this release (2026-10-11). -->
# MystTiq v1.0.7.5 Checkpoint: Under Load, and Over Time

## What the tests found, and what changed

- **An update stopped half way was called installed.** If SteamCMD or MystTiq stopped during an update, MystTiq still
  said Installed, because PalServer.exe was there, and would start the half-updated server. Now it shows **Incomplete —
  Update required** and refuses the start until Update has run again.
- **Closing the desktop during an update stopped it half way.** The update now runs to its end on the service.
- **A backup failed at once if a save file was in use** (for example while the server wrote it). It now waits up to 10
  seconds, then names the file and the program holding it.

## Verification

- **Full gate** `scripts\Test-v1.0.7.5-Logic.ps1 -RunBuild -RunLong`: 365 / 365 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.7.4 gate passes, and
  the Linux VM checks ran and passed.
- **Static gate:** 301 / 301. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **The long set** (in the full gate):
  - an update interrupted half way is reported Incomplete and not started, and a finished one clears it;
  - 36 simultaneous requests: 10 answered 200 and 26 refused 409, with no server error and no half-written backup;
  - a backup waits for a save file held 2.5 s, and names `Level.sav` when it stays held;
  - 10,000 console lines: the slowest poll took 329 ms;
  - the service stopped in 1.9 s with exit code 0, its port and files free, and the next one found the server still
    running;
  - a 10-minute idle soak: memory flat, 0 of 275 status requests failed, a clean stop.
- **Rapid tab switching** (ArtworkHarness): 300 switches with late replies; each tab shows only its own data.
- **3-minute soak trials:** passed on Windows and on the Linux VM (a per-user unit).
- **LogicHarness:** 190 scenarios. **ArtworkHarness:** 835 checks in 12 languages.

## For you

1. **Nothing to do for this release** beyond updating the service when you like. If an update is ever interrupted, the
   Dashboard will now say so; run Update again.
2. **The 24-hour runs** are going on this computer and on the Linux VM (as your user, on a test port; your
   mysttiq-palworld service is untouched). I'll collect them after 2026-10-13 03:00 UTC. Don't reboot the VM before
   then, if you can avoid it.
3. **Roadmap:** Q-10 is `Built`. Every quality and safety item from the outside review (Q-1 to Q-16) is now built. Next
   is v1.0.8.0: the start of splitting the main ViewModel (C-0, the foundation, and the Console page as the pilot).
4. **Push:** v1.0.7.0 to v1.0.7.5 are ready to push and tag when you say so.
