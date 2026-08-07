# Build & Test Plan — v0.2.13.2

## Build

1. Unblock trusted project scripts if Windows marked them as downloaded:
   `Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File`
2. Run `./scripts/Get-ProjectVersion.ps1`; expect `0.2.13.2`.
3. Run `./scripts/Build.ps1`; expect a successful Release build.
4. Run `./scripts/Package-Portable.ps1`.
5. Confirm `artifacts/MystTiqPalworldServer-v0.2.13.2-win-x64-portable.zip` and `SHA256SUMS.txt` exist.

## Portable layout

Extract the ZIP to a new writable folder. Confirm it contains:

- `MystTiqPalworldServer.exe`
- `portable.mode`
- `Data/README.txt`
- `Workspace/README.txt`
- `Workspace/Servers`
- `Workspace/SteamCMD`
- `Workspace/Backups`
- `Workspace/Downloads`
- `Workspace/Exports`

## Portable discovery

1. Put a valid Palworld server installation under `Workspace/Servers/<any folder>`.
2. Put SteamCMD under `Workspace/SteamCMD/<any folder>`.
3. Launch MystTiq for the first time.
4. Confirm Manager Settings detects the folder containing `PalServer.exe`.
5. Confirm it detects the discovered `steamcmd.exe`.
6. Confirm Backup Root defaults to `Workspace/Backups`.
7. Close and reopen the app; confirm the paths remain remembered.
8. Confirm settings are written under `Data/Settings` rather than ProgramData.
9. Confirm activity, notifications, cache, diagnostics, and window placement remain under `Data`.

## External paths

Manually select a server outside the portable folder, save settings, restart, and confirm the external path remains selected.

## Installed-mode regression

Build or run without `portable.mode` and confirm existing settings continue loading from the established ProgramData location. Confirm portable workspace defaults are not forced on installed mode.

## Write protection

Copy the portable build to a location the current user cannot write to. Confirm startup reports a clear working-directory permissions error rather than failing silently.

## Application regression

Test Dashboard, notification bell, server controls, backups, Players, World Inspector, MOD management, and ownership exports.
