# v0.7.100.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the smokes and the live UI check use current code.
2. Run strict validation and `scripts/Test-v0.7.100.0-Logic.ps1 -RunBuild`, including the frozen v0.7.99.0
   checkpoint regression gate, every carried-forward smoke (v0.7.97.0, v0.7.98.0 and the new v0.7.100.0) and
   `MystTiq.LogicHarness`.
3. New surface: `MapViewport`, wheel/drag/click-to-zoom, offline markers, `SavePlayerLocationReader` and the
   explorer's `playerLocations`.
4. **Live UI check** (window rendered with PrintWindow, input sent with PostMessage so it works with the display
   asleep; see `ui-capture-helper.ps1`): open a tab on a profile whose world has players, then World > Map.
   Confirm offline rings and the status line, wheel zoom (readout), Reset View, marker and list clicks, drag pan
   and the page not scrolling under the wheel. Do not run actions on the real Default Server.
5. Still needs a person: solid green online dots (needs a player online).
6. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
