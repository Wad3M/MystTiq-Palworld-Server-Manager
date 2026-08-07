# Build & Test Plan — v0.2.14.6

## Build

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

## Transaction Center
1. Open World Inspector → Transaction Center.
2. Confirm the page loads without modifying any world/save timestamps.
3. Click Refresh and confirm existing durable journals appear.
4. Verify search and state/operation filters.
5. Select a record and review stages/details.
6. Verify Open Backup is enabled only when the recorded backup exists.
7. Verify Open Report is enabled only when the recorded report exists.
8. Temporarily place a malformed JSON file in a transaction folder and confirm refresh continues while the error is logged.
9. Confirm rollback is displayed as availability only; no rollback action is offered.

## Regression
- Repair Center scan and preview.
- World Management wizard.
- Unique-player identity totals.
- Startup Players/Guilds/Bases loading.
- Notification diagnostics and bell behavior.
- Coordinated live backup.
- Regular and portable startup.
