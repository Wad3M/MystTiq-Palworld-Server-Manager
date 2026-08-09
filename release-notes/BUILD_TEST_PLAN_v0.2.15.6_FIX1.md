# Build / Test Plan — v0.2.15.6 FIX1

Run from the repository root:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.2.15.6-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime-loaded regression

1. Start PalServer normally with known working UE4SS/Lua MODs enabled.
2. Wait for the 45-second startup MOD summary.
3. Open MOD Library. Expected: MODs with `Starting Lua mod '<runtime-folder>'` evidence show `LOADED = Loaded`.
4. Click Refresh in MOD Library. Expected: loaded states remain correct and do not revert to `Not loaded`.
5. Run Verify & Scan All MODs. Expected: matching UE4SS/Lua MODs report runtime evidence and Healthy when no other fault exists.
6. Confirm Workshop/managed packages whose display/package name differs from the UE4SS folder still match runtime evidence.
7. Stop PalServer and restart it. Repeat steps 2–5 to confirm the resolver is not carrying stale pre-start state.

## Startup gate regression

Confirm normal Start and Restart still pass through `ModLifecycleCoordinator`, while Start Without MODs remains available as the intentional bypass.
