<!-- MystTiq v1.0.0.5: file reviewed for this release (2026-10-05). -->
# v1.0.0.2 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.0.2-Logic.ps1 -RunBuild`: the frozen v1.0.0.1 gate, every earlier contract, the v1.0.0.2 contracts
   (the unique-names rules, routes and card), the unique-names smoke, every carried smoke, both harnesses and the Linux VM
   checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.0.2-Logic.ps1` there (static).
5. Live: Players > Unique player names lists the known players' names; reserve and release a name and save; a second
   account joining with a taken name in another case is turned away.
