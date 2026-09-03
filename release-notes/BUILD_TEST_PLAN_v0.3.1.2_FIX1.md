# Build / Test Plan — v0.3.1.2 FIX1

Apply FIX1 over v0.3.1.2.

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\scripts\Apply-v0.3.1.2-Cleanup.ps1

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.2-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected:

```text
Validation summary: 0 error(s), 0 warning(s)
Failed : 0
```

The already-completed GUI lifecycle functional acceptance remains valid because FIX1 changes release hygiene only.
