# Build & Test Plan — v0.2.14.1 FIX5

## Purpose
Correct the Workspace Manager XAML startup failure caused by missing case-sensitive style resources.

## Build
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

## Startup validation
1. Launch the regular build.
2. Launch a freshly extracted portable build.
3. Confirm neither build shows a `WorkspaceRefreshButton` or `StaticResourceExtension` startup error.

## Workspace validation
1. Open **Server → Workspace**.
2. Confirm Refresh, Validate All, Browse, Open, and Save Paths buttons render.
3. Confirm the buttons remain compact and use the correct semantic MystTiq colors.
4. Check 100%, 125%, and 150% Windows display scaling for clipping or overlap.

## Regression checks
- Notification self-test
- Startup Guild/Base loading
- Player validation
- Configuration compact World section
- Running-server coordinated backup
