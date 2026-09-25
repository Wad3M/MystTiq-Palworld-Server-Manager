# v0.8.1.0 Build and Test Plan

1. Clean (including both harnesses' bin/obj), then PUBLISH the desktop build (with `Select-Object -Last`, never `-First`).
2. `scripts/Validate-Release.ps1 -Strict` after Clean: 0 errors / 0 warnings.
3. `scripts/Test-v0.8.1.0-Logic.ps1 -RunBuild`, including the frozen v0.8.0.0 gate, every carried smoke, the logic harness
   and `MystTiq.ArtworkHarness` (191 checks, including the Ribbon icons).
4. Live: the Home and Server Ribbons show the ten vector icons in the tab's theme colours; buttons without one keep their
   glyph.
5. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
