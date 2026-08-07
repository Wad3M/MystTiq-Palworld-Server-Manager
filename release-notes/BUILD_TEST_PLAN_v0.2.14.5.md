# Build & Test Plan — v0.2.14.5

## Build

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

## Navigation

- Confirm the left navigation contains World Inspector but no separate World Validator entry.
- Open World Inspector and verify World Validator, World Management, Repair Center, and Transaction History tabs remain available.

## Repair Center

- Open Repair Center before scanning and confirm guidance requests a scan.
- Select Scan World and confirm candidate counts populate.
- Select and clear candidates.
- Preview selected candidates and confirm no world files are changed.
- Confirm zero-candidate worlds show a healthy/no-candidates message.

## Regression

- Player identity deduplication remains effective.
- Startup Players/Guilds/Bases loading works.
- Notification diagnostics and bell behavior work.
- Coordinated live backup works.
- World Management wizard remains usable.
- Installed and portable builds launch successfully.
