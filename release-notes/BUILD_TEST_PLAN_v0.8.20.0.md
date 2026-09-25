# v0.8.20.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of the harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.20.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.19.0 gate;
   - every carried smoke, plus the v0.8.20.0 host history smoke;
   - both harnesses;
   - the live remote sign-in (SKIP when the Linux VM does not answer).
4. Run `scripts/Test-v0.8.20.0-LinuxIsolated.ps1` against the Linux test VM.
5. Publish the desktop build and check `/healthz` reports 0.8.20.0.
6. Live check on the clone tab: the History card shows today's readings.
7. Create the FullSource ZIP and verify it against the SHA-256 manifest.

Any failure blocks promotion.
