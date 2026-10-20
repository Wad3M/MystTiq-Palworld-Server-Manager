<!-- MystTiq v1.1.2.0: file reviewed for this release (2026-10-19). -->
# MystTiq v1.1.2.0 Checkpoint: Why It Stopped, and Start with Windows

## What changed

- **Why a server stopped (F-19).** Every stop now says who or what asked for it: a button or the tray in MystTiq, Exit,
  Discord (with the user), an automation rule, MOD Safe Start, a character move, a frozen-server restart, the command
  line, the service stopping, or the API (with the account). The Dashboard's Overall Health card shows
  "Last stop: <reason> · <time>", and the activity log says "Server stopped: <reason>". The next unexplained stop will
  say what did it.
- **Start with Windows (F-20).** Settings > Appearance, "Start MystTiq when I sign in (opens in the tray)". Off until you
  turn it on; I have not turned it on on your PC. With it on, the server that was running when the PC went down starts
  again, and one you stopped stays stopped. A Windows restart is no longer held up by the window hiding to the tray.
- **Patch.** The Linux VM checks pass `-n` to ssh, so a finished remote command cannot leave a check waiting.

## Verification

- **Full gate** `scripts\Test-v1.1.2.0-Logic.ps1 -RunBuild -RunLong` (VM at 192.168.1.122): 440 / 440 passed, 1 skipped
  (the system-wide unit: no passwordless sudo). The Linux desktop session check passed on the first run (ssh now with
  `-n`). LogicHarness: the four F-19/F-20 scenarios; ArtworkHarness: 852 checks, including the stop reason sent, the
  Last stop line and the Settings option on a test Run key (deleted afterwards; the real Run key untouched).
- **Packages:** the Windows ZIP, the Linux ZIP and the Docker image (`/healthz` answered v1.1.2.0 with TLS and sign-in
  on), no `createdump`, all in `SHA256SUMS.txt`. **Linux VM:** v1.1.2.0 installed in the home folder
  (`~/MystTiq/current`), the production unit untouched. **Static gate:** 374 / 374. **Validate-Release -Strict:** 0
  errors, 0 warnings. **Distribution check:** passed.
