# v0.8.17.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of the harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.17.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.16.0 gate;
   - every carried smoke, plus the new v0.8.17.0 host and priority smoke;
   - the logic harness, including the two priority and eco mode scenarios;
   - the ArtworkHarness, including the Host page;
   - the live remote sign-in (SKIP when the Linux VM does not answer).
4. Run `scripts/Test-v0.8.17.0-LinuxIsolated.ps1` against the Linux test VM.
5. Publish the desktop build and check `/healthz` reports 0.8.17.0.
6. Live check, on the clone tab only:
   - open the HOST tab and compare the readings with Task Manager;
   - do not change the priority of a real server.
7. Create the FullSource ZIP and verify it against the SHA-256 manifest.

Any failure blocks promotion.
