# v0.7.106.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean, then PUBLISH the desktop build (with
   `Select-Object -Last`, never `-First`) so the live UI check uses current code.
2. Run strict validation and `scripts/Test-v0.7.106.0-Logic.ps1 -RunBuild`, including the frozen v0.7.105.0
   checkpoint gate, every carried-forward smoke, and `MystTiq.LogicHarness`. No new server route was touched
   this version, so no new route smoke exists.
3. New surface: `MapLabelLayout.ComputeLabelOffsets`, `PlayerMapPointDto`/`BaseMapPointDto.LabelMargin`,
   `MainWindowViewModel.ApplyMapLabelDeoverlap`.
4. Live UI check (window rendered with PrintWindow; helper `ui-capture-helper.ps1`): open the Map page on a
   profile with a base and an offline player close together (the clone's real save has this), confirm their
   labels no longer overlap, and confirm zooming in spreads markers apart without breaking anything. Tune
   `CollisionRadius`/`LabelStep` further if a real cluster still overlaps.
5. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
