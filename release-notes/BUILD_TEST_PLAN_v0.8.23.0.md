# v0.8.23.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of the harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.23.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.22.0 gate;
   - the command-role audit against the table;
   - every carried smoke;
   - both harnesses, including every gated command and every page for each role;
   - on the Linux VM when it answers: the remote sign-in from Windows and Linux, and the unit check.
4. Publish the desktop build and check `/healthz` reports 0.8.23.0.
5. Create the FullSource ZIP and verify it against the SHA-256 manifest.

Any failure blocks promotion.