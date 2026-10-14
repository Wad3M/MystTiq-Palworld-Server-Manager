<!-- MystTiq v1.0.9.0: file reviewed for this release (2026-10-12). -->
# MystTiq v1.0.9.0 Checkpoint: Every Command Reachable; the Backlog Scheduled

## What changed

A tidy-up after the code split:
- **Three leftover commands removed.** No button, Ribbon entry or code could run them: a world-map toggle and the setup
  wizard's former Local / Remote choice. Nothing you can click changes.
- **A new test keeps every command reachable.** It checks all 251 of the desktop's commands are used somewhere beyond
  their own definition. I tried it both ways: it passes now, and it names the command when an unused one is added.

The roadmap now holds your decision (D-11):
- v1.0.9.1: Discord control commands.
- v1.0.9.2: MOD load order, with profile export and import.
- v1.0.9.3: level-gap scans.
- v1.1.0.0: the documentation pass, a release, and that release as the new accepted baseline.

One finding while planning: the Discord bot already has start, stop, restart, save, backup, broadcast, kick and ban
commands with role checks (since v0.6.17.0). So v1.0.9.1 fixes what is missing. Long commands such as a restart never
answer Discord in time, so Discord shows "did not respond" even when the command worked.

## Verification

- **Full gate** `scripts\Test-v1.0.9.0-Logic.ps1 -RunBuild -RunLong`: 395 / 395 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.8.9 gate passes,
  the long set passes, and the Linux VM checks ran and passed.
- **Static gate:** 331 / 331. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **T-1:** the reachability check passes on all 251 desktop commands; run with an unused command added, it fails and
  names it.
- **ArtworkHarness** (836 checks in 12 languages) and **LogicHarness** (190 scenarios) pass.

## For you

1. **Nothing to test.** No visible change.
2. **The 24-hour runs** (v1.0.7.5) finish around 03:00 UTC on 2026-10-13; I'll collect them then.
3. **Push:** v1.0.7.0 to v1.0.9.0 are ready to push and tag when you say so.
