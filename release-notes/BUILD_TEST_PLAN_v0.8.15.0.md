# v0.8.15.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of the harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.15.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.14.0 gate;
   - every carried smoke;
   - the logic harness;
   - the ArtworkHarness, including the access-refusal checks;
   - the live remote sign-in against an isolated instance on the Linux test VM. It is reported as SKIP when the VM
     does not answer on SSH; set `MYSTTIQ_LINUX_HOST` if its address changed.
4. Confirm the remote instance was removed ("remote instance removed: True") and nothing is left in `/tmp` on the VM.
5. Publish the desktop build and check `/healthz` reports 0.8.15.0.
6. Confirm local, token-less use still shows every card and no role notice.
7. Create the FullSource ZIP and verify it against the SHA-256 manifest.

Any failure blocks promotion.
