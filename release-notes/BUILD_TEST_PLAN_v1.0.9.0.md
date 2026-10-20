<!-- MystTiq v1.1.4.0: file reviewed for this release (2026-10-20). -->
# v1.0.9.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.9.0-Logic.ps1 -RunBuild -RunLong`: the frozen v1.0.8.9 gate, every earlier contract, the v1.0.9.0
   contracts, every carried smoke, the long set, both harnesses and the Linux VM checks.
3. T-1: the reachability check passes (every desktop command is used beyond its declaration), and the ArtworkHarness
   passes with every page rendered as before.
4. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
5. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.9.0-Logic.ps1` there (static).
