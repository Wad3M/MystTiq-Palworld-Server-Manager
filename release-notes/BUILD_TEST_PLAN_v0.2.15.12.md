# Build / Test Plan — v0.2.15.12

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.15.12-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime acceptance
1. Start PalServer normally; Dashboard reaches Running.
2. Stop PalServer normally; lifecycle reaches Stopped and cleanup completes.
3. Restart PalServer; a new server session/PID snapshot is created.
4. Close/reopen MystTiq while PalServer is running; adoption succeeds.
5. Verify resource CPU/RAM monitoring remains functional.
6. Verify stdout/stderr/Pal.log monitoring remains functional.
7. Verify guarded server ports continue to be observed correctly.
8. Run MOD Verify All after 30-60 seconds.
9. AntiDupe and PalImportFilter must retain native module evidence and reach Confirmed Loaded when their exact DLLs are mapped.
10. Stop/start and verify old native module evidence is not inherited by the new session.
11. Run Update Server while stopped and confirm update behavior is unchanged.
12. Exercise Start Without MODs if available and confirm `-NoMods` behavior is unchanged.
