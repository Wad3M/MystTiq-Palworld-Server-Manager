<!-- MystTiq v1.0.8.8: file reviewed for this release (2026-10-12). -->
# MystTiq v1.0.8.8 Checkpoint: The Update Center, Doctor and Diagnostics, Tidied

## What changed

The ninth step of the split (C-8) moves the **Update Center**, **Server Doctor**, the **Crash Analyzer** and the
**Diagnostics Center** (network, internet reachability, firewall, UPnP, this computer's checks, anti-cheat and console
capture) into their own file. Nothing looks or works differently. The main window's code files are down from 375,111
to 339,118 bytes.

A few pieces stay with the main window and are handed to the new page: updating the server files and pip (they belong
to the HOST page's install), the list of every Palworld running on this computer, and opening the UE4SS page.

## Verification

- **Full gate** `scripts\Test-v1.0.8.8-Logic.ps1 -RunBuild -RunLong`: 389 / 389 passed, 1 skipped (the v1.0.2.0
  system-unit test: no passwordless sudo on the VM). Every earlier check is carried, the frozen v1.0.8.7 gate passes,
  the long set passes, and the Linux VM checks ran and passed.
- **Static gate:** 325 / 325. **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
- **The split** (ArtworkHarness, 835 checks in 12 languages, every page rendered as before): the Update Center's rows
  (Update on every row, UE4SS's opening the UE4SS page), Server Doctor's focused finding and the Diagnostics checks pass
  against the new ViewModel.
- **The role table still matches the code**, with the Diagnostics commands read from their new file.
- **LogicHarness:** 190 scenarios.

## For you

1. **Nothing to test.** This release only moves code.
2. **The 24-hour runs** (v1.0.7.5) finish around 03:00 UTC on 2026-10-13; I'll collect them then.
3. **Roadmap:** C-8 is `Built`. Next is v1.0.8.9, the last step: Settings, Security, Fleet and the browser view (C-9).
4. **Push:** v1.0.7.0 to v1.0.8.8 are ready to push and tag when you say so.
