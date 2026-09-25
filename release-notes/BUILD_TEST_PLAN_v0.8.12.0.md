# v0.8.12.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of both harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.12.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.11.0 gate;
   - every carried smoke;
   - the new second-NAT smoke;
   - the logic harness, including the "Second NAT" scenario;
   - `MystTiq.ArtworkHarness`.
4. Publish the desktop build and check `/healthz` reports 0.8.12.0.
5. Live check: open Diagnostics → WAN / External Reachability → Run Reachability Check. The run is read-only and
   adds no mapping. Confirm:
   - the router is found over UPnP;
   - the second-NAT check shows a verdict with a reason;
   - the outside-in entry is Skipped.
6. Linux: run `scripts/Test-v0.8.12.0-LinuxIsolated.ps1 -LinuxHost <VM address>`. It must end with "exit=0".
7. Create the FullSource ZIP and verify it against the SHA-256 manifest.

Any failure blocks promotion.
