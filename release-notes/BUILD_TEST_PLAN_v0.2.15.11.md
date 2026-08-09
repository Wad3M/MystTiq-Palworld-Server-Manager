# Build / Test Plan — v0.2.15.11

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.2.15.11-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime regression
1. Scan MOD Library and compare installed/enabled states with v0.2.15.10 FIX1.
2. Verify Selected and Verify All.
3. Run Scan Compatibility.
4. Export TXT/JSON verification report.
5. Confirm AntiDupe and PalImportFilter native-module evidence still works.
6. Enable/disable one MOD, refresh, then restore its state.
7. Start/restart PalServer and confirm runtime evidence session isolation.
8. Confirm Library/Dashboard event-driven synchronization.
9. Install/upgrade/delete/repair only with a disposable/test MOD if exercising destructive paths.
