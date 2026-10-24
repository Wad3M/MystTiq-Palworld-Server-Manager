<!-- MystTiq v1.1.5.0: file reviewed for this release (2026-10-24). -->
# v1.1.1.2 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.1.1.2-Logic.ps1 -RunBuild -RunLong` (with `MYSTTIQ_LINUX_HOST` set to the VM's current address).
3. F-16: the LogicHarness reads MystTiq's own copy when the one beside `Level.sav` is older (the owner's case), says so,
   never uses an empty copy, and the world clock follows the same rule.
4. F-17: the ArtworkHarness finds 29 navigation icons, MOD Sources' own among them.
5. `Build.ps1 Package`, `scripts/Build-DockerImage.ps1 -Save`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
6. `scripts/New-MystTiqCheckpoint.ps1`, then the archive gate. Live: the owner's Players page shows Wade and Melly under
   their current IDs.
