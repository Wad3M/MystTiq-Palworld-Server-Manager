<!-- MystTiq v1.1.5.0: file reviewed for this release (2026-10-23). -->
# v1.1.0.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.1.0.0-Logic.ps1 -RunBuild -RunLong`: the frozen v1.0.9.3 gate, every earlier contract, the v1.1.0.0
   contracts, every carried smoke, the long set, both harnesses and the Linux VM checks.
3. T-2: the documentation checks name v1.1.0.0 as the current version and the accepted baseline (README, site,
   roadmap, checklist, publishing guide), and every file and build target the documents name exists.
4. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
5. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.1.0.0-Logic.ps1` there (static).
