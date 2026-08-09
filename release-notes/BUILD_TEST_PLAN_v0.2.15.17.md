# Build / Test Plan — v0.2.15.17

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.15.17-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime acceptance
1. Start PalServer and confirm Dashboard UPTIME starts from the PalServer session, not MystTiq launch time.
2. Confirm WORLD PULSE appears without disrupting the existing Dashboard layout.
3. Confirm `Day N • HH:MM` appears when decoded active-world JSON contains `GameDateTimeTicks`.
4. Compare the WORLD PULSE day/time against the in-game clock shortly after a Palworld save.
5. Confirm the save freshness counter resets after `Level.sav` is written.
6. Join with a player: online/peak/joins/unique update once and Activity records one join event.
7. Leave: online/leaves update once and Activity records one leave event.
8. Restart PalServer: session metrics reset and a new session ID/uptime is used.
9. Confirm the latest backup age is correct.
10. Temporarily make decoded world JSON unavailable and confirm the world clock shows unavailable rather than an estimated value.
11. Re-run MOD verification and confirm AntiDupe/PalImportFilter native evidence and Overall Health remain unchanged.
