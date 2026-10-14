<!-- MystTiq v1.1.0.1: file reviewed for this release (2026-10-13). -->
# MystTiq v1.1.0.1 Checkpoint: Start-up Notice Fix

## What changed

**Fixed: "Something went wrong (background task)" after the desktop started** (roadmap F-1, reported 2026-10-13).
Every desktop crash record on your PC (five by the end of the day, v1.0.7.0 to v1.1.0.0) was the same thing: the
start-up check for a port in use gave up on its connection attempt after 0.75 s and abandoned it, while Windows takes
about 2 s to refuse a closed local port. The abandoned attempt failed later with nothing waiting for it, and the desktop
reported it. Nothing stopped and no data was affected. The check now cancels its attempt at the limit and waits for it.

v1.1.0.0 stays the accepted baseline; this is a fix on top of it.

## Verification

- **Full gate** `scripts\Test-v1.1.0.1-Logic.ps1 -RunBuild -RunLong`: 408 of 409 run passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). The one failure, the Linux desktop-session check, was its SSH probe
  to the VM hanging for two hours on a dead connection (the VM answered SSH instantly when checked); run again on its
  own, `scripts\Test-v0.8.26.0-LinuxDesktopSession.ps1` passed (16 checks, the real window on the VM's XFCE desktop).
  The frozen v1.1.0.0 gate passes, the long set passes, and the other Linux VM checks ran and passed.
- **F-1:** the ArtworkHarness checks the start-up port check on a closed and a listening port and counts socket errors
  left unobserved after a collection: none. The v1.1.0.0 method, run the same way in a standalone program, left 3 from
  3 closed-port checks; the new one none. A gate check rejects any connect in the source that is waited on and abandoned.
- **Static gate:** 344 / 344. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.

## For you

1. **Pushes:** v1.0.7.0 to v1.1.0.1 are committed locally only; they need your yes to push and tag.
2. **Live checks only you can do:** run `/mysttiq-backup` from your Discord bot; leave the level-gap rule on for a week.
