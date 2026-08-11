# Build / Test Plan — v0.3.0.2 FIX1

Run:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.2-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson

.\Build.ps1 LinuxHeadless
```

Expected:

- no `CS8997`
- no cascading parser errors in `LinuxSystemdServiceManager.cs`
- `MystTiq.Core` builds successfully
- Windows WPF regression remains clean
- v0.3.0.2 harness reports zero failures
- Linux self-contained publish succeeds
