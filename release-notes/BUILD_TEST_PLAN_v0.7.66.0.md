# v0.7.66.0 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.7.66.0 logic suite (`scripts/Test-v0.7.66.0-Logic.ps1 -RunBuild`), including the frozen v0.7.65.0 checkpoint regression gate and the carried-forward v0.5.1.5/v0.7.12.0/v0.7.15.0/v0.7.17.0/v0.7.64.0 smoke suites.
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. New surface this release: `src/MystTiq.Desktop/Views/TrayReminderToast.axaml.cs` (`owner` parameter, screen resolution prefers the owner), `src/MystTiq.Desktop/App.axaml.cs` (`ShowTrayStillRunningReminder` passes `mainWindow` as owner).
5. **Not verified visually** — no way to render the native Avalonia window or reproduce the reported multi-monitor layout (secondary monitor at a negative Y origin) in this environment. The fix targets the exact mechanism identified in the bug report; live confirmation on the actual hardware is the only way to fully close this out.
6. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion, except the disclosed live-visual-verification gap noted in step 5.
