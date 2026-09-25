# v0.8.21.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of the harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.21.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.20.0 gate;
   - every carried smoke;
   - both harnesses (including the unit scenario);
   - the `--fleet-root` smoke, and the check that every smoke uses its own fleet folder;
   - on the Linux VM when it answers: the systemd unit verified by systemd, and the live remote sign-in.
4. Publish the desktop build and check `/healthz` reports 0.8.21.0.
5. Create the FullSource ZIP and verify it against the SHA-256 manifest.

This version installs nothing on the VM; the check uses `service-unit` and `systemd-analyze verify` in `/tmp` only.

Any failure blocks promotion.
