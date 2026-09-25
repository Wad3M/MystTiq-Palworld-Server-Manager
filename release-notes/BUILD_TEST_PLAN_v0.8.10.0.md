# v0.8.10.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of both harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.10.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.9.0 gate;
   - every carried smoke;
   - the logic harness;
   - `MystTiq.ArtworkHarness`, including the original images on 13 pages in dark and light.
4. Publish the desktop build.
   - Launch it and check `/healthz` reports 0.8.10.0.
   - Look at the live Ribbon: Backup and Doctor on every page, and the batch 3 redesigns on Doctor, Diagnostics,
     Players, Map and Notifications.
5. Create the FullSource ZIP and verify it against the SHA-256 manifest.

Any failure blocks promotion.
