# Build and Test Plan — v0.2.14.9 FIX3

## Build
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

## Update Center
1. Open Update Center and run Check All.
2. Confirm every row action fits without clipping.
3. Confirm colors: Update amber; Verify green; Source blue; Check/Retry blue; Install/Enable/Create green; Disable warning amber; Manage blue.
4. Confirm each action remains clickable and performs its prior behavior.
5. Check at 100%, 125%, and 150% Windows scaling.

## Admin Commands
1. Enable Admin Commands and start PalServer through MystTiq.
2. Confirm the console status changes to Loaded after the MOD success line appears.
3. Restart MystTiq while PalServer remains running so the process is adopted.
4. Confirm the tailed recent Pal.log history restores Loaded status without restarting PalServer.
5. Stop PalServer and confirm the status resets to Not loaded.
6. Confirm an explicit Admin Commands load failure reports Not loaded.

## Regression
- Installer generation
- UE4SS live compatibility refresh
- Diagnostics nested scrolling
- Server start/stop/restart
- MOD inventory refresh
