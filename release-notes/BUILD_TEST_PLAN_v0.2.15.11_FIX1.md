# Build / Test Plan — v0.2.15.11 FIX1

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.15.11-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Expected:
- validation: 0 errors / 0 warnings
- no CS1039 / CS1010 / CS1002 cascade from `MainWindow.ModCenter.cs`
- architecture harness passes, including extraction-safety checks

Then continue the v0.2.15.11 behavior-preservation runtime tests.
