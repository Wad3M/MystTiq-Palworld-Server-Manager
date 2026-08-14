# Apply Instructions — v0.3.0.7 FIX1

Apply over the official **v0.3.0.6 FIX5** baseline / initial v0.3.0.7 RC files.

Extract the ChangedFiles ZIP over:

`C:\GameServers\MystTiqPalLinux`

Then run in PowerShell 7:

```powershell
cd C:\GameServers\MystTiqPalLinux

# IMPORTANT: unblock first so the cleanup helper can run under the current execution policy.
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\scripts\Apply-v0.3.0.7-Cleanup.ps1

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.3.0.7-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson

.\Build.ps1 LinuxHeadless
```

Do not proceed to Linux deployment until the Windows gate is clean.
