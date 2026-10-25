<!-- MystTiq v1.1.5.0: file reviewed for this release (2026-10-25). -->
# v1.1.0.2 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.1.0.2-Logic.ps1 -RunBuild -RunLong`: the frozen v1.1.0.1 gate, every earlier contract, the v1.1.0.2
   contracts, every carried smoke, the long set, both harnesses and the Linux VM checks.
3. F-2: the LogicHarness checks the move on a made-up world (the rewrite, the refusals), the join check with the guard,
   and the service moving the owner's character on a copy of the real world with the real converter (216 references,
   Wade level 15 under the new ID). The ArtworkHarness renders the Player IDs card in every language.
4. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
5. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.1.0.2-Logic.ps1` there (static).
