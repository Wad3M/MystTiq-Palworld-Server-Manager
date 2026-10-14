<!-- MystTiq v1.0.7.2: file reviewed for this release (2026-10-11). -->
# MystTiq v1.0.7.2 Checkpoint: Progress You Can See

The third release from the outside review (roadmap Q-8, Q-12, Q-13 and Q-7). Built on v1.0.7.1; none of v1.0.7.0,
v1.0.7.1 or v1.0.7.2 is pushed yet.

## What I found first

- Against the v1.0.7.1 service, a start showed no step and the backup list had no progress at all.
- The Dashboard said "Connection failed" for every kind of connection problem.
- Map and Players polled for up to a minute with nothing on screen.

## Fixed in v1.0.7.2

- **Start-up steps.** The Dashboard's server tile shows the step (checking settings, launching, waiting for the game
  port) and how long. A failed start says where it stopped.
- **Backup scans.** Verify All and the world-day reading show "12 of 47", list archives they can't read, and have
  Cancel. Verify All keeps what it had done. World days are saved every 10 backups.
- **Page waits.** Map and Players say at once what they're waiting for, load when it ends, and stop waiting if you
  leave.
- **Connection line.** It tells apart these, each with what to do:
  - the service unreachable;
  - reconnecting;
  - a rejected sign-in;
  - a certificate mismatch;
  - a service error;
  - Palworld running with its REST API not answering.
- **One difference from the roadmap text:** there's no "checking REST" start step. Readiness is the game port. A REST
  API that isn't answering is shown by the connection line and handled by the frozen-server watchdog. This is written
  down in the architecture doc.

## Verification

- **Reproduced first:** the new progress smoke against the published v1.0.7.1 service. The status had no start step and
  the backup list had no scan; both checks failed. On v1.0.7.2 both pass.
- **Full gate** `scripts\Test-v1.0.7.2-Logic.ps1 -RunBuild`: 352 / 353 passed, 1 skipped (the v1.0.2.0 system-unit
  test: no passwordless sudo on the VM). The one failure was the remote sign-in check: its first SSH command to the VM
  stalled for 34 minutes (the VM itself answered normally), and I stopped it. Rerun on its own right after, it passed:
  four roles, Windows and Linux Desktop, 42 checks. Every earlier check is carried, the frozen v1.0.7.1 gate passes,
  and the other Linux VM checks ran and passed.
- **Static gate:** 290 / 290. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **Progress route smoke** (an isolated service): a stuck start shows "Waiting for the game port" with its times, and
  the timed-out start names that step; Verify All over 61 backups reports N of M, the cancel route stops it keeping the
  ones done, and a full run names the broken archive.
- **LogicHarness:** 185 scenarios, including the start's steps, every connection cause, and Verify All with a broken
  archive and Cancel.
- **ArtworkHarness:** 831 checks in 12 languages, including the start step on the Dashboard, the backup scan card, the
  page-wait banner (shown at once, loaded on the signal, stopped on leaving) and the connection cause line.

## For you

1. **Nothing to do** for your servers.
2. **Roadmap:** Q-13 is `Done`; Q-8, Q-12 and Q-7 are `Built`. Each still lacks one live piece of evidence:
   - Q-8: a live start of the clone;
   - Q-12: world days read from a real backup set;
   - Q-7: a live disconnect and reconnect.
3. **Next:** v1.0.7.3 (Q-6, a lighter console refresh).
4. **Push:** v1.0.7.0, v1.0.7.1 and v1.0.7.2 are ready to push and tag when you say so.
