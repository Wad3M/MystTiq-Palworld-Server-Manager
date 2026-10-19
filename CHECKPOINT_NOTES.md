<!-- MystTiq v1.1.1.1: file reviewed for this release (2026-10-18). -->
# MystTiq v1.1.1.1 Checkpoint: Packages Without createdump

## What changed

**The downloads no longer include .NET's crash-dump helper**, as you asked after the VirusTotal report. A self-contained
.NET app ships `createdump.exe` beside it; MystTiq never uses it, but the sandbox ran it instead of MystTiq, so the
report showed only that helper and the sandbox's own Python (matched to the vcruntime140 side-loading rule). It is now
left out of every publish (`Directory.Build.targets`), so the Windows ZIP, the Linux ZIP and the Docker image hold only
MystTiq's own programs, and the packaging refuses a build that contains it. MystTiq itself runs exactly as before.

**New README screenshots** (Dashboard, Players, Backups, MOD Library) from this version, rendered from the real window
against a demo copy of your world (no addresses shown).

**Found while taking them (queued as F-16):** your Players, Guilds and Map pages read a decoded copy of the world from
10-01 that sits beside `Level.sav` (MystTiq may not write there), so they still show the old player IDs. The fix is to
use MystTiq's own current copy when that file is older; it is on the roadmap as the next item.

## Verification

- **Full gate** `scripts\Test-v1.1.1.1-Logic.ps1 -RunBuild -RunLong`: 422 / 422 passed, 6 skipped (the Linux VM checks:
  the VM did not answer at 192.168.1.105, its address from 2026-10-15). F-15: the service published for Windows and for
  Linux holds no `createdump` and starts. Every earlier check is carried, the frozen v1.1.1.0 gate passes, the long set
  and both harnesses pass.
- **Packages:** the Windows ZIP (575 files), the Linux ZIP (569 files) and the Docker image hold no `createdump` and no
  `vcruntime` DLL; the image answered `/healthz` as v1.1.1.1 with TLS and sign-in on. All three are in `SHA256SUMS.txt`.
- **Screenshots:** the four README screenshots retaken from this version against a demo copy of the world (checked by
  eye: no addresses shown).
- **Static gate:** passed after the screenshot and docs update. **Validate-Release -Strict:** 0 errors, 0 warnings.
  **Distribution check:** passed.