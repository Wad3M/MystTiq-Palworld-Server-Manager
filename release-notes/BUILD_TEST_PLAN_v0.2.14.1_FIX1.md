# Build & Test Plan — v0.2.14.1 FIX1

## Purpose
Correct live Backup Center failures when Palworld holds an active player save with an exclusive file lock.

## Build
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

## Tests
1. Start the Palworld server and connect at least one player.
2. From Backup Center, click Backup Now while the server is running.
3. If the live snapshot succeeds, verify the archive normally.
4. If Palworld keeps a save locked, verify MystTiq offers the coordinated maintenance workflow instead of showing the old terminal failure.
5. Select No and confirm the server remains running and no partial backup remains.
6. Repeat, select Yes, and confirm MystTiq saves/stops the server, creates and verifies the backup, then starts the server again.
7. Confirm the new archive appears as Verified.
8. Confirm Dashboard operational state returns to Running.
9. Stop the server manually and verify ordinary stopped-server backups still work without a maintenance prompt.
10. Regression-test Restart, Stop, notifications, Players, World Inspector, Workspace, and restore safety backup.
