# Build / Test Plan — v0.2.15.9

## Build
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.2.15.9-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime
1. Start PalServer normally and wait at least 60 seconds.
2. Verify all MODs.
3. Confirm verification details show a Capability profile for UE4SS mods.
4. For AntiDupe and PalImportFilter, confirm Active / Unverified includes expected functional proof when no event has occurred.
5. Exercise only normal, safe gameplay that naturally triggers a MOD if practical.
6. Re-verify. If a MOD emits identifiable activity, it may promote to Confirmed Running.
7. Confirm no prior-session evidence is inherited after stop/restart.
8. Export TXT/JSON and confirm capability/evidence explanations are preserved.
