<!-- MystTiq v1.0.8.2: file reviewed for this release (2026-10-12). -->
# MystTiq v1.0.8.2 Checkpoint: The Players Page, Tidied

## What changed

The third step of the split (C-2): the **Players** page now lives in its own file, `PlayersViewModel`. That covers the
player list and filters, notes and warnings, kick and ban, temporary bans, the ban list, the whitelist, unique names,
starter kits, Give Item, and the inventory and Pal box. Nothing looks or works differently. The main window's code files
are down from 704,238 to 644,530 bytes.

One slip was caught and fixed on the way: after a busy spell, Save names and Save Whitelist stayed greyed out, because
the "busy finished" refresh only knew the main window's own buttons. It now covers every page's.

## Verification

- **Full gate** `scripts\Test-v1.0.8.2-Logic.ps1 -RunBuild -RunLong`: 375 / 375 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried (they now read the window's code
  wherever it lives), the frozen v1.0.8.1 gate passes, the long set passes, and the Linux VM checks ran and passed.
- **Static gate:** 311 / 311. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **The split** (ArtworkHarness, 835 checks in 12 languages, every page rendered as before): the Players page's checks
  pass against the new ViewModel (the Unique player names card, reservations, the whitelist, Save while busy).
- **The role table still matches the code**, with the Players commands read from their new file.
- **LogicHarness:** 190 scenarios.

## For you

1. **Nothing to test.** This release only moves code.
2. **The 24-hour runs** (v1.0.7.5) finish around 03:00 UTC on 2026-10-13; I'll collect them then.
3. **Roadmap:** C-2 is `Built`. Next is v1.0.8.3: the World pages move out (C-3).
4. **Push:** v1.0.7.0 to v1.0.8.2 are ready to push and tag when you say so.
