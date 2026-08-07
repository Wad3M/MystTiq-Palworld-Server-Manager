# Apply Instructions — v0.2.14.11 Changed Files

## Recommended Method

Use the complete-project ZIP for the cleanest validation.

## Changed-Files Method

1. Back up the current v0.2.14.10 source tree.
2. Extract the changed-files ZIP into the repository root.
3. Allow files to overwrite their matching paths.
4. Do not delete files not included in the changed-files package.
5. Run:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

## Key Source Changes

- `src/PalworldManager/MainWindow.xaml`
- `src/PalworldManager/MainWindow.xaml.cs`
- `src/PalworldManager/MainWindow.ScrollRouting.cs`
- `src/PalworldManager/app.manifest`
- `Directory.Build.props`
- versioned build/release documentation
