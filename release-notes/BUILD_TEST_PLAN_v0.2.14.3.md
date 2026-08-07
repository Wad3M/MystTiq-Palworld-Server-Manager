# Build & Test Plan — v0.2.14.3

## Build
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

## Dialog regression
- Start, stop, and restart confirmations.
- Backup delete and restore confirmations.
- World import and repair confirmations.
- MOD delete, enable, runtime, and installer confirmations.
- Information, warning, error, and Yes/No dialogs.
- Confirm destructive dialogs default to No where previously configured.

## UI standards
- Dashboard, Workspace, Players, MODs, Configuration, Backups and DataGrid action buttons.
- Check 100%, 125%, and 150% scaling.
- Confirm no clipped labels or oversized buttons.

## Regression
- Unique player count remains correct.
- Startup world-data initialization.
- Notification self-test and bell behavior.
- Coordinated live backup.
- Portable and installed startup.
