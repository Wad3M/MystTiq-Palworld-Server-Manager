# Apply Instructions — v0.2.15.6 Changed Files

Baseline required: **MystTiq Palworld Server Manager v0.2.15.5 full source**.

1. Stop PalServer and close MystTiq Palworld Server Manager.
2. Back up the v0.2.15.5 source tree.
3. Extract `MystTiqPalworldServer_v0.2.15.6_ChangedFiles.zip` over the repository root, preserving folders and allowing replacement of existing files.
4. Do not flatten `src/PalworldManager/Models`, `src/PalworldManager/Services`, or `release-notes`.
5. Run the exact build sequence below.

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

After a successful build, complete `release-notes/BUILD_TEST_PLAN_v0.2.15.6.md`. Do not promote the baseline until runtime validation is complete.
