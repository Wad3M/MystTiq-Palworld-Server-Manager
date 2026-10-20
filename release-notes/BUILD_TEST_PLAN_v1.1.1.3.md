<!-- MystTiq v1.1.4.0: file reviewed for this release (2026-10-20). -->
# v1.1.1.3 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.1.1.3-Logic.ps1 -RunBuild -RunLong` with `MYSTTIQ_LINUX_HOST=192.168.1.122`: every earlier contract and
   smoke, the long set, both harnesses (the ArtworkHarness on Avalonia 12.1.4, the Linux desktop session) and the VM checks.
3. `Build.ps1 Package`, `scripts/Build-DockerImage.ps1 -Save`, `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, archive gate, install; the release workflow (download-artifact v8) attaches the Docker image.
