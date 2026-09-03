# Build / Test Plan — v0.3.1.4

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\scripts\Apply-v0.3.1.4-Cleanup.ps1

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.4-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

Expected:

- validation 0 errors / 0 warnings
- Windows Avalonia publish PASS
- Linux Avalonia publish PASS
- Linux headless publish PASS
- logic failures 0

## Live Linux acceptance

Deploy the v0.3.1.4 headless package:

```powershell
.\scripts\Deploy-Test-MystTiqLinux.ps1 -Extended
```

Then in the GUI verify:

1. Backups page loads current inventory.
2. Backup Now creates a new verified entry.
3. Delete Selected removes only the selected managed backup.
4. Restore refuses while PalServer is running.
5. Stop PalServer, restore a test backup, and confirm a safety backup is created.
6. Settings → Managed Server / Headless Configuration loads current values.
7. Save an intentionally invalid value and confirm validation rejects it.
8. Save a harmless valid lifecycle change and confirm the GUI reports restart required.
9. Confirm `/etc/mysttiq/mysttiq.json.pre-api-*.bak` exists.
10. Restart MystTiq service and confirm the new value is effective.
11. Confirm remote authentication and TLS remain enabled.
12. Re-run Start / Stop / Restart, Players and Monitoring regressions.
