# v0.7.99.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`, on its output) so the smokes and the live UI check use current code.
2. Run strict validation and `scripts/Test-v0.7.99.0-Logic.ps1 -RunBuild`, including the frozen v0.7.98.0
   checkpoint regression gate, the carried-forward smoke suites (v0.7.97.0 and v0.7.98.0 included) and
   `MystTiq.LogicHarness`.
3. New surface: the Map page and navigation entry, marker positioning, coordinate text, the Players page pointer.
4. **Live UI check** (possible from this session: capture the app window and drive it with synthetic clicks):
   launch the published app, open a tab on a profile whose world has bases, then World > Map. Confirm the entry
   and icon, that the map loads on Palpagos, the status lines, the base list coordinates, that markers sit on
   land at the expected places (Default Server: first base on the start peninsula), and that the map fits the
   window. Open Players and confirm the Open Map pointer. Do not run actions on the real Default Server.
5. Still needs a person: player dots (needs a player online), and the Crash Analyzer detail pane with findings.
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
