# Build / Test Plan — v0.2.16.2

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File

.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All

.\scripts\Test-v0.2.16.2-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson
```

## Runtime acceptance

### Live-save safety
1. Start PalServer and open World Inspector.
2. Refresh/click through Inspector repeatedly while the server is running.
3. Trigger or wait for a world save and refresh Inspector during the write window.
4. Confirm no `Level.sav ... being used by another process` modal is shown.
5. If contention persists through the retry window, confirm the Inspector status surface says the save is currently being written and asks to retry.
6. Confirm inspection succeeds after the save stabilizes.
7. Confirm no files under the active world are modified by inspection.

### UI/button audit
1. Inspect Dashboard, Server, World, Players, Guilds/Bases, Backups, MOD Dashboard, MOD Library, UE4SS, Tools, Diagnostics, and Settings.
2. Confirm buttons are visibly ~10% more compact without clipped/wrapped labels.
3. Confirm equal-cell action groups align cleanly.
4. Confirm green/yellow/red/blue/purple/neutral semantic colors remain correct.
5. Confirm hover, pressed, disabled, focus, and tooltip behavior remain consistent.
6. Test common Windows DPI scaling and resize the application to verify no new clipping/overflow.
7. Confirm long labels such as Start Without MODs / verification / migration actions remain readable.

### Regression
Re-test Start / Stop / Restart, native MOD evidence, Operational Health, Backup, and WORLD PULSE.
