# Build & Test Plan — v0.4.6.1

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.6.1-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

## Manual runtime acceptance

1. Confirm the GUI auto-launches and reaches **Connected** without requiring a token for its own local sidecar.
2. Confirm the connection detail identifies the MystTiq backend version/platform and the actual loopback endpoint used.
3. Press **Start Server** and verify the request reaches the lifecycle endpoint.
4. Confirm PalServer starts from the configured server root and readiness follows the configured `PublicPort`.
5. If UDP readiness is not reached, confirm the GUI displays the real lifecycle result rather than a generic connection failure.
6. Close to tray, reopen, and verify the same backend session remains manageable.
7. Verify a remote/LAN profile still requires its configured bearer token/TLS rules.
