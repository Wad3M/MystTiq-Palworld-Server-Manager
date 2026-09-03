# v0.4.17.3 Build and Test Plan

1. Run `Build.ps1 Clean` and strict validation.
2. Run `scripts/Test-v0.4.17.3-Logic.ps1 -ProjectRoot . -RunBuild -ExportJson`.
3. Require WPF compatibility plus Windows/Linux headless and Avalonia builds.
4. Require runtime smoke for health, the aggregate poll, prior mutation safety, crash history, and read-only Save Tools diagnostics.
5. Regenerate and strictly validate the source manifest.
6. Package Full Source and Changed Files ZIPs and reject blocked build/runtime entries.
7. Install the Full Source package with the existing clean updater and repeat the complete gate before promotion.
