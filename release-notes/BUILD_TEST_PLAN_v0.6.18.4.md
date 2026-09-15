# v0.6.18.4 Build and Test Plan

1. Close artifact-hosted MystTiq desktop and sidecar processes with Clean.
2. Run strict validation and the complete v0.6.18.4 logic suite (`scripts/Test-v0.6.18.4-Logic.ps1 -RunBuild`), including the frozen v0.6.18.3 checkpoint regression gate and the v0.5.1.5 runtime smoke suite (expected fully unaffected — this release is Desktop-only).
3. Build shared, Windows/Linux headless, and Windows/Linux Avalonia targets.
4. Confirm by inspection (no GUI click-through capability in this environment, disclosed gap since v0.6.4.0): with a real connected server, Start is disabled while `ServerIsRunning` is true, Stop/Restart are disabled while it's false; all three re-enable correctly as the polled status changes.
5. Create FullSource and Changed Files ZIPs and verify their entries and SHA-256 hashes.

Any failure blocks promotion.
