# Build / Test Plan — v0.3.0.1 FIX1

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.1-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson

.\Build.ps1 LinuxHeadless
```

Expected compile correction:

- no CS1061 for `IServerSessionInspector.FindProcessesByName`
- no CA1416 warning for `File.SetUnixFileMode` in `LinuxServerLifecycleService`
- Windows regression build remains clean
- v0.3.0.1 lifecycle harness passes with zero failures

After compile success, continue with the existing v0.3.0.1 Linux runtime acceptance sequence.
