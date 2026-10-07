<!-- MystTiq v1.0.6.1: file reviewed for this release (2026-10-06). -->
# v1.0.0.5 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.0.5-Logic.ps1 -RunBuild`: the frozen v1.0.0.4 gate, every earlier contract, the v1.0.0.5 contracts,
   every carried smoke, both harnesses and the Linux VM checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.0.5-Logic.ps1` there (static).
5. Live: World > Bases shows base cards with locations and workers, Show on map zooms to the base; World > Guilds shows
   the roster by name and the guild's bases.
