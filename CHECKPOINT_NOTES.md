<!-- MystTiq v1.0.8.0: file reviewed for this release (2026-10-11). -->
# MystTiq v1.0.8.0 Checkpoint: A Cleaner Inside, and Ctrl+C Fixed

## Fixed

- **Ctrl+C stopped the game too.** If you ran `mysttiq-server api-run` in a terminal, Ctrl+C stopped your game server
  along with MystTiq. Now it stops MystTiq only, and the next MystTiq finds the server still running.
- **From Git Bash, Ctrl+C did nothing.** It now stops the service cleanly.

Your installed service and the desktop's built-in service were never affected.

## The split begins (C-0)

- **The groundwork:** a shared "session" that each page uses, and a common base for page ViewModels.
- **The first page moved:** the Console page now lives in its own file (`ConsoleLogViewModel`), out of the main window's
  code. Nothing looks or works differently.
- **The main window's code:** 619,028 → 609,714 bytes.

## Verification

- **Full gate** `scripts\Test-v1.0.8.0-Logic.ps1 -RunBuild -RunLong`: 369 / 369 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried (the console and command contracts
  now read the new files), the frozen v1.0.7.5 gate passes, and the Linux VM checks ran and passed.
- **Static gate:** 305 / 305. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **The Ctrl+C fix**, in the long set: the service stopped on Ctrl+C (exit 0) and the game server kept running. The
  same check against v1.0.7.5 fails ("the Ctrl+C stopped the game server too"). Run from Git Bash, the service now
  stops on Ctrl+C too.
- **The rest of the long set** still passes, including a 10-minute idle soak with 0 of 271 status requests failed and a
  clean stop.
- **The split** (ArtworkHarness, 835 checks in 12 languages, every page rendered as before):
  - the console checks pass against the new Console ViewModel (an unchanged poll touches nothing, 10,000 lines in 106 ms,
    the hidden filter);
  - rapid tab switching passes.
- **LogicHarness:** 190 scenarios.

## For you

1. **Nothing to test** unless you run the service yourself in a terminal; then Ctrl+C now leaves the game running.
2. **The 24-hour runs** (v1.0.7.5) are still going on this computer and on the VM. I'll collect them after 2026-10-13
   03:00 UTC.
3. **Roadmap:** C-0 is `Built`. Next is v1.0.8.1: the Dashboard, HOST, addresses and bandwidth move out (C-1).
4. **Push:** v1.0.7.0 to v1.0.8.0 are ready to push and tag when you say so.
