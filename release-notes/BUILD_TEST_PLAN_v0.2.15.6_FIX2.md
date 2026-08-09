# Build / Test Plan — v0.2.15.6 FIX2

## Build
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.2.15.6-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Targeted runtime test
- Start with known-good UE4SS mods.
- Confirm Loaded appears.
- Keep server running 3 minutes; refresh at 60, 90, 120, and 180 seconds. Loaded must not revert to Not loaded.
- Verify & Scan All MODs must continue to report runtime evidence.
- Stop server and confirm session-loaded evidence is reset.
- Restart and confirm status is reacquired for the new session.
