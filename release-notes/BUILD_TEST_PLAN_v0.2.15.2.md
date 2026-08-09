# Build & Runtime Test Plan — v0.2.15.2

## Build
Run from the repository root:

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Expected: zero validation errors and a successful application/portable/installer build.

## Runtime Tests

### 1. Resolver consistency
On the current modern UE4SS installation, confirm startup diagnostics resolve:
`<Win64>\ue4ss\Mods`.

### 2. Inventory
Open MOD Dashboard / MOD library. Mods that exist only under legacy `<Win64>\Mods` must not be presented as healthy active-runtime mods. The active root inventory should reflect `<Win64>\ue4ss\Mods`.

### 3. Install
Install a known UE4SS Lua test mod. Confirm files are created under:
`<ActiveModsRoot>\<ModName>\...`
and not under legacy `<Win64>\Mods`.

### 4. Enable / Disable
Toggle the test mod. Confirm `mods.txt` / relevant enable markers are read or written under the active root only.

### 5. Repair States / Apply Enabled
Run state repair or Apply Enabled. Confirm only the active root is reconciled. Legacy copies must remain untouched.

### 6. Delete
Delete the test mod. Confirm the active-root copy is removed and legacy folders are not modified.

### 7. Folder actions
Open Mods Root and Open Selected Mod Folder. Confirm Explorer opens the active `ue4ss\Mods` path.

### 8. Legacy regression
On a legacy-layout test installation where only `<Win64>\Mods` exists, confirm the same operations still use that root.

### 9. No migration yet
If both roots exist, confirm v0.2.15.2 does not automatically copy, move, or delete legacy mods.
