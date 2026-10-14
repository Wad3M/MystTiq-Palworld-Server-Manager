<!-- MystTiq v1.0.8.6: file reviewed for this release (2026-10-12). -->
# MystTiq v1.0.8.6 Checkpoint: The MOD Pages, Tidied

## What changed

The seventh step of the split (C-6) moves the **MOD** pages into their own file: the dashboard and library, UE4SS,
Safe Start, the Workshop scan, Nexus Mods and the MOD browser. Nothing looks or works differently. The main window's
code files are down from 474,431 to 404,700 bytes.

One ordering slip was caught on the way: the pages were being set up before the main window's own services, so the MOD
page would have started without its Nexus client. The services are now set up first.

## Verification

- **Full gate** `scripts\Test-v1.0.8.6-Logic.ps1 -RunBuild -RunLong`: 385 / 385 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.8.5 gate passes,
  the long set passes, and the Linux VM checks ran and passed.
- **Static gate:** 321 / 321. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **The split** (ArtworkHarness, 835 checks in 12 languages, every page rendered as before): the MOD pages' checks pass
  against the new ViewModel (the drop zone, the MOD browser against stand-in repositories, the nxm:// handoff).
- **The role table still matches the code**, with the MOD commands read from their new file.
- **LogicHarness:** 190 scenarios.

## For you

1. **Nothing to test.** This release only moves code.
2. **The 24-hour runs** (v1.0.7.5) finish around 03:00 UTC on 2026-10-13; I'll collect them then.
3. **Roadmap:** C-6 is `Built`. Next is v1.0.8.7: Automation, Notifications, Alert Center and Activity (C-7).
4. **Push:** v1.0.7.0 to v1.0.8.6 are ready to push and tag when you say so.
