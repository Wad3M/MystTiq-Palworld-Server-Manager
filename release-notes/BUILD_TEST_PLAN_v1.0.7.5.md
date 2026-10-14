<!-- MystTiq v1.1.0.2: file reviewed for this release (2026-10-13). -->
# v1.0.7.5 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.7.5-Logic.ps1 -RunBuild -RunLong`: the frozen v1.0.7.4 gate, every earlier contract, the v1.0.7.5
   contracts, every carried smoke, the long set (with a 10-minute soak), both harnesses and the Linux VM checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.7.5-Logic.ps1` there (static).
5. The 24-hour idle soak: `scripts/Measure-MystTiqIdleSoak.ps1 -Hours 24 -SampleMinutes 30` on Windows (run from a copy
   outside the working tree), and `-Target Linux -Action Start`, later `-Action Collect`, on the test VM.
