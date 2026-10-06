<!-- MystTiq v1.0.4.0: file reviewed for this release (2026-10-05). -->
# v1.0.0.6 Build and Test Plan

1. `.\Build.ps1 Clean`, then `scripts/Validate-Release.ps1 -Strict`: 0 errors, 0 warnings.
2. `scripts/Test-v1.0.0.6-Logic.ps1 -RunBuild`: the frozen v1.0.0.5 gate, every earlier contract, the v1.0.0.6 contracts,
   every carried smoke, both harnesses and the Linux VM checks.
3. `Build.ps1 Package`, then `scripts/Test-v0.9.9.0-Distribution.ps1`.
4. Checkpoint, then extract the FullSource ZIP into an empty folder and run `Test-v1.0.0.6-Logic.ps1` there (static).
5. Live: Server Setup, Workspace, Update Center, Doctor and Security show red Delete/Remove, purple Open/Manage, green
   Verify/Rescan, and coloured tags (green READY, amber Update available, grey Self-updating, red MISSING).
