# Build / Test Plan — v0.3.0.4 FIX1

Run the full Windows gate:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.4-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson

.\Build.ps1 LinuxHeadless
```

Expected:

- validation reports **0 errors / 0 warnings**
- no CS1929 in `LocalManagementApiHost.cs`
- Windows Deployment / LinuxHeadless automation contract passes
- logic harness reports **0 failures**
- Linux headless package publishes successfully

Only after this gate is clean should the automated Linux deployment/acceptance workflow be run.
