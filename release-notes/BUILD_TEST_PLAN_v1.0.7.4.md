<!-- MystTiq v1.1.5.1: file reviewed for this release (2026-10-26). -->
# v1.0.7.4 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.7.4-Logic.ps1 -RunBuild`: the frozen v1.0.7.3 gate, every earlier contract, the v1.0.7.4 contracts,
   every carried smoke, both harnesses and the Linux VM checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.7.4-Logic.ps1` there (static).
5. Owed: after the owner updates the service, their existing channels still send (a test notification), and the Discord
   bot still signs in; then the plain copy is deleted from Server Doctor.
