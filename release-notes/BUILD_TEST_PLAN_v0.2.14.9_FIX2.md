# Build & Test Plan — v0.2.14.9 FIX2

## Build

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

## Diagnostics Center

1. Run diagnostics until the results grid contains enough rows to scroll.
2. Place the pointer over the center of the results grid.
3. Confirm the wheel scrolls the grid.
4. Reach the top or bottom of the grid and continue scrolling.
5. Confirm the containing Diagnostics page continues scrolling.
6. Place the pointer over a status card and confirm the page scrolls directly.

## Cross-page scroll audit

Repeat the same boundary test on pages containing nested grids, lists, or multiline logs:

- Transaction Center
- Players
- Guilds and Bases
- Backup Center
- MOD and UE4SS pages
- Configuration
- World Inspector and World Validator
- Repair Center and World Management
- Save Tools diagnostics
- Activity and Notifications

Confirm nested content scrolls first and the containing page takes over at its boundary.

## Regression checks

- Combo boxes still open and select normally.
- Sliders still respond normally.
- DataGrid row selection remains functional.
- Horizontal scrolling is unchanged.
- Touchpad and high-resolution wheel input do not jump excessively.
- Installed and portable builds behave identically.
