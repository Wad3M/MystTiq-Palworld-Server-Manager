<!-- MystTiq v1.0.8.5: file reviewed for this release (2026-10-12). -->
# MystTiq v1.0.8.5 Checkpoint: The Backups and Save Tools Pages, Tidied

## What changed

The sixth step of the split (C-5) moves the **Backups** page, the save-folder access card and the **Save Tools** page
into their own file. Nothing looks or works differently. The main window's code files are down from 493,942 to 474,431
bytes.

## Verification

- **Full gate** `scripts\Test-v1.0.8.5-Logic.ps1 -RunBuild -RunLong`: 383 / 383 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.8.4 gate passes,
  the long set passes, and the Linux VM checks ran and passed.
- **Static gate:** 319 / 319. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **The split** (ArtworkHarness, 835 checks in 12 languages, every page rendered as before): the backup-scan card's
  checks pass against the new ViewModel (progress, the unreadable archive named, cancel).
- **The role table still matches the code**, with the Backups commands read from their new file.
- **LogicHarness:** 190 scenarios.

## For you

1. **Nothing to test.** This release only moves code.
2. **The 24-hour runs** (v1.0.7.5) finish around 03:00 UTC on 2026-10-13; I'll collect them then.
3. **Roadmap:** C-5 is `Built`. Next is v1.0.8.6: the MOD pages (C-6).
4. **Push:** v1.0.7.0 to v1.0.8.5 are ready to push and tag when you say so.
