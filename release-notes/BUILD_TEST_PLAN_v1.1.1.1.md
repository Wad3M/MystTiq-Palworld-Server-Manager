<!-- MystTiq v1.1.5.0: file reviewed for this release (2026-10-24). -->
# v1.1.1.1 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.1.1.1-Logic.ps1 -RunBuild -RunLong` (with `MYSTTIQ_LINUX_HOST` set to the VM's current address): the
   frozen v1.1.1.0 gate, every earlier contract, the v1.1.1.1 contracts, every carried smoke, the long set, both harnesses and
   the Linux VM checks. F-15: the service published for Windows and Linux holds no `createdump` and starts.
3. `Build.ps1 Package`, `scripts/Build-DockerImage.ps1 -Save`, then `scripts/Test-v0.9.9.0-Distribution.ps1`; the Windows
   and Linux ZIPs and the image hold no `createdump`.
4. `scripts/New-MystTiqCheckpoint.ps1`, then extract the FullSource ZIP into an empty folder and run
   `Test-v1.1.1.1-Logic.ps1` there (static).
