<!-- MystTiq v1.0.7.1: file reviewed for this release (2026-10-08). -->
# MystTiq v1.0.7.1 Checkpoint: Conflicts Refused, Alert Storms Bounded

The second release from the outside review (roadmap Q-4 and Q-11). Built on v1.0.7.0, which is not pushed yet either.

## What I found first

- **Work that could overlap a start.** I ran the new conflict smoke against the v1.0.7.0 service. A backup was written
  while a server was starting, and a backup and a start sent together both ran. SteamCMD updates and MOD changes only
  checked "is the server running?" and then went ahead.
- **Alert storms.** Every Discord, email and webhook send started on its own, with no limit.

## Fixed in v1.0.7.1

- **One thing at a time.**
  - Scheduled and manual backups hold the world while they zip it. An edit's own safety backup is part of the edit.
  - SteamCMD updates, every MOD change, PalDefender updates and console capture changes hold the server.
  - Whichever comes second is refused with the reason.
  - Scheduled automation waits up to 10 minutes instead of failing, so a backup and a restart at the same minute run
    one after the other.
- **Alert storms.**
  - Sends go through a queue (200), with two senders.
  - The same alert within 10 minutes goes out once, then one message with the count.
  - A full queue drops and records.
  - Alert Center shows what's waiting.
  - Test notifications always go out.
- **One difference from the roadmap text:** Verify All doesn't take the world lock. It only reads backup archives and
  is already kept apart from backups and restores; locking it would only refuse checks during an edit. This is
  written down in the architecture doc.

## Verification

- **Reproduced first:** the new conflict smoke against the published v1.0.7.0 service. A backup was written while a
  server was starting, and a backup and a start sent together both ran. On v1.0.7.1 both are refused or kept apart.
- **Full gate** `scripts\Test-v1.0.7.1-Logic.ps1 -RunBuild`: 347 / 347 passed, 1 skipped (the v1.0.2.0 system-unit
  test: no passwordless sudo on the VM; the per-user unit check covers it). Every earlier check is carried, the frozen
  v1.0.7.0 gate passes on its own checkpoint, and the Linux VM checks ran and passed.
- **Static gate:** 285 / 285. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **Conflict route smoke** (an isolated service, a start that never becomes ready): during the start, a backup, a MOD
  change and a SteamCMD update are each refused with the reason and nothing changes; a backup and a start sent together
  never overlap; alone, a backup is a completed operation in the history.
- **LogicHarness:** 182 scenarios, including:
  - a backup refused during a Pal box edit, while that edit's own safety backup is not;
  - a held backup blocking an edit, and another server never held up;
  - a MOD change refused during a start, while Safe Start's switch is not;
  - a scheduled backup waiting for an edit;
  - the alert storm against a real local webhook: 299 combined, the queue capped at 200 with the rest dropped and
    recorded, never more than 2 sends at once, none lost, the summary, two tests both sent.
- **ArtworkHarness:** 824 checks in 12 languages, including the send queue in Alert Center.

## For you

1. **Nothing to do** for your servers.
2. **Roadmap:** Q-4 and Q-11 are `Built`. Next is v1.0.7.2: Q-8 (start-up steps), Q-12 (backup scan progress), Q-13
   (page waits) and Q-7 (connection status).
3. **Push:** v1.0.7.0 and v1.0.7.1 are both ready to push and tag when you say so.
