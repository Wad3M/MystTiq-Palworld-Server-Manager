<!-- MystTiq v1.1.5.0: file reviewed for this release (2026-10-23). -->
# v1.1.4.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.1.4.0-Logic.ps1 -RunBuild -RunLong` with `MYSTTIQ_LINUX_HOST=192.168.1.122`: every earlier contract and
   smoke, the long set, both harnesses (the update flow, the card in 12 languages at three widths, the wrong-path rule,
   the interrupted stop) and the VM checks.
3. `Build.ps1 Package`, `scripts/Build-DockerImage.ps1 -Save`, `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, archive gate, install on the owner's PC: the card shows the installed and public builds. The update itself
   is not run there (the owner's choice when an update is out).
