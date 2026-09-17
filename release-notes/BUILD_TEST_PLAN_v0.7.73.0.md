# v0.7.73.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.73.0 logic suite (`scripts/Test-v0.7.73.0-Logic.ps1 -RunBuild`), including the frozen v0.7.72.0 checkpoint regression gate and the carried-forward smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/App.axaml.cs` (`ExitGui_OnClick` running-state check, new `UpdateTrayStatus()` + `trayStatusTimer`).
5. Verified: `dotnet build src/MystTiq.Desktop/MystTiq.Desktop.csproj` clean. No live GUI click-testing was possible in this environment (no GUI-automation capability, a disclosed limitation consistent with the rest of this project) — code-reviewed against the exact same `Tabs.Count(t => t.ServerIsRunning)` check `MainWindow_Closing` already uses successfully, not a new, unproven condition.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
