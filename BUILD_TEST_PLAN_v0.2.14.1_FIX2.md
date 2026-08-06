# Build & Test Plan — v0.2.14.1 FIX2

## Purpose
Ensure world-backed Dashboard, Players, Guilds, Bases, and recovery summaries initialize automatically on first launch instead of waiting for a manual tab refresh.

## Build

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

## Startup tests
1. Close MystTiq completely.
2. Start it with an existing Palworld world configured.
3. Remain on Dashboard; do not visit Guilds or Bases.
4. Confirm Guilds & Bases no longer remains at zero when the world contains guilds/bases.
5. Open Players, Guilds, Bases, and Recovery and confirm their data is already populated.
6. Close and relaunch with the server running; repeat.
7. Close and relaunch with the server stopped; repeat.
8. Test a configuration with no valid world and confirm startup remains responsive and shows the normal no-world status rather than crashing.

## Regression tests
- Dashboard refresh button still works.
- Manual Guilds and Bases refresh buttons still work.
- Active-world changes still refresh the correct world.
- Coordinated live backup from FIX1 still works.
- Workspace page, notifications, mods, backups, and server controls remain functional.
