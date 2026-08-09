# Build & Runtime Test Plan — v0.2.15.4

## Build
Run from the repository root:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Expected: 0 validation errors, 0 validation warnings, successful Release build, portable package, installer, and checksum verification.

## Runtime tests
1. Start PalServer with UE4SS and at least one enabled Lua mod.
2. Refresh the MOD library after startup.
3. Confirm the UE4SS/Lua row reports `RUNTIME = Active`.
4. Confirm a mod with `Starting Lua mod '<name>'` in the latest UE4SS log reports `LOADED = Loaded`.
5. Confirm an active-root mod with no startup evidence reports `Not loaded`.
6. Confirm a managed UE4SS/Lua mod whose files exist only outside the Active Mods Root reports `Misconfigured` / `Missing`.
7. Open MOD Runtime and confirm UE4SS Root, Active Mods Root, Legacy Mods Root, Runtime Mods Root, path health, directory counts, and loaded count are displayed.
8. With manager/runtime path disagreement, confirm diagnostics show Degraded / mismatch state.
9. Confirm PAK/Workshop-only mods show N/A for UE4SS runtime columns.
10. Confirm migration/install/enable/disable/delete behavior from v0.2.15.3 remains unchanged.

## Installer regression
- Confirm a fresh installer defaults to `C:\GameServers\MystTiqPalworldServer`.
- Confirm the destination can still be changed by the user.
- Confirm installed launches request administrator elevation as expected.
