<!-- MystTiq v1.0.7.3: file reviewed for this release (2026-10-11). -->
# MystTiq v1.0.7.3 Checkpoint: Joining Again, and a Lighter Console

## Your report: "unable to connect"

It wasn't a connection problem. On 8 October at 6:12 PM you connected, and MystTiq's identity guard kicked you 8
seconds later, while your character was still loading. While you load, the server lists you with no character, and the
guard took that for the new-character screen. Then it happened again on your second try.

The server's own logs show it: PalDefender has you connecting at 18:12:17, and AdminCommands never wrote its usual
"Wade [67D8D355] has connected" line.

Your character was never touched. This had been possible since v1.0.0.1.

## Fixed in v1.0.7.3

- **The guard waits for a loading player.**
  - A player with no character yet gets 90 seconds to finish loading.
  - A player still without one after that is kicked once.
  - A player given a different character is still kicked at once.
- **A lighter console (Q-6).** It only redraws when there are new lines (an unchanged poll used to rebuild about 1,000
  rows), and a line's time no longer changes on every refresh.

## Verification

- **Full gate** `scripts\Test-v1.0.7.3-Logic.ps1 -RunBuild`: 356 / 356 passed, 1 skipped (the v1.0.2.0 system-unit
  test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.7.2 gate passes, and the Linux
  VM checks ran and passed.
- **Static gate:** 293 / 293. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **The guard fix, replayed** (LogicHarness): your join, listed with no character at 8 s and 38 s and then with your own
  character, gives no kick and nothing recorded. A rejoin still without a character after 91 s is kicked once; a
  different character is kicked at once.
- **The console, measured** (ArtworkHarness):
  - an unchanged poll touches the lists 0 times (it was about 1,000), and a changed one twice;
  - 10,000 lines in 20 polls took 77 ms, with the list held at 500;
  - a line keeps the time it was first seen;
  - the Console's filtered view waits while the page is hidden.
- **LogicHarness:** 186 scenarios. **ArtworkHarness:** 835 checks in 12 languages.

## For you

1. **To test:** update the service (the Dashboard offers it) and join the main server with the guard's kick on. You
   should stay in. If you turned the kick off, turn it back on.
2. **Roadmap:** Q-6 and Q-16 are `Built`. Next is v1.0.7.4: Q-14 (encrypted secrets), Q-15 (archive limits) and Q-9
   (secrets kept out of logs).
3. **Push:** v1.0.7.0 to v1.0.7.3 are ready to push and tag when you say so.
