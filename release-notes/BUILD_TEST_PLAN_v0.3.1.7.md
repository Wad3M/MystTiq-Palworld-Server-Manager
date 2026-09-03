# Build / Test Plan — v0.3.1.7

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\scripts\Apply-v0.3.1.7-Cleanup.ps1

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.7-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected:

```text
Validation summary: 0 error(s), 0 warning(s)
Failed : 0
```

Deploy:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

On the secured production listener, the formerly noisy lifecycle result should now appear as:

```text
[SKIP] Extended API lifecycle :: current API uses TLS/auth/non-default bind
```

and the summary should report:

```text
Warnings: 0
Skipped: 1
```

Live GUI acceptance:

1. Open World Explorer.
2. Refresh World.
3. Confirm the expected active World ID is resolved.
4. Confirm Level.sav is categorized as World / Required Present.
5. Confirm player `.sav` files appear under Player Save.
6. Confirm counts and total size are plausible.
7. Confirm no modify/delete/restore actions are offered on this page.
8. Re-run Setup & Update, Doctor, Players, Monitoring, Backups and lifecycle regressions.
