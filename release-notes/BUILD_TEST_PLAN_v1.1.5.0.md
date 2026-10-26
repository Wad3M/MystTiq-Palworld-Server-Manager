<!-- MystTiq v1.1.6.0: file reviewed for this release (2026-10-26). -->
# v1.1.5.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.1.5.0-Logic.ps1 -RunBuild -RunLong` with `MYSTTIQ_LINUX_HOST=192.168.1.122`: every earlier contract and
   smoke, the long set, both harnesses (the resize grips and placement, every page at 1680, 2560 and 3840 px wide,
   Simple and Advanced, the portable folders and the update kinds) and the VM checks.
3. `Build.ps1 Package` (now also the portable ZIP and the setup), `scripts/Build-DockerImage.ps1 -Save`,
   `scripts/Test-v0.9.9.0-Distribution.ps1`; the setup checked with an administrative extract (`msiexec /a`) and its
   tables read (shortcuts, the service actions and their conditions); the portable helper run from the portable ZIP.
4. Checkpoint, archive gate, install on the owner's PC (the ZIP copy, as before; the setup is the owner's to run, since
   it needs administrator rights). Before publishing: the owner scans the setup and the portable ZIP on VirusTotal.
