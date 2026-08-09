# Apply Instructions — v0.2.15.5

Apply the changed-files archive over a clean v0.2.15.4 FIX1 working tree, or use the complete source archive.

Then run:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Do not promote v0.2.15.5 until the centralized health-state and Workshop identity runtime tests pass.
