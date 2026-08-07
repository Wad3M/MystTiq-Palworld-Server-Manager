# Build & Test Plan — v0.2.14.4

## Build
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

## Navigation
1. Open World Inspector.
2. Confirm tabs for Overview, Players, Guilds, Bases, Saves, World Validator, World Management, Repair Center, and Transaction History.
3. Use the sidebar World Validator shortcut and confirm it opens World Inspector with World Validator selected.
4. Use the Players, Guilds, and Bases action buttons and confirm they navigate to the full managers.

## World Management
1. Confirm only Step 1 is active initially.
2. Confirm locked steps are grey and explain the prerequisite when hovered.
3. Select a test ZIP and confirm the banner updates to the next required step.
4. Progress through Analyze, Options, Plan, and Validation.
5. Confirm the final import remains blocked while PalServer is running.
6. Use only a disposable or backed-up world for Import & Activate.

## Validator & Regression
- Run World Validator from its nested tab.
- Confirm duplicate players remain consolidated.
- Confirm startup Guild/Base totals populate.
- Confirm notification diagnostics, coordinated live backup, Workspace, regular startup, and portable startup.
- Check 100%, 125%, and 150% scaling.
