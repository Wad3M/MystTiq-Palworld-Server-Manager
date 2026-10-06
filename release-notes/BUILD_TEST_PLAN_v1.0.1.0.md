<!-- MystTiq v1.0.6.0: file reviewed for this release (2026-10-06). -->
# v1.0.1.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.1.0-Logic.ps1 -RunBuild`: the frozen v1.0.0.6 gate, every earlier contract, the v1.0.1.0 contracts,
   every carried smoke, both harnesses and the Linux VM checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.1.0-Logic.ps1` there (static).
5. Live: the Update Center shows Update on all 12 rows, greyed out for SteamCMD and (with nothing newer) MystTiq, with
   the reason beside it. PalDefender's update, run on a copy of the live Win64 files against the real GitHub release.
