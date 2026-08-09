# Apply Instructions — v0.2.15.3 Changed Files

1. Apply the changed-files package over **v0.2.15.2 FIX1** or use the full-source package.
2. From the repository root run:
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```
3. Perform the migration and ZIP normalization tests in `BUILD_TEST_PLAN_v0.2.15.3.md`.
4. Do not delete the legacy `Win64\Mods` directory during this phase.
5. Promote v0.2.15.3 only after compile and runtime validation pass.
