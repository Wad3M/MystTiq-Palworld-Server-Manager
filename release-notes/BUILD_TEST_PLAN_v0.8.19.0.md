# v0.8.19.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of the harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.19.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.18.0 gate;
   - every carried smoke (including the v0.7.114.0 multi-user smoke), plus the v0.8.19.0 route roles smoke with
     authentication on;
   - both harnesses;
   - the live remote sign-in on the Linux VM (SKIP when it does not answer).
4. Publish the desktop build and check `/healthz` reports 0.8.19.0.
5. Live check on the clone tab: local use keeps every Ribbon button enabled.
6. Create the FullSource ZIP and verify it against the SHA-256 manifest.

Any failure blocks promotion.
