# v0.8.14.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of both harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.14.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.13.0 gate;
   - every carried smoke;
   - the logic harness, including "Role cards";
   - the ArtworkHarness, which checks every gated card for each role.
4. Publish the desktop build and check `/healthz` reports 0.8.14.0.
5. Confirm the local, token-less Desktop still shows every card and no role notice.
6. Create the FullSource ZIP and verify it against the SHA-256 manifest.

This version is Desktop-only. A live signed-in check as each role, against a remote instance, is v0.8.15.0.

Any failure blocks promotion.
