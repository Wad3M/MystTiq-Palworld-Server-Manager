# v0.8.22.0 Build and Test Plan

1. Clean the build output, including the bin/obj folders of the harnesses and FakePalServer.
2. Run `scripts/Validate-Release.ps1 -Strict`. It must report 0 errors and 0 warnings.
3. Run `scripts/Test-v0.8.22.0-Logic.ps1 -RunBuild`. It covers:
   - the frozen v0.8.21.0 gate;
   - every carried smoke;
   - both harnesses;
   - on the Linux VM when it answers:
     - the remote sign-in as Viewer, Operator, Admin and Owner from Windows and from Linux;
     - the systemd unit check.
4. Publish the desktop build and check `/healthz` reports 0.8.22.0.
5. Look at the running window's title bar: every icon is drawn.
6. Create the FullSource ZIP and verify it against the SHA-256 manifest.

The Linux run uses an isolated instance in `/tmp` and its own home folder there. The VM's installed service,
`/etc/mysttiq` and the VM user's settings are not touched.

Any failure blocks promotion.