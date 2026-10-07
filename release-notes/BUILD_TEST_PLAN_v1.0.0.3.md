<!-- MystTiq v1.0.6.1: file reviewed for this release (2026-10-06). -->
# v1.0.0.3 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.0.3-Logic.ps1 -RunBuild`: the frozen v1.0.0.2 gate, every earlier contract, the v1.0.0.3 contracts
   (NATIVE MODs, the drop zone), the NATIVE MOD smoke, every carried smoke, both harnesses and the Linux VM checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.0.3-Logic.ps1` there (static).
5. Live: the MODs page lists PalDefender and the UE4SS loader as NATIVE MODs; a ZIP dragged from Explorer onto the
   install box installs; a player can use Give Item (PalDefender loaded).
