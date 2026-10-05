<!-- MystTiq v1.0.0.6: file reviewed for this release (2026-10-05). -->
# v1.0.0.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.0.0-Logic.ps1 -RunBuild`: the frozen v0.9.10.0 gate, the 1.0 contracts (version, full-release
   workflow, public docs without open items, the update check counting only full releases from 1.0), every carried
   smoke, both harnesses and the Linux VM checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.0.0-Logic.ps1` there (static).
5. Live: `/healthz` reports 1.0.0.0; the app reuses its helper across a restart.
6. Push `main`, tag `v1.0.0.0`, review the draft release (not a prerelease) and publish it.
