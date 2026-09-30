<!-- MystTiq v0.9.10.0: file reviewed for this release (2026-09-30). -->
# MystTiq v0.9.10.0 Checkpoint: Review Fixes

The accepted baseline stays **v0.8.25.0** until you accept this one.

This version answers the outside review of v0.9.9.0 (`MystTiq_v0.9.9.0_Review.zip`). Full detail:
`docs/architecture/v0.9.10.0-review-fixes.md`.

## The six findings

- **F1, a slow helper was forgotten.**
  - What was wrong: if the helper MystTiq started didn't answer within 750 ms, MystTiq forgot it and started a second
    one beside it.
  - Now: a helper that is still running gets up to 8 seconds to answer (4 s per try) and is reused. One that can't be
    used is replaced only after it has exited. If it won't stop, MystTiq says so and starts nothing.
  - Proof: the ArtworkHarness starts stand-in helpers that answer after 2 s, or never.
- **F2, names were translated.**
  - What was wrong: a server named "Ready" showed as "Bereit".
  - Now: names in quotes, or after "server", "player", "guild" and similar words, are kept as written, and the
    Dashboard shows the server name and description untranslated.
  - Proof: checked with "Ready", "None" and "Backup".
  - Remaining: a name in an unquoted slot that doesn't follow one of those words can still collide.
- **F3, unknown MOD state counted as "up to date".**
  - Now: each MOD records whether it was really checked. The MOD update alert clears only when every MOD it named was
    checked and is current, or was removed.
- **F4, the update check missed MystTiq's own releases.**
  - What was wrong: GitHub's "latest release" skips prereleases, and every MystTiq 0.x release is one.
  - Now: the check reads the release list. While MystTiq is 0.x, prereleases count; from 1.0 on, only stable releases do.
- **F5, crash evidence expired.**
  - What was wrong: the join-crash finding appeared at one minute and was gone at ten.
  - Now: it no longer depends on when it is read, so it is recorded once and stays in the history. Its title no longer
    claims timing: "Server session ended on a player joining".
- **F6, the source ZIP failed its own gate.**
  - What was wrong: `CHECKPOINT_NOTES.md` was copied in after the files were stamped.
  - Now: these notes carry the stamp, a contract checks it, and the source ZIP is checked by its own gate after it is
    made (below).

Also:

- The Doctor's port finding now allows for a router that forwards another outside port on purpose.
- From the review's translation notes:
  - Level, Gender and Nickname labels in all 11 languages;
  - no more "MOD(s)" plurals;
  - German "Stopp erzwingen" and "Änderungen zurücksetzen";
  - "this app" instead of "this desktop".

## Verification

- **Full gate:** 240/240 (`gate9100-full.txt`).
  - It includes the frozen v0.9.9.0 gate, whose one known archive failure (F6) is set aside by its exact text.
  - Four Linux VM checks were skipped: the VM at 192.168.1.122 didn't answer SSH (connection timed out), so nothing
    was checked on Linux this time.
- **Static gate:** 194/194. **Clean and strict validation:** 0 errors, 0 warnings. **Distribution check:** 4/4.
- **Both harnesses:**
  - Logic harness: every scenario passed.
  - ArtworkHarness: 740 checks passed, including the helpers that answer after 2 s or never, and "Ready", "None" and
    "Backup" kept as names.
- **Windows, live:** v0.9.10.0 started one helper (its record now has a start time). After closing and reopening the
  app, it reused that same helper.
- **The source ZIP checked on its own:** after this checkpoint was made, its FullSource ZIP was extracted into an empty folder and its own gate run there. The result is `archive9100-gate.txt`, next to the ZIP (it can't be inside the ZIP it checks).
- Your clone server had already stopped cleanly at 12:34 today, before the review arrived. I didn't restart it.

## For you

- **The review's other notes:**
  - The README's "What's new" now shows only this release, with earlier ones folded away.
  - The known limitations now separate what was verified in game on 2026-09-28 from what is still open.
  - English keeping your system's number format is documented as intended.
- **Not done by me:**
  - a native-speaker review (the reviewer also says theirs wasn't one);
  - a check of the port finding on a real NAT setup.
- **Still yours to do:**
  - click **Allow through Firewall** once;
  - native review of the translations, and a screen-reader pass;
  - the Discord and email delivery checks;
  - decide whether to update your main server and what to do with the Frostbound profiles;
  - push and tag v0.9.0.0 through v0.9.10.0.
