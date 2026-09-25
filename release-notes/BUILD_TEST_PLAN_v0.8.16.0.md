# v0.8.16.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of the harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.16.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.15.0 gate;
   - every carried smoke;
   - the logic harness;
   - the live remote sign-in (SKIP when the Linux VM does not answer);
   - the ArtworkHarness: every mode, following the system and density.
4. Publish the desktop build and check `/healthz` reports 0.8.16.0.
5. Live check, on a clone tab only:
   - Midnight, High contrast and Compact are applied and look as rendered;
   - the other tabs keep their own mode;
   - Dark and Comfortable are restored afterwards.
6. Create the FullSource ZIP and verify it against the SHA-256 manifest.

This version is Desktop-only. Following the system was checked with the harness standing in for the operating system;
the real Windows switch was not flipped.

Any failure blocks promotion.
