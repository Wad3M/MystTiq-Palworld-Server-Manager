# v0.7.113.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the smoke and live checks use current code.
2. Run strict validation and `scripts/Test-v0.7.113.0-Logic.ps1 -RunBuild`, including the frozen v0.7.112.0
   checkpoint gate, every carried-forward smoke (now including v0.7.112.0's), the new v0.7.113.0 smoke, and
   `MystTiq.LogicHarness`.
3. New surface: `HeadlessTeleportService` (chat watcher, identity check, cooldown), `TeleportPointText`,
   `/api/v1/teleport` routes, the Map page Teleport Points card.
4. The new smoke starts two helper processes (fake RCON and REST on ports 18323/18333) and kills them at the
   end; confirm none are left running.
5. Live: the card renders on the Map page. With a real player, PalDefender and RCON: save a point, have the
   player type `!tp <name>`, and confirm they move and get the reply (not possible on this machine today;
   recorded as not verified).
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
