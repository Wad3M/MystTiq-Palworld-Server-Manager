# Build & Test Plan — v0.4.5.1

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.5.1-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

After the GUI auto-launches, close the window and verify it remains available from the system tray. Exercise Show, Start, Stop, Restart, Exit GUI (keep backend running), and Stop Local Management Backend & Exit. Verify page-specific presentation and Start Server retryability.


## Runtime acceptance continuation
This candidate additionally restores hover help, player context actions, persistent activity/audit logging, Windows console capture, and canonical world filtering. The full v0.4.5.1 logic/runtime gate remains promotion-blocking until clean.

## Runtime parity continuation

- Configuration now includes a real active `PalWorldSettings.ini` OptionSettings editor through `/api/v1/palworld/config`.
- Save creates a timestamped rollback copy under `ConfigBackups`.
- PalServer readiness follows `PublicPort` from the active Palworld configuration instead of assuming UDP 8211.
- Generic/self-explanatory tooltips were removed; explanatory and safety tooltips remain.
- `docs/GUI_PARITY_v0.4.5.1.md` is the authoritative page-by-page legacy parity audit.
- Promotion still requires 0 validation warnings, v0.4.5.1 logic/runtime smoke, and manual Start Server + configuration-save acceptance.
