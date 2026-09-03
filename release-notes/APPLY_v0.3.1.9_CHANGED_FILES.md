# Apply Instructions — v0.3.1.9

Extract the v0.3.1.9 Changed Files package over:

```text
C:\GameServers\MystTiqPalLinux
```

Then run:

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\scripts\Apply-v0.3.1.9-Cleanup.ps1

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.9-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected validation result:

```text
Validation summary: 0 error(s), 0 warning(s).
Release validation passed.
```

Expected logic result:

```text
Failed : 0
```

After local validation:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

The secured production-listener lifecycle test may remain an intentional `SKIP`, not a warning.
