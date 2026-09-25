# v0.8.7.0 Build and Test Plan

1. Clean, including both harnesses' bin/obj and FakePalServer. Then PUBLISH the desktop build (use `Select-Object -Last`
   on its output, never `-First`).
2. Run `scripts/Validate-Release.ps1 -Strict` after Clean: 0 errors / 0 warnings.
3. Run `scripts/Test-v0.8.7.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.6.0 gate;
   - every carried smoke;
   - the logic harness;
   - `MystTiq.ArtworkHarness`, including the Dashboard in all three languages at 950x650 and 1440x880.
4. Live check:
   - Switch to Deutsch. The Dashboard's labels, buttons and templates should be German.
   - Switch back to English.
   - Leave the real app on English.
5. Create the FullSource ZIP and verify the SHA-256 manifest.

Any failure blocks promotion.
