# Build Test Plan — v0.2.15.4 FIX1

Run from the repository root:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Expected:

- 0 validation errors
- 0 validation warnings
- successful Release build
- portable package generated
- installer generated
- SHA256 checksums verified

## Targeted Runtime Tests

### MystPalIntelligence

Select the ZIP containing:

`Mods\MystPalIntelligence\Scripts\main.lua`

Expected:

- no null-reference exception
- preview/install identifies a UE4SS Lua MOD
- final runtime path is beneath the resolver-selected Active Mods Root

### PalDefender

Select the ZIP containing root files:

- `PalDefender.dll`
- `d3d9.dll`

Expected:

- no null-reference exception
- preview/install identifies `Win64 Loader / Anti-Cheat`
- files target the Palworld Win64 directory
- overwrite protection remains active
