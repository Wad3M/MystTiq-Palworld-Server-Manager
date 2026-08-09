# Build / Test Plan — v0.2.15.14

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.15.14-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime regression
1. Launch MystTiq and confirm normal startup without service-construction errors.
2. Start, stop, and restart PalServer.
3. Reopen MystTiq while PalServer is running and confirm adoption.
4. Confirm CPU/RAM and server log monitoring.
5. Verify all MODs; native AntiDupe/PalImportFilter evidence must remain functional.
6. Confirm Overall Health behavior from v0.2.15.13 FIX2 remains unchanged.
7. Disable all MODs and confirm MOD health remains neutral.
8. Confirm server update and Start Without MODs behavior.
