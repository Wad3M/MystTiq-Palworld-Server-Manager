# v0.8.8.0 Build and Test Plan

1. Clean, including both harnesses' bin/obj and FakePalServer. Then PUBLISH the desktop build (use `Select-Object -Last`
   on the output, never `-First`).
2. Run `scripts/Validate-Release.ps1 -Strict` after Clean. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.8.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.7.0 gate;
   - every carried smoke;
   - the logic harness;
   - `MystTiq.ArtworkHarness`: the image icons on 13 pages, in dark and light;
   - the check that all 30 packages match their recorded hashes.
4. Live check: the new icons appear on Configuration, Backups, MOD Library and Server Doctor.
5. Create the FullSource ZIP and verify its SHA-256 manifest.

Any failure blocks promotion.
