# v0.7.96.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and `scripts/Test-v0.7.96.0-Logic.ps1 -RunBuild`, including the frozen v0.7.95.0
   checkpoint regression gate, the carried-forward smoke suites, and `MystTiq.LogicHarness` (now including 3
   map scenarios; `PalworldMapCoordinates.cs` and `MapContentsText.cs` are compiled into the harness).
3. New surface: the calibrated image mapping, on-by-default real positions, expanded card, first-run Palpagos
   background, bases applied from the Players page read, the map status line.
4. Live check: `GET /api/v1/world/players-guilds` on the Default Server (read-only) returns the world's base
   coordinates that the map plots; the same coordinates are checked against the mapping.
5. Not covered without GUI automation: how the card actually renders. Open the Players page and check that the
   Palpagos map is showing, the amber base markers sit on the start peninsula, and the status line reads
   sensibly with the server stopped and running.
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
