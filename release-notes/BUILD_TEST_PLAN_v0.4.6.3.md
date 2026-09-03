# Build & Test Plan — v0.4.6.3

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.6.3-Logic.ps1 `
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

## v0.4.6.3 dashboard/deployment runtime acceptance

1. Place the v0.4.6.3 FullSource ZIP in the current Windows user's Downloads folder and confirm `Update-FromDownloads.ps1` identifies it as the newest FullSource package.
2. Confirm the updater runs `Build.ps1 Clean`, empties the old source tree, installs the staged package, unblocks scripts and validates the installed tree.
3. Start PalServer and confirm CPU history renders as a graph with live samples.
4. Confirm Active World shows its nickname and complete World ID; Copy World ID copies the complete canonical value.
5. Confirm World Pulse shows exact Day/time when decoded GameDateTimeTicks evidence exists, plus save age and backup age.
6. Confirm Live Console uses green text and includes redirected process output plus Pal.log content.
7. Confirm only one Dashboard heading is visible and Server Name/Description are populated.
8. Confirm the persistent lower-left Server Status area wraps correctly with no overlapping management-service text.
