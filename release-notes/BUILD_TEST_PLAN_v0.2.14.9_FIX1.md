# Build and Test Plan — v0.2.14.9 FIX1

## Install the installer compiler once
```powershell
.\Build.ps1 InstallerTools
```
Open a new PowerShell 7 window afterward.

## Full release build
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Expected artifacts:
- `MystTiqPalworldServer-v0.2.14.9-win-x64-portable.zip`
- `MystTiqPalworldServer-v0.2.14.9-win-x64-setup.exe`
- `SHA256SUMS.txt` containing both files

## Installer tests
1. Install for the current user.
2. Confirm Start Menu and optional Desktop shortcuts.
3. Launch and confirm installed mode; `portable.mode` must be absent.
4. Re-run the installer and confirm upgrade/repair behavior.
5. Uninstall and confirm external Palworld server data is untouched.

## UE4SS compatibility tests
1. Open UE4SS and note the compatibility timestamp.
2. Enable or disable a MOD and confirm the card refreshes.
3. Install or delete a test MOD and confirm totals refresh.
4. Run compatibility scan and confirm attention/conflict/failure counts update.
5. Confirm no hard-coded Admin Commands claim remains.
