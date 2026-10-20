<!-- MystTiq v1.1.2.0: file reviewed for this release (2026-10-19). -->
# v1.1.0.3 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.1.0.3-Logic.ps1 -RunBuild -RunLong`: the frozen v1.1.0.2 gate, every earlier contract, the v1.1.0.3
   contracts, every carried smoke, the long set, both harnesses and the Linux VM checks.
3. F-3: the LogicHarness checks the player list (no "None" record, a move's merge, the clean-up of stale records), the
   removal of archived files only when a backup holds them, and the real-world move leaving nothing beside the world.
4. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
5. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.1.0.3-Logic.ps1` there (static).
