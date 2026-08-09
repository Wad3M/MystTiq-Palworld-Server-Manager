# Apply Instructions — v0.2.15.2 Changed Files

Apply the changed-files package over the validated v0.2.15.1 source tree, preserving relative paths.

Key source changes include:
- `src/PalworldManager/Services/ModService.cs`
- `src/PalworldManager/Services/ModScannerService.cs`
- `src/PalworldManager/Services/ServerDoctorService.cs`
- `src/PalworldManager/Services/ModCompatibilityService.cs`
- `src/PalworldManager/MainWindow.xaml.cs`
- version and documentation files

Then run:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Do not promote v0.2.15.2 until the Phase 2 runtime tests pass.
