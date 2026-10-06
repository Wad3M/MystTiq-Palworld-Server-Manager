<!-- MystTiq v1.0.5.0: file reviewed for this release (2026-10-06). -->
# v1.0.5.0 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.5.0-Logic.ps1 -RunBuild`: the frozen v1.0.4.0 gate, every earlier contract, the v1.0.5.0
   contracts, every carried smoke (the MOD archive smoke included), both harnesses and the Linux VM checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.5.0-Logic.ps1` there (static).
5. Owed (roadmap evidence): one install on the clone through the real Nexus path with the owner's account (a free
   account: Mod Manager Download, with MystTiq taking those links).
