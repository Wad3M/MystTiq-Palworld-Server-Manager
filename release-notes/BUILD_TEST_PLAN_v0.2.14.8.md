# Build & Test Plan — v0.2.14.8

## Build

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

Expected version: `0.2.14.8`.

## Lifecycle tests

1. Launch the installed build and confirm normal startup.
2. Launch the portable build and confirm normal startup.
3. Close MystTiq immediately after launch while startup refreshes are still running.
4. Reopen MystTiq and confirm no stale process, locked log, or duplicate timer behavior.
5. Leave MystTiq open for at least two monitor intervals and confirm Dashboard status continues to update.
6. Close MystTiq with PalServer stopped.
7. Close MystTiq with PalServer running, decline the warning, then confirm the application stays open.
8. Repeat and accept the warning; confirm the server stops and MystTiq closes.

## Regression tests

- Diagnostics Center and support package.
- Transaction Center filters and details.
- Repair Center scan/preview.
- World Management wizard.
- Player identity deduplication.
- Startup player/guild/base totals.
- Notification self-test and final bell auto-hide.
- Coordinated live backup.
- Workspace validation.
- Start, stop, and restart controls.

## Pass criteria

- Build and portable packaging succeed.
- No startup or shutdown exception dialog appears.
- Closing during startup does not leave MystTiq or PalServer processes in an unexpected state.
- Timers do not fire twice after reopening the application.
- Existing workflows behave as in v0.2.14.7.
