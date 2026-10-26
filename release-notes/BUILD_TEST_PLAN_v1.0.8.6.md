<!-- MystTiq v1.1.6.0: file reviewed for this release (2026-10-26). -->
# v1.0.8.6 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.8.6-Logic.ps1 -RunBuild -RunLong`: the frozen v1.0.8.5 gate, every earlier contract, the v1.0.8.6
   contracts, every carried smoke, the long set, both harnesses and the Linux VM checks.
3. The split's exit gate: the ArtworkHarness passes with every page rendered as before, the moved areas live only in
   their new files, and the sizes are recorded.
4. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
5. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.8.6-Logic.ps1` there (static).
