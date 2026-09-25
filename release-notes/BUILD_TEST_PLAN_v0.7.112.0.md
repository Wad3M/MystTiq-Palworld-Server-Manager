# v0.7.112.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the smoke and live checks use current code.
2. Run strict validation and `scripts/Test-v0.7.112.0-Logic.ps1 -RunBuild`, including the frozen v0.7.111.0
   checkpoint gate, every carried-forward smoke (now including v0.7.111.0's), the new v0.7.112.0 smoke, and
   `MystTiq.LogicHarness`.
3. New surface: `HeadlessKitService.GiveEntriesAsync`/`DeliverAsync`, `POST /api/v1/players/{playerId}/give`,
   the Players page Give box and context-menu entry.
4. Live: the Give box renders on Player Administration and Give is disabled without an online player. With a
   real player online and PalDefender + RCON set up, give `item PalSphere 1` and confirm it arrives (not
   possible on this machine today; recorded as not verified).
5. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
