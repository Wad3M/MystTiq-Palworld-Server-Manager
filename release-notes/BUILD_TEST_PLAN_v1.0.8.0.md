<!-- MystTiq v1.1.6.0: file reviewed for this release (2026-10-26). -->
# v1.0.8.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.8.0-Logic.ps1 -RunBuild -RunLong`: the frozen v1.0.7.5 gate, every earlier contract, the v1.0.8.0
   contracts, every carried smoke, the long set (with the Ctrl+C check), both harnesses and the Linux VM checks.
3. The split's exit gate: the ArtworkHarness passes with every page rendered as before, the moved area lives only in its
   new file, and the main file's size is recorded.
4. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
5. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.8.0-Logic.ps1` there (static).
