<!-- MystTiq v1.1.6.0: file reviewed for this release (2026-10-26). -->
# v1.1.2.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.1.2.0-Logic.ps1 -RunBuild -RunLong` with `MYSTTIQ_LINUX_HOST=192.168.1.122`: every earlier contract and
   smoke, the long set, both harnesses (stop reasons, the Last stop line, the Settings option on a test Run key) and the
   VM checks (now with `ssh -n`).
3. `Build.ps1 Package`, `scripts/Build-DockerImage.ps1 -Save`, `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, archive gate, install on the owner's PC: a stop from the desktop shows its reason on the Dashboard and in
   the activity log. The start-at-sign-in option is not turned on there (the owner's choice).
