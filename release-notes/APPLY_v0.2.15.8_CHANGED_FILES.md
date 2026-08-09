# Apply Instructions — v0.2.15.8 Changed Files

1. Stop MystTiq and PalServer.
2. Back up the current repository/source folder.
3. Confirm the starting source is the validated **v0.2.15.7** baseline (including FIX2 runtime reacquisition corrections).
4. Extract `MystTiqPalworldServer_v0.2.15.8_ChangedFiles.zip` over the repository root, preserving folders and replacing matching files.
5. Delete `scripts\Test-v0.2.15.7-Logic.ps1` if it remains from the previous source tree; v0.2.15.8 uses `scripts\Test-v0.2.15.8-Logic.ps1`.
6. Run the PowerShell unblock command, then Build.ps1 Clean / Validate / All.
7. Run `scripts\Test-v0.2.15.8-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson`.
8. Perform the runtime acceptance tests in `BUILD_TEST_PLAN_v0.2.15.8.md`.
9. Do not promote v0.2.15.8 until compile and runtime acceptance are complete.

No server configuration, MOD-state, save, or data migration is required.
