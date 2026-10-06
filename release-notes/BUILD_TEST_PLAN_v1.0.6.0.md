<!-- MystTiq v1.0.6.0: file reviewed for this release (2026-10-06). -->
# v1.0.6.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.6.0-Logic.ps1 -RunBuild`. It runs:
   - the frozen v1.0.5.0 gate and every earlier contract;
   - the v1.0.6.0 contracts;
   - every carried smoke (the Pal box and MOD layout smokes included);
   - both harnesses;
   - the Linux VM checks, including the per-user systemd unit.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.6.0-Logic.ps1` there (static).
5. Owed (roadmap evidence): a player looking in game on the clone (S-1, S-2, S-3); Discord and email sends (R-2); the
   owner's Xbox (X-1); the Nexus install with the owner's account (M-1).
