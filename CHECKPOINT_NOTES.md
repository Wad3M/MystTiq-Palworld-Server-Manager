<!-- MystTiq v1.1.1.2: file reviewed for this release (2026-10-19). -->
# MystTiq v1.1.1.2 Checkpoint: Current Players, Guilds and Map, New Icons

## What changed

**Your Players, Guilds, Bases and Map pages show the world as it is now**, as you asked. They read a decoded copy of
the world from 10-01 that sits beside `Level.sav` (MystTiq may not write in your save folder), so they still showed
Wade and Melly under their old IDs. They now read whichever copy is newer, beside `Level.sav` or MystTiq's own, and say
which. The Give Item picker reads the same way.

**Your new icons:** MOD Sources has its own icon (it borrowed MOD Dashboard's) and Launcher has the rocket. Their dark
backgrounds were made transparent so they sit on the navigation like the others.

**Proven on a copy of your world:** with the 10-01 copy kept beside `Level.sav` and made read-only (as on your
server), the Players data came from MystTiq's own current copy, with Wade under E290DA9A and Melly under 1467C601. The
README screenshots were retaken from that run, so they show v1.1.1.2 and the new MOD Sources icon.

## Verification

- **Full gate** `scripts\Test-v1.1.1.2-Logic.ps1 -RunBuild -RunLong`: 425 / 425 passed. The Linux VM was back at
  **192.168.1.122**, and its checks were run against that address straight after: cores pinned, the service unit accepted
  by systemd, remote sign-in as all four roles from Windows and Linux, the per-user systemd unit and the desktop in the
  XFCE session all **passed** (the system-wide unit check stays skipped: no passwordless sudo).
- **F-16:** the LogicHarness scenario passes, and on a demo copy of your world (stale 10-01 copy beside `Level.sav`,
  read-only) the Players data came from MystTiq's own current copy.
- **F-17:** both icons are 1254x1254 with a transparent background; the ArtworkHarness counts 29 navigation icons.
- **Packages:** the Windows ZIP, the Linux ZIP and the Docker image (it answered `/healthz` as v1.1.1.2 with TLS and
  sign-in on), none holding `createdump`, all in `SHA256SUMS.txt`. **Static gate:** passed after the screenshot update.
  **Validate-Release -Strict:** 0 errors, 0 warnings. **Distribution check:** passed.
