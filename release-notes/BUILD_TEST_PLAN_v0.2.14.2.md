# Build & Test Plan — v0.2.14.2 Core Reliability

## Build

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

## Player discovery consistency

1. Place valid 32-hex player saves, `_dps.sav`, zero-byte saves, malformed names, and temporary `~RF*.TMP` files in a test Players folder.
2. Confirm Players, Dashboard, Guilds, World Inspector, World Tools, and Player Recovery agree on the valid-player count.
3. Confirm rejected files never appear as players.
4. Review world-discovery diagnostics for rejected-file reasons.

## Filesystem resilience

1. Refresh Players, Guilds, World Inspector, and World Tools while Palworld autosaves.
2. Remove or rename a test file during a scan.
3. Confirm the scan completes without a global application error.
4. Confirm access-denied or missing folders are treated as unavailable rather than fatal.

## Startup coordinator

1. Start MystTiq with a valid world and remain on Dashboard.
2. Confirm Players, Guilds, Bases, recovery, and Dashboard totals initialize automatically.
3. Repeat with a missing world, missing Players folder, and server running.
4. Confirm one failed startup stage does not block later stages.

## Regression

- Notification self-test and final auto-hide.
- Coordinated live backup.
- Workspace validation and persisted paths.
- Regular and portable startup.
- Configuration World Editing layout.
