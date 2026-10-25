<!-- MystTiq v1.1.5.0: file reviewed for this release (2026-10-25). -->
# MystTiq v1.1.5.0 Checkpoint: Resizable Window, Simple Mode, Installer and Portable

## What changed

- **A window you can size (V-1).** Drag any edge or corner; it remembers its size, position and maximized state.
- **Simple and Advanced (V-2).** Click the switch in the title bar. Simple is for setting a server up, choosing its
  settings and starting it: Dashboard (with a three-step guide), Server Setup, Configuration, Backups and Settings, with
  the other tabs, pages, specialist buttons and the Dashboard's monitoring panels hidden. Advanced shows everything.
  The switch now works with one click: Settings' Simple/Advanced list used to set the mode straight back.
- **The Dashboard's top row (U-2, V-3).** LIFECYCLE, then the local and public addresses in their own card, then the
  update card with MystTiq (Update MystTiq) and the Palworld server (Update Server). The row stays while the service is
  not connected, and the desktop now connects by itself to a helper that was slow to start (45 s wait, then a quiet
  retry every 10 s); after the first install it had stayed on "Service unreachable".
- **Configuration (V-4, V-5).** Simple Settings is blue and Advanced Settings amber, and the title bar's switch matches
  them. The page fills itself in from the server's settings file whether the server runs or not.
- **Portable (W-2)** and **Windows setup (W-1)**, as in the first v1.1.5.0 checkpoint; the setup's `.msi` is signed by the
  release workflow once SignPath is set up.

## Verification

- **Full gate** `scripts\Test-v1.1.5.0-Logic.ps1 -RunBuild -RunLong` (2026-10-24, after the Simple mode, update card,
  addresses, reconnect, Simple/Advanced switch, switch colour, Configuration-loading and O-6/O-7 rework): 454 / 454 passed, 6 skipped: the Linux VM (192.168.1.122) answered ping but not SSH,
  so its checks did not run and v1.1.5.0 was not copied to it. Desktop harness: 875 checks ("Server build —" with no answer and the old connection line cleared on connect, the title bar's switch clicked with Settings' Simple/Advanced list on screen, sticking both ways, blue in Simple and amber in Advanced, Configuration opened while busy waiting and then loading, the update card's MystTiq and
  server lines in 12 languages at 950, 1100 and 1440 px, the addresses card between LIFECYCLE and the update card, the
  top row shown while the service is not connected, the quiet reconnect every other tick against a live and a closed
  port, the Simple/Advanced toggle clicked with the mouse: 5 menu pages, the World, MODs, Tools and Host tabs hidden, the
  Ribbon filtered, the Dashboard's detail panels hidden, the guide's three buttons, and back); logic harness all
  scenarios.
- **Seen on the owner's PC after the first install of this build:** the desktop stayed on "Service unreachable" (the
  helper's first start with five servers took longer than the 8 s wait, and the disconnected tick never retried), so the
  top row looked missing; and the title bar's Simple/Advanced switch did nothing (Settings' list wrote Advanced back on
  every change). Both fixed in this build; the switch was proven by clicking it in a real window (an isolated portable
  copy, mouse messages posted to its window), one click to Simple and one back.
- **Packages:** Windows ZIP, portable ZIP (with `portable.txt`), setup `.msi`, Linux ZIP and Docker image (`/healthz`
  answered over TLS on 18213); all in `SHA256SUMS.txt`. The console proxy is built between `Build.ps1 Clean` and
  `Package` (an earlier packaging without it was caught by the distribution check); the Windows ZIP, the portable ZIP and
  the setup each hold `MystTiqConsoleProxy.dll`. **Setup:** an administrative extract (577 files, installed.txt present,
  no portable.txt); not installed on the owner's PC (needs administrator rights). **VirusTotal** (owner uploaded,
  2026-10-25): the setup `899b30e1…` 0/49 and the portable ZIP `4903af4a…` 0/62, no Sigma rules or sandbox detections; one
  medium MITRE signature on the portable ZIP (the desktop's call to its helper on 127.0.0.1). **Validate-Release -Strict:** 0 errors,
  0 warnings. **Distribution check:** passed. **Static gate:** 393 / 393. **README screenshots** (O-6): retaken from an isolated demo service with this PC's addresses replaced by documentation addresses before each capture (0 leaks reported, every image looked at).

## Still owed

See the roadmap's "Outstanding for v1.1.5.0": SignPath (O-4, applied 2026-10-25, waiting for approval), the release
build blocked by Microsoft's .NET 10.0.403 feed (O-9) and the translation review (O-8). Done: the
VirusTotal scan (O-1), the push and tag (O-3), README screenshots (O-6) and two status-line follow-ups (O-7). The owner
removed the Linux VM install (O-2) and publishing v1.1.1.3 and v1.1.2.0 (O-5) on 2026-10-25.
