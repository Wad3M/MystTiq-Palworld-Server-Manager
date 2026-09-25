# v0.8.25.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of the harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.25.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.24.0 gate;
   - every carried smoke;
   - both harnesses: the ArtworkHarness checks every mode's decorative colours and Windows contrast themes;
   - the Linux VM checks when it answers.
4. Publish the desktop build and check `/healthz` reports 0.8.25.0.
5. Look at the running window in Dark: it must look as before.
6. Create the FullSource ZIP and verify it against the SHA-256 manifest. This checkpoint is the accepted baseline.

Any failure blocks promotion.