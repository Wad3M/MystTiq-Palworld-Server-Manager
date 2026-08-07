# Apply v0.2.13.2 Changed Files

1. Start from the validated v0.2.13.1 FIX1 source.
2. Close Visual Studio and MystTiq.
3. Extract the changed-files ZIP over the repository root, preserving folders.
4. Delete `bin`, `obj`, and prior `artifacts` folders before testing.
5. Unblock trusted scripts when required:
   `Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File`
6. Run `./scripts/Get-ProjectVersion.ps1`; expect `0.2.13.2`.
7. Run `./scripts/Build.ps1`.
8. Follow `BUILD_TEST_PLAN_v0.2.13.2.md`.
9. Do not promote this version until installed-mode and portable-mode tests pass.
