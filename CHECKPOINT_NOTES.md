<!-- MystTiq v1.1.1.0: file reviewed for this release (2026-10-14). -->
# MystTiq v1.1.1.0 Checkpoint: MOD Sources, Player Checks Without the Desktop, Linux and Docker Downloads

## What changed

**MOD Sources**, as you asked: a new item under MODs with the Steam Workshop card, the MOD browser and the Nexus Mods
catalog. MOD Library keeps the ZIP install, the load order and the installed MODs. Nexus links open MOD Sources.

**The nine fixes**
1. The player checks (join check, identity guard, moves, player list, whitelist, names, kits, temporary bans) run in the
   service every 15 seconds when no desktop is open.
2. A player on the new-character screen is disconnected once with the steps: their character is safe, make one with any
   name, MystTiq moves theirs onto it.
3. Xbox and other non-Steam accounts are covered (their character is found from the player list).
4. Players > Player IDs can link an account to its character by hand.
5. The Dashboard tells a player on this PC to join with a local address, not 127.0.0.1.
6. The Linux checks' SSH calls keep the session alive (no more two-hour hangs).
7. The checkpoint script is part of the project: `scripts\New-MystTiqCheckpoint.ps1`.
8. The checkpoint README mismatch goes with it.
9. The Like double-click preset is gone from Server > Launcher.

**Faster tab switching** (the task you asked me to do here): switching server tabs re-applied the colour theme every time
and froze the desktop for about 2 seconds per open window. An unchanged theme is now left alone: 3.7 s to under 0.1 s.

**Linux and Docker downloads**, as you asked: every release now carries the Linux ZIP and the headless service's Docker
image (`...Docker-linux-x64.tar.gz`), built by the release workflow; this checkpoint holds both.

## Verification

- **Full gate** `scripts\Test-v1.1.1.0-Logic.ps1 -RunBuild -RunLong`: 419 / 419 passed, 6 skipped. The skips are the
  Linux VM checks: the VM (192.168.1.122) was off the network all afternoon (no ping, no SSH), so the SSH keep-alive
  (fix 6) is checked in the scripts but not yet live. Every earlier check is carried (pinned where code moved), the frozen
  v1.1.0.3 gate passes, the long set passes, and the ArtworkHarness passes.
- **LogicHarness:** player checks without a desktop, every account (Xbox from the player list, links by hand), the
  new-character notice (once, let through, again after 30 minutes), the identity alert's advice; all scenarios pass.
- **ArtworkHarness:** MOD Sources and MOD Library cards, the Nexus link opening MOD Sources, no Like double-click preset,
  and the tab switch: an unchanged theme is left alone (measured on a copy: 3,658 ms to 14 ms per switch).
- **Packages:** Windows ZIP, Linux ZIP and the Docker image archive (run here: `/healthz` answered v1.1.1.0 with TLS and
  sign-in on), all in `SHA256SUMS.txt`. **Static gate:** passed. **Validate-Release -Strict:** 0 errors, 0 warnings.
  **Distribution check:** passed.
