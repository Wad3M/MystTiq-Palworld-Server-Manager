<!-- MystTiq v1.1.5.0: file reviewed for this release (2026-10-24). -->
# v1.0.9.3 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.9.3-Logic.ps1 -RunBuild -RunLong`: the frozen v1.0.9.2 gate, every earlier contract, the v1.0.9.3
   contracts, every carried smoke, the long set, both harnesses and the Linux VM checks.
3. B-2: the two LogicHarness scenarios (the bands and the gap check; the rule in the anti-cheat service) pass, and the
   Anti-Cheat card shows the rule's row in 12 languages. Live: a week of the owner's server with the rule on (E-16).
4. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
5. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.9.3-Logic.ps1` there (static).
