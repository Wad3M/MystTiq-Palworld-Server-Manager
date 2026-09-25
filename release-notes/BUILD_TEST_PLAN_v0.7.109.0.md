# v0.7.109.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the live UI check uses current code.
2. Run strict validation and `scripts/Test-v0.7.109.0-Logic.ps1 -RunBuild`, including the frozen v0.7.108.0
   checkpoint gate, every carried-forward smoke, and `MystTiq.LogicHarness`. No new server route was touched
   this version, so no new route smoke exists.
3. New surface: `MapLabelLayout.ComputeMarkerOffsets`, `PlayerMapPointDto`/`BaseMapPointDto.MarkerOffsetX/Y`,
   `MainWindowViewModel.ApplyMapLabelDeoverlap`'s new marker-then-label ordering.
4. Live UI check (window rendered with PrintWindow; helper `ui-capture-helper.ps1`): open the Map page on a
   profile with a genuinely coincident marker cluster (the clone's real save has one — several offline players
   at the exact same last-known position), confirm they now render as distinct fanned-out icons instead of one.
5. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
