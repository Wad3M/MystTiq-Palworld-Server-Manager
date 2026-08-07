# Build Test Plan — v0.2.14.10

## Required sequence

Run from the repository root in PowerShell:

```powershell
.\Build.ps1 Clean
.\Build.ps1 Validate
.\Build.ps1 All
```

## Expected results

### Clean
- Removes `artifacts/`.
- Removes all project `bin/` and `obj/` directories.
- Ends with `Clean complete.`

### Validate
- Identifies version `0.2.14.10`.
- Reports zero errors.
- Reports `Release validation passed.`

### All
- Restores and builds the Windows x64 project.
- Publishes the self-contained portable application.
- Produces `artifacts/MystTiqPalworldServer-v0.2.14.10-win-x64-portable.zip`.
- Detects Inno Setup 6 or 7.
- Produces `artifacts/MystTiqPalworldServer-v0.2.14.10-win-x64-setup.exe`.
- Produces `artifacts/SHA256SUMS.txt` with both assets.
- Verifies all generated checksums.

## Detection tests

Test at least the normal installed Inno Setup configuration. Optional targeted checks:

```powershell
.\Build.ps1 Installer -ISCC "C:\Path\To\ISCC.exe"
$env:INNO_SETUP_HOME = "C:\Program Files (x86)\Inno Setup 6"
.\Build.ps1 Installer
```

## Asset verification

```powershell
.\scripts\Build-Checksums.ps1 -Verify
```

## Runtime smoke tests

1. Extract the portable ZIP into a new folder.
2. Launch with `Start-MystTiq.cmd` and directly with `MystTiqPalworldServer.exe`.
3. Confirm settings and generated data remain under the portable folder.
4. Install using the setup EXE.
5. Confirm Start Menu and optional desktop shortcuts.
6. Launch the installed application.
7. Install the same version over itself to test upgrade behavior.
8. Uninstall and confirm application files are removed without touching external Palworld server data.

## Failure reporting

Provide the full PowerShell command, complete error text, and which stage failed: Clean, Validate, Build, Package, Installer, Checksums, or Runtime.
