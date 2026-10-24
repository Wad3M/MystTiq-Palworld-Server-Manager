<!-- MystTiq v1.1.5.0: file reviewed for this release (2026-10-24). -->
# v1.1.0.1 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.1.0.1-Logic.ps1 -RunBuild -RunLong`: the frozen v1.1.0.0 gate, every earlier contract, the v1.1.0.1
   contracts, every carried smoke, the long set, both harnesses and the Linux VM checks.
3. F-1: the ArtworkHarness runs the port check on a closed port, waits past Windows' refusal and collects; no socket error
   is left unobserved. The v1.1.0.0 method, run the same way in a standalone program, leaves one per check.
4. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
5. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.1.0.1-Logic.ps1` there (static).
