<!-- MystTiq v1.0.8.9: file reviewed for this release (2026-10-12). -->
# MystTiq v1.0.8.9 Checkpoint: Security and Fleet, Tidied; the Split Is Done

## What changed

The last step of the split (C-9) moves the **Security** page (API tokens, named user accounts, the browser view's
address) and the **Fleet** page (your servers, Clone World, Backup / Doctor / Update All) into their own files. The main
window keeps only the window itself: tabs, connecting and signing in, the setup wizard and the Settings page that holds
them. Over the ten steps its code files went from 732,039 to 321,400 bytes.

**Two small display bugs found and fixed on the way:**
- The Automation page's "no rules yet" card did not hide or show when the rule list changed (since v1.0.8.7).
- The Workspace page's discovery status could stay stale after an environment check (since v1.0.8.4).

Both came from the split: the main window announced a change that belonged to a page, and the page never heard it. The
page now hears it. A test checks this, and the gate refuses the pattern if it comes back.

## Verification

- **Full gate** `scripts\Test-v1.0.8.9-Logic.ps1 -RunBuild -RunLong`: 392 / 392 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.8.8 gate passes,
  the long set passes, and the Linux VM checks ran and passed.
- **Static gate:** 328 / 328. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **The split** (ArtworkHarness, 836 checks in 12 languages, every page rendered as before): the Security and Fleet pages
  render from the new ViewModels, and the new check passes (adding and clearing an automation rule raises the page's
  "no rules" state twice; a tab change raises the browser view's address on the Security page).
- **The role table still matches the code**, with the Security and Fleet commands read from their new files.
- **LogicHarness:** 190 scenarios.

## For you

1. **One thing you can look at:** open Automation with no rules, add one, and the "no rules yet" card goes away at once
   (and comes back when you delete it). Nothing else looks different.
2. **The 24-hour runs** (v1.0.7.5) finish around 03:00 UTC on 2026-10-13; I'll collect them then.
3. **Roadmap:** C-9 is `Built`, which completes the ViewModel split (C-0 to C-9). No next milestone is scheduled.
4. **Push:** v1.0.7.0 to v1.0.8.9 are ready to push and tag when you say so.
