# Build & Runtime Test Plan — v0.2.15.3 FIX1

## Build
```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

Expected: 0 validation errors, 0 validation warnings, successful application build, portable package, installer, and checksum verification.

## Installer — fresh installation
1. Uninstall any test copy or use a Windows account/machine without a previous MystTiq install record.
2. Start the generated setup executable.
3. Confirm Windows requests administrator approval for Setup.
4. Confirm the default destination is `C:\GameServers\MystTiqPalworldServer`.
5. Complete installation with **Launch MystTiq Palworld Server Manager** selected.
6. Confirm MystTiq starts successfully with administrative privileges.
7. Close MystTiq and launch it again from the Start Menu shortcut. Confirm Windows requests administrator approval and the application starts.
8. If a desktop shortcut was selected, repeat the same test from the desktop shortcut.

## Installer — upgrade compatibility
1. Upgrade an existing MystTiq installation.
2. Confirm the installer retains the previously selected install directory (`UsePreviousAppDir=yes`).
3. Confirm the upgraded application still launches elevated.

## Regression
Repeat the v0.2.15.3 MOD migration and ZIP-normalization runtime tests. FIX1 changes installer/documentation behavior only.
