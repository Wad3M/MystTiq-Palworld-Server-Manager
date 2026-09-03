# Build & Test Plan — v0.4.4.3

From PowerShell 7 in the project root:

```powershell
cd C:\GameServers\MystTiqPalLinux
Get-ChildItem . -Recurse -Filter *.ps1 | Unblock-File
.\Build.ps1 Clean
.\Build.ps1 Validate
.\scripts\Test-v0.4.4.3-Logic.ps1 `
    -ProjectRoot . `
    -RunBuild `
    -ExportJson
```

`-RunBuild` validates and compiles the shared/Core, Windows headless, Linux headless, Windows Avalonia and Linux Avalonia targets, launches the Windows GUI after the successful desktop publish, then runs the isolated Windows headless API runtime smoke test.

Expected runtime-smoke passes:
- Windows headless API starts and answers `/healthz`.
- `/api/v1/status/poll` returns the aggregate status payload.
- `/api/v1/config/editable` returns editable configuration.
- `/api/v1/server/distribution` returns Windows distribution state.

After the automated gate, perform GUI acceptance: verify navigation row sizing, open each navigation destination, confirm the local API becomes Connected/Available, and verify Start/Stop/Restart are enabled when the headless API is connected.

Clean acceptance requirement: if an auto-launched development GUI or local sidecar is still running from `artifacts\publish`, `Build.ps1 Clean` must stop only those artifact-hosted MystTiq development processes, remove the artifacts tree, and fail explicitly if cleanup cannot complete.
