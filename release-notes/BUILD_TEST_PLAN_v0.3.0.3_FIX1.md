# Build / Test Plan — v0.3.0.3 FIX1

Run the complete Windows gate:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.3-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson

.\Build.ps1 LinuxHeadless
```

Expected:

- no CS1503 from `HeadlessConfigurationService.cs`
- no CS0103 for `QuoteSystemdArgument`
- `MystTiq.Core` builds successfully
- `MystTiq.HeadlessHost` builds successfully
- Windows WPF regression remains clean
- v0.3.0.3 harness reports zero failures
- Linux self-contained publish succeeds

After this gate passes, continue with the v0.3.0.3 Linux configuration/API acceptance.
