# Build / Test Plan — v0.2.15.16

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.15.16-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime regression
1. Launch MystTiq normally.
2. Start, stop, restart, and Force Stop PalServer.
3. Reopen MystTiq with PalServer already running and confirm adoption.
4. Confirm CPU/RAM monitoring still follows the active PalServer processes.
5. Confirm guarded-port/server health detection remains correct.
6. Verify MODs and confirm native AntiDupe/PalImportFilter evidence remains functional.
7. Confirm disabled and Active / Unverified MODs remain neutral to Overall Health.
8. Test Start Without MODs.
9. Test SteamCMD server update while stopped.
10. Review README rendering and verify no duplicated release-history/feature sections remain.
