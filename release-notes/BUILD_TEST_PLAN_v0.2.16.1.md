# Build / Test Plan — v0.2.16.1

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.2.16.1-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime acceptance
1. Start/stop/restart PalServer normally.
2. Verify server install/update paths remain unchanged.
3. Verify UE4SS active root and MOD Library results match v0.2.15.17.
4. Verify AntiDupe/PalImportFilter native runtime evidence remains correct.
5. Install/verify UE4SS and confirm files target the existing Win64 runtime directory.
6. Run Server Doctor and MOD compatibility checks.
7. Confirm Overall Health semantics remain unchanged.
8. Confirm WORLD PULSE remains functional.
