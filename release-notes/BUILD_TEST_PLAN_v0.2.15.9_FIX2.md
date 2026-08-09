# Build / Test Plan — v0.2.15.9 FIX2

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.2.15.9-FIX2-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime acceptance
1. Start PalServer and wait 60 seconds.
2. Refresh MOD Library.
3. Confirm AntiDupe and PalImportFilter classify as `UE4SS / Native` if they contain DLL/no Lua payload.
4. Verify all MODs.
5. Confirm their details show Native/C++ capability profile and safe static signals where observable.
6. Confirm static analysis alone does not mark a native mod Confirmed Loaded/Running.
7. Confirm quiet Active / Unverified mods produce `awaiting runtime confirmation`, not `need attention`.
8. Confirm genuine failures still produce ATTENTION/FAILED.
9. Restart server and confirm session evidence isolation remains intact.
