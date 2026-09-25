# v0.8.6.0 Build and Test Plan

1. Clean, including the bin/obj folders of both harnesses and FakePalServer. Then PUBLISH the desktop build. Use
   `Select-Object -Last` on the output, never `-First`.
2. Run `scripts/Validate-Release.ps1 -Strict` after Clean: 0 errors / 0 warnings.
3. Run `scripts/Test-v0.8.6.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.5.0 gate;
   - every carried smoke;
   - the logic harness;
   - `MystTiq.ArtworkHarness`, which visits every page in three languages at two sizes and checks for raw keys and
     cut-off buttons.
4. Live check:
   - Switch to Deutsch. The Ribbon shows German on the Dashboard and on Doctor, with the icons intact.
   - Switch back to English.
   - Leave the real app on English.
5. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
