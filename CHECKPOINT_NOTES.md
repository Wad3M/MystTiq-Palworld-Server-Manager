<!-- MystTiq v1.0.8.4: file reviewed for this release (2026-10-12). -->
# MystTiq v1.0.8.4 Checkpoint: The Configuration and Workspace Pages, Tidied

## What changed

The fifth step of the split (C-4) gives two more pages their own files:
- **Configuration**: the Palworld settings editor, MystTiq's own settings, and the Launcher's start options.
- **Workspace**: the folders and the active world.

Nothing looks or works differently. The main window's code files are down from 569,569 to 493,942 bytes.

**The setup wizard stays with the main window.** It creates the server's profile and its tab, which is the main window's
own job (the roadmap's last step leaves "the window shell and tab coordination" there). Splitting off only its form
fields would have spread one flow across two places.

## Verification

- **Full gate** `scripts\Test-v1.0.8.4-Logic.ps1 -RunBuild -RunLong`: 381 / 381 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.8.3 gate passes,
  the long set passes, and the Linux VM checks ran and passed.
- **Static gate:** 317 / 317. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **The split** (ArtworkHarness, 835 checks in 12 languages, every page rendered as before): the Configuration page's
  and the Launcher's checks pass against the new ViewModel (no preset adds a log option, a switched-on one warns, a
  profile with no saved launcher settings loads with them off).
- **The role table still matches the code**, with the Configuration commands read from their new file.
- **LogicHarness:** 190 scenarios.

## For you

1. **Nothing to test.** This release only moves code.
2. **The 24-hour runs** (v1.0.7.5) finish around 03:00 UTC on 2026-10-13; I'll collect them then.
3. **Roadmap:** C-4 is `Built`, with the wizard noted as staying. Next is v1.0.8.5: Backups and Save Tools (C-5).
4. **Push:** v1.0.7.0 to v1.0.8.4 are ready to push and tag when you say so.
