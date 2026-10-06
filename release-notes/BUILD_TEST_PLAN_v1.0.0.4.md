<!-- MystTiq v1.0.2.0: file reviewed for this release (2026-10-05). -->
# v1.0.0.4 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.0.4-Logic.ps1 -RunBuild`: the frozen v1.0.0.3 gate, every earlier contract, the v1.0.0.4 contracts
   (world clock, restore, backup days), the restore smoke (real backups, isolated server), every carried smoke, both
   harnesses and the Linux VM checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.0.4-Logic.ps1` there (static).
5. Live: the Dashboard shows the world's current day; the Backups list shows each backup's day; a restore on a test
   server reports the restored day and the game shows it.
