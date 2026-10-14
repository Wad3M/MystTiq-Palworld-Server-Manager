<!-- MystTiq v1.0.8.7: file reviewed for this release (2026-10-12). -->
# MystTiq v1.0.8.7 Checkpoint: Notifications, Alerts, Automation and Activity, Tidied

## What changed

The eighth step of the split (C-7) moves **Notifications**, the **Alert Center**, **Automation** and **Activity**, with
delivery and the Discord bot, into their own file. Nothing looks or works differently. The main window's code files are
down from 404,700 to 375,111 bytes.

One slip was caught on the way: the tool that moves the code renamed a word inside a piece of text, so the
Notifications ribbon group was briefly titled "Alerts.Notifications". The tests caught it, it is fixed, the tool no
longer touches text, and a check of all the code found no other case.

## Verification

- **Full gate** `scripts\Test-v1.0.8.7-Logic.ps1 -RunBuild -RunLong`: 387 / 387 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.8.6 gate passes,
  the long set passes, and the Linux VM checks ran and passed.
- **Static gate:** 323 / 323. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **The split** (ArtworkHarness, 835 checks in 12 languages, every page rendered as before): the notification, delivery
  and Alert Center checks pass against the new ViewModel, and every page's Ribbon text is identical to before (the
  check that caught the "Alerts.Notifications" title).
- **The role table still matches the code**, with the Alerts commands read from their new file.
- **LogicHarness:** 190 scenarios.

## For you

1. **Nothing to test.** This release only moves code.
2. **The 24-hour runs** (v1.0.7.5) finish around 03:00 UTC on 2026-10-13; I'll collect them then.
3. **Roadmap:** C-7 is `Built`. Next is v1.0.8.8: the Update Center, Doctor, Crash Analyzer and Diagnostics (C-8).
4. **Push:** v1.0.7.0 to v1.0.8.7 are ready to push and tag when you say so.
