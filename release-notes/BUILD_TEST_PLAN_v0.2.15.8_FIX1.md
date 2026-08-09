# Build / Test Plan — v0.2.15.8 FIX1

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
.\scripts\Test-v0.2.15.8-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

Runtime: confirm positive-evidence mods show Loaded/Healthy; AntiDupe and PalImportFilter show either Loaded/Healthy if proof exists or Active / Unverified if silent. They must not show Not loaded solely because no signature exists. Export verification and confirm evidence state/confidence/source detail. Repeat after 5 minutes and after a server restart.
