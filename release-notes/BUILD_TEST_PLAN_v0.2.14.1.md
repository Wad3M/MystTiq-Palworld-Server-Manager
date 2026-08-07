# Build & Test Plan — v0.2.14.1 Workspace Manager UI

## Build

From the repository root:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

Expected version: `0.2.14.1`.

## Workspace page

1. Open **Server → Workspace**.
2. Confirm the page shows Portable or Installed mode correctly.
3. Confirm every displayed path matches the active deployment.
4. Click every **Open** button and confirm the expected folder opens.
5. Test **Browse** for Server, SteamCMD, and Backups.
6. Click **Save Paths**, restart MystTiq, and confirm the selected paths persist.
7. Confirm the Settings page reflects the same three paths.

## Validation

1. Run **Validate All** with a valid server and SteamCMD.
2. Confirm PalServer.exe is detected.
3. Temporarily select a folder without PalServer.exe and confirm an attention result.
4. Select an unwritable or invalid backup location and confirm a useful validation message.
5. Verify validation does not edit Palworld saves or server files.

## Regression

- Dashboard loads.
- Server start/stop/restart works.
- Notification bell behavior remains correct.
- Backups, Players, Guilds, World Inspector, MOD tools, and World Validator open normally.
- Portable package starts and detects its workspace.
- Installed build continues using Windows application-data locations.
