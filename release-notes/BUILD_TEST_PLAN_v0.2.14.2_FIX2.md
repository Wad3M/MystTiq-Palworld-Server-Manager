# Build & Test Plan — v0.2.14.2 FIX2

## Build
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

## Player identity test
1. Close MystTiq completely.
2. Reopen it with the affected world selected.
3. Open Players without manually deleting the existing history file.
4. Confirm only one row remains for each real Player ID.
5. Confirm the retained row preserves the real player name and REST identifiers instead of the imported placeholder.
6. Confirm Dashboard known-player totals match the unique rows shown in Players.
7. Refresh Players while the server is running and confirm the duplicate does not return.
8. Restart MystTiq and confirm the cleanup persists.

## Regression
- Valid offline save-only players remain visible.
- Online players continue to merge with their existing history record.
- Notes and banned state remain attached to the retained canonical player.
- Player deletion and recovery resolve the retained player correctly.
- Guild, Base, World Inspector, notifications, workspace, and backup flows remain functional.
