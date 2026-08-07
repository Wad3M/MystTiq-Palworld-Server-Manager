# Build & Runtime Test Plan — v0.2.14.11

## Compile

Run from the repository root:

```powershell
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Expected: zero validation errors, successful Release win-x64 build, portable ZIP, installer, and verified SHA256 checksums.

## Update Center

1. Open Update Center and confirm **CHECK ALL UPDATES** uses the standard blue refresh styling.
2. Hover the button and confirm the tooltip explains that update information is refreshed.
3. Run Check All and confirm the button disables while checking, changes to the checking state, and re-enables afterward.
4. Confirm update rows retain appropriate semantic action colors for UPDATE, INSTALL, VERIFY, SOURCE, RETRY, and CHECK.
5. Confirm no layout clipping at the minimum supported window size.

## Admin Commands Refresh

1. Start the Palworld server with Admin Commands enabled and confirm a successful runtime line changes the status to loaded.
2. Navigate away from Console and back, then select **REFRESH STATUS**. Confirm the loaded state remains accurate.
3. Select **REFRESH VIEW** and confirm both the visible console and Admin Commands status refresh.
4. Stop the server and confirm the status returns to not loaded.
5. Start without Admin Commands or without matching runtime evidence, select Refresh Status, and confirm it does not falsely report loaded.
6. Confirm the refresh action writes a clear result to the activity/session log.

## Scroll Routing

1. Test mouse-wheel scrolling over the sidebar, Update Center grid, Console, Players grid, configuration lists, and nested detail panels.
2. At an inner control's top or bottom boundary, continue scrolling and confirm the nearest eligible outer ScrollViewer takes over.
3. Confirm controls with vertical scrolling disabled do not capture vertical wheel input.
4. Test a precision touchpad and confirm small deltas scroll smoothly.
5. Hold Shift over a horizontally scrollable grid/text area and use the wheel; confirm horizontal movement.
6. Confirm normal wheel movement does not change ComboBox selection unexpectedly.
