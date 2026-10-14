<!-- MystTiq v1.0.8.6: file reviewed for this release (2026-10-12). -->
# v1.0.7.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.7.0-Logic.ps1 -RunBuild`: the frozen v1.0.6.1 gate, every earlier contract, the v1.0.7.0 contracts,
   every carried smoke (with the new saved-lists route smoke), both harnesses and the Linux VM checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.7.0-Logic.ps1` there (static).
