# Build & Runtime Test Plan — v0.2.15.3

## Build
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Expected: 0 validation errors, 0 validation warnings, successful application build, portable package, installer, and checksum verification.

## Runtime — Migration
1. Confirm both `Win64\Mods` and the resolver-selected active root exist.
2. Stop PalServer.
3. Open MOD Dashboard and choose **MIGRATE LEGACY MODS**.
4. Confirm the preview shows legacy and active roots and counts.
5. Continue. Confirm missing user MOD files are copied into the active root.
6. Confirm legacy files remain unchanged.
7. Confirm known UE4SS runtime-component folders are skipped.
8. If the same file exists in both roots with different content, confirm the active file remains unchanged and a conflict is reported.
9. Refresh MOD inventory and confirm migrated user MODs appear from the active root.

## Runtime — ZIP normalization
Install test archives using these layouts:
- `Mods\TestMod\Scripts\main.lua`
- `TestMod\Scripts\main.lua`
- `ue4ss\Mods\TestMod\Scripts\main.lua`

For every layout, expected final path:
`<ActiveModsRoot>\TestMod\Scripts\main.lua`

Confirm no package creates `<ActiveModsRoot>\Mods\TestMod` or new content under legacy `Win64\Mods`.
