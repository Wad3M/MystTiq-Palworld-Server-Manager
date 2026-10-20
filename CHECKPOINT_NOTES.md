<!-- MystTiq v1.1.4.0: file reviewed for this release (2026-10-20). -->
# MystTiq v1.1.4.0 Checkpoint: Update the Server from the Dashboard

## What changed

- **Server update card (U-1).** The Dashboard's LIFECYCLE bar is shorter, and beside it, above OVERALL HEALTH, a
  card shows your installed Palworld build against Steam's, with **Update Server**. It asks first (saying how many
  players are online), then the service stops the server, takes a safety backup, runs SteamCMD and starts it again,
  even if you close MystTiq. I have not run an update on your server; yours is on Steam's current build.
- **Wrong-path warning fixed (F-21).** With your five server profiles, a stopped one said "A PalServer process is
  running, but not at the configured path" whenever another of your servers ran. Found while taking the screenshots.
- **Interrupted stops logged (F-22).** A stop whose request was cancelled now leaves a line in the activity log.
- **Screenshots.** All twelve in `docs/images` retaken from v1.1.4.0; the README captions say so.

## Verification

- **Full gate** `scripts\Test-v1.1.4.0-Logic.ps1 -RunBuild -RunLong` (VM at 192.168.1.122): 447 / 447 passed, 1 skipped
  (the system-wide unit: no passwordless sudo). LogicHarness: the update flow (running and stopped server, failed backup,
  failed update, refused second update), the wrong-path rule and the interrupted stop. ArtworkHarness: 858 checks,
  including the card in 12 languages at 950, 1100 and 1440 px (the Italian tag ends in "…" at 950 px, with a tooltip).
- **Live:** the card's route answered from a demo service using the real SteamCMD (build 25247047, up to date), and the
  demo's status read "PalServer is not running." while the owner's server ran (it reported that server as its own wrong
  path before F-21). All twelve screenshots retaken from that demo and checked by eye; the demo was deleted.
- **Packages:** the Windows ZIP, the Linux ZIP and the Docker image (`/healthz` answered v1.1.4.0 with TLS and sign-in
  on), no `createdump`, all in `SHA256SUMS.txt`. **Linux VM:** v1.1.4.0 installed in the home folder
  (`~/MystTiq/current`), the production unit untouched. **Static gate:** 381 / 381. **Validate-Release -Strict:** 0
  errors, 0 warnings. **Distribution check:** passed.
