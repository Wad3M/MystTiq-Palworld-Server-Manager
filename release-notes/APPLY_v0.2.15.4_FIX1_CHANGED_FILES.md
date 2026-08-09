# Apply Instructions — v0.2.15.4 FIX1

Apply the changed files over a clean v0.2.15.4 source tree.

Primary source change:

- `src/PalworldManager/Services/ModService.cs`

Then run:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

This is a hotfix to v0.2.15.4. The application version remains `0.2.15.4`.
