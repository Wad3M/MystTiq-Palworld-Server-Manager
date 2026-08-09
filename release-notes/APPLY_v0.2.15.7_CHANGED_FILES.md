# Apply Instructions — v0.2.15.7 Changed Files

1. Start from the validated v0.2.15.6/FIX2 source line.
2. Stop MystTiq and PalServer.
3. Back up the repository/source directory.
4. Delete the superseded active harness `scripts\Test-v0.2.15.6-Logic.ps1` if it exists.
5. Extract `MystTiqPalworldServer_v0.2.15.7_ChangedFiles.zip` over the repository root, preserving folders and replacing matching files.
6. Run the required Clean / Validate / All build sequence.
7. Run `scripts\Test-v0.2.15.7-Logic.ps1`.
8. Perform the runtime acceptance tests in `BUILD_TEST_PLAN_v0.2.15.7.md`.
9. Do not promote v0.2.15.7 until compile and runtime tests are accepted.


## FIX1 compile hotfix
If applying FIX1 over the initial v0.2.15.7 RC, replace the updated source/documentation files from the FIX1 changed-files package. No configuration migration or runtime-state reset is required. Re-run Clean, Validate, All, then the v0.2.15.7 logic harness.

## FIX2 notes
FIX2 modifies `RuntimeStateService.cs`, `MainWindow.xaml.cs`, the v0.2.15.7 logic harness, release documentation, and the source manifest. Apply the changed-files archive over the v0.2.15.7 FIX1 source, then run Clean / Validate / All and the logic harness before runtime testing.
