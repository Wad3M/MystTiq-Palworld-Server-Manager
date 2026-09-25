# v0.8.24.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of the harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.24.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.23.0 gate;
   - every carried smoke, and the cores smoke (real process, service mode, a restart);
   - both harnesses;
   - on the Linux VM when it answers: cores on every thread, the unit check and the remote sign-in.
4. Publish the desktop build and check `/healthz` reports 0.8.24.0.
5. Create the FullSource ZIP and verify it against the SHA-256 manifest.

Any failure blocks promotion.