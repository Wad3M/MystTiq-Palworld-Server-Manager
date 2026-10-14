<!-- MystTiq v1.0.9.1: file reviewed for this release (2026-10-12). -->
# MystTiq v1.0.9.1 Checkpoint: Discord Commands That Answer

## What changed

**Your Discord bot's long commands now answer.** `/mysttiq-start`, `-stop`, `-restart` and `-backup` take longer than
the 3 seconds Discord allows. The bot used to reply only once the work was done, so Discord showed "The application did
not respond" and the result never appeared, even when the command worked.

Now:
- MystTiq acknowledges at once ("thinking…"), then replaces that with the result.
- A refused command is still answered at once, and only to the person who ran it.
- If a command fails, the channel sees a plain "Command failed". The reason (which can include file paths) goes only to
  whoever ran it, and into the Activity log.
- A start or restart from Discord runs the same MOD check before starting as one from the app.

## Verification

- **Full gate** `scripts\Test-v1.0.9.1-Logic.ps1 -RunBuild -RunLong`: 398 / 398 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.9.0 gate passes,
  the long set passes, and the Linux VM checks ran and passed.
- **Static gate:** 334 / 334. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **B-4:** the four LogicHarness scenarios on the Discord command flow pass (refusal, acknowledge before work, private
  failure reason, expired reply). With the acknowledgement moved after the work (the old order), the acknowledge-first
  scenario fails ("work starts / defer / edit"), so it guards the fix.
- **ArtworkHarness** (836 checks in 12 languages) and **LogicHarness** (every scenario, the four new ones included) pass.

## For you

1. **One live check, when convenient:** in your Discord server, run `/mysttiq-backup`. It should say it's thinking and
   then show "Backup created…". If you'd like, `/mysttiq-restart` too. I can't do this without your bot token, and
   I haven't touched your bot settings.
2. **The 24-hour runs** (v1.0.7.5) finish around 03:00 UTC on 2026-10-13; I'll collect them then.
3. **Next:** v1.0.9.2, MOD load order with profile export and import (B-3).
4. **Push:** v1.0.7.0 to v1.0.9.1 are ready to push and tag when you say so.
