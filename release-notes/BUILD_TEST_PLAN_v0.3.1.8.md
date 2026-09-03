# Build / Test Plan — v0.3.1.8

```powershell
cd C:\GameServers\MystTiqPalLinux

Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\scripts\Apply-v0.3.1.8-Cleanup.ps1

.\Build.ps1 Clean
.\Build.ps1 Validate

.\scripts\Test-v0.3.1.8-Logic.ps1 `
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

Live acceptance:

1. Open Players & Guilds → Refresh Identities.
2. Confirm player save IDs match the active world's Players directory.
3. If decoded Level.sav.json exists, confirm known player names and guild memberships.
4. Confirm guild leader/member/base counts are plausible.
5. Confirm missing leader save is shown as Orphaned / Needs Review.
6. If decoded semantic JSON is absent, confirm the UI says Player-save identities only rather than producing guessed guild data.
7. Confirm the page exposes no repair/delete/migration action.
8. Re-run World Explorer, Setup & Update, Doctor, Monitoring, Backups and lifecycle regressions.
