# Build / Test Plan — v0.2.15.15

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.15.15-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime regression

1. Launch MystTiq normally.
2. Start PalServer and confirm normal hidden/no-console startup behavior.
3. Confirm stdout/stderr and Pal.log monitoring.
4. Stop PalServer normally.
5. Restart PalServer normally.
6. Force Stop and confirm the complete PalServer process tree is removed.
7. Reopen MystTiq while PalServer is running and confirm adoption.
8. Run Verify All MODs and confirm native AntiDupe/PalImportFilter evidence still works.
9. Confirm v0.2.15.13 operational-health behavior remains correct for healthy, disabled, and Active / Unverified MODs.
10. Test Start Without MODs.
11. Test Server Update while stopped.
