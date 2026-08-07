# Build Test Plan — v0.2.14.2 FIX1

## Build validation

```powershell
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 All
```

Expected results:

- Restore succeeds.
- Release x64 build succeeds.
- Portable publish succeeds.
- `MystTiqPalworldServer-v0.2.14.2-win-x64-portable.zip` is created.
- `SHA256SUMS.txt` is created.
- Installer is built when Inno Setup 6 is available; otherwise only the installer step is skipped.

## Fail-fast validation

Temporarily introduce a harmless compile error in a local test copy and run:

```powershell
.\Build.ps1 All
```

Confirm:

- The command exits after the build failure.
- Portable packaging does not run.
- No successful release-preparation message is shown.

Undo the temporary compile error before continuing.

## Runtime regression checks

- Regular and portable builds launch.
- Dashboard startup data initializes.
- Player totals agree across Players, Dashboard, Guilds, and World tools.
- Notification self-test passes.
- Workspace validation passes.
- Coordinated live backup still works.
